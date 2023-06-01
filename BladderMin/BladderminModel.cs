using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace BladderMin
{

    public struct BladderMinMarginResults
    {
        public double SupMargin; // initial margin for superior direction, mm
        public double AntMargin; // initial margin for anterior direction, mm
        public double TotalSupMargin;
        public double TotalAntMargin;
        public double InfMarginSmoothingMargin; // margin used to smooth the inferior margin by expansion then joining with the original structure, mm
    }
    public struct BladderMinMarginInitializationParameters
    {
        public double InitialSupMargin; // initial margin for superior direction, mm
        public double InitialAntMargin; // initial margin for anterior direction, mm
        public double InfMarginSmoothingMargin; // margin used to smooth the inferior margin by expansion then joining with the original structure, mm
        public double SupMarginReductionIncrement; // amount to increase internal sup margin by in each iteration, mm
    }
    public class BladderMinMargins
    {
        private BladderMinMarginInitializationParameters _startParameters;
        private double _currentSupMargin = 0; //mm
        private double _currentAntMargin = 0; //mm
        private double _totalSupMargin = 0; //mm
        private double _totalAntMargin = 0; //mm
        private double _runningSupMargin = 0; //mm
        private double _runningAntMargin = 0; //mm
        private double maxInternalMargin = 50; // mm
        private double supMarginIncrement = 1; // mm

        public BladderMinMargins()
        {
            _startParameters = new BladderMinMarginInitializationParameters
            {
                InitialSupMargin = 0,
                InitialAntMargin = 0,
                InfMarginSmoothingMargin = 25,
                SupMarginReductionIncrement = 1
            };
        }
        public bool IncrementMargins()
        {
            _currentAntMargin = Math.Ceiling((_currentSupMargin / 3));
            _currentSupMargin = _currentSupMargin + supMarginIncrement;
            bool maxInternalMarginReached = _currentSupMargin > maxInternalMargin;
            if (maxInternalMarginReached)
            {
                _currentSupMargin = _currentSupMargin % (maxInternalMargin + 1); // overflow back to 1 if max internal margin reached
                _totalSupMargin += _runningSupMargin; // add running margin to total margin
                _totalAntMargin += _runningAntMargin;
                _runningSupMargin = 0; // reset running margins
                _runningAntMargin = 0; 
            }
            _runningSupMargin = _currentSupMargin;
            _runningAntMargin = _currentAntMargin;
            return (maxInternalMarginReached);
        }
        public void SetStartParameters(BladderMinMarginInitializationParameters startParameters)
        {
            _startParameters = startParameters;
        }

        public BladderMinMarginResults GetCurrentMargins()
        {
            return new BladderMinMarginResults()
            {
                SupMargin = _currentSupMargin,
                AntMargin = _currentAntMargin,
                TotalSupMargin = _totalSupMargin + _runningSupMargin,
                TotalAntMargin = _totalAntMargin + _runningAntMargin,
                InfMarginSmoothingMargin = _startParameters.InfMarginSmoothingMargin
            };
        }

        public AxisAlignedMargins GenerateAxisAlignedInnerMargins(PatientOrientation o)
        {
            return Helpers.ConvertMargins(o, StructureMarginGeometry.Inner,  0, _currentAntMargin, 0, 0, 0, _currentSupMargin);
        }

        public AxisAlignedMargins GenerateAxisAlignedInfSmoothingMargins(PatientOrientation o)
        {
            return Helpers.ConvertMargins(o, StructureMarginGeometry.Outer, 0, 0, _startParameters.InfMarginSmoothingMargin, 0, 0, 0);
        }

    }


    public struct BladderMinCreationResults
    {
        public bool Success;
        public string Message;
        public ProtocolResult ProtocolResult;
        public BladderMinMarginResults MarginResult;
       
        public BladderMinCreationResults(bool success, string message, ProtocolResult protocolResult = new ProtocolResult(), BladderMinMarginResults marginResult = new BladderMinMarginResults())
        {
            Success = success;
            Message = message;
            MarginResult = marginResult;
            ProtocolResult = protocolResult;
        }

    }

    public class BladderminModel
    {
        //Define properties
        private EsapiWorker _ew;
        private BladderMinMarginInitializationParameters _marginParameters { get; set; }

        //BladderminModel constructor
        public BladderminModel(EsapiWorker ew)
        {
            _ew = ew;
            _marginParameters = new BladderMinMarginInitializationParameters
            {
                InitialSupMargin = 0,
                InitialAntMargin = 0,
                InfMarginSmoothingMargin = 25,
                SupMarginReductionIncrement = 1
            };
        }

        public async Task<BladderMinCreationResults> CreateBladderMinStructure(Protocol protocol, string bladderStructureId, string planSumId = "")
        {
            BladderMinCreationResults result = new BladderMinCreationResults(false, "Method did not complete");
            await Task.Run(() => _ew.AsyncRun((p, pl) =>
            {
                try
                {
                    //Get bladder structure.
                    Structure bladder = pl.StructureSet.Structures.FirstOrDefault(x => x.Id == bladderStructureId);
                    //Checks that the user selected a bladder contour.
                    if (bladder == null)
                    {
                        var errorMessage = "Could not find bladder contour.";
                        Helpers.SeriLog.AddError(errorMessage);
                        throw new Exception(errorMessage);
                    }
                    else if (bladder.IsEmpty)
                    {
                        var errorMessage = "Bladder structure is empty.";
                        Helpers.SeriLog.AddError(errorMessage);
                        throw new Exception(errorMessage);
                    }

                    //Creates a temporary high res bladder structure to ensure that the high res structure is being worked on.
                    string blaHiresName = "bladHiRes_AUTO";
                    Structure bladderHiRes = pl.StructureSet.Structures.FirstOrDefault(x => x.Id.Equals(blaHiresName, StringComparison.OrdinalIgnoreCase));
                    if (bladderHiRes != null)
                    {
                        string warningMessage = string.Format("'{0}' already exists. Please delete or rename structure before running the script again.", blaHiresName);
                        Helpers.SeriLog.AddError(warningMessage);
                    }
                    else
                    {
                        bladderHiRes = pl.StructureSet.AddStructure("CONTROL", blaHiresName);
                        bladderHiRes.SegmentVolume = bladder.SegmentVolume;
                        bladderHiRes.ConvertToHighResolution(); //Make a high res structure. Better clinically for DVH estimates close to PTV structures etc. 
                    }

                    //Creates a temporary step 2 bladder structure in situations where the margin reductions are larger > 50mm
                    string BladderMinTest = "Blad_AUTO_test";
                    Structure bladderMinTest = pl.StructureSet.Structures.FirstOrDefault(x => x.Id.Equals(BladderMinTest, StringComparison.OrdinalIgnoreCase));
                    if (bladderMinTest != null)
                    {
                        string warningMessage = $"{bladderMinTest} already exists. Clearing structure...";
                        Helpers.SeriLog.AddLog(warningMessage);
                    }
                    else
                    {
                        bladderMinTest = pl.StructureSet.AddStructure("CONTROL", BladderMinTest);
                        bladderMinTest.SegmentVolume = bladderHiRes.SegmentVolume; // automatically a high res structure due to conversion of bladderHiRes

                    }

                    //Create Bladdermin_AUTO structure.
                    string bladderMinName = "Bladmin_AUTO";
                    var bladderMin = pl.StructureSet.Structures.FirstOrDefault(x => x.Id == bladderMinName);
                    if (bladderMin != null)
                    {
                        string warningMessage = $"{bladderMinName} already exists. Clearing structure...";
                        Helpers.SeriLog.AddLog(warningMessage);
                    }
                    else
                    {
                        bladderMin = pl.StructureSet.AddStructure("CONTROL", bladderMinName);
                        bladderMin.ConvertToHighResolution();
                        bladderMin.SegmentVolume = bladderHiRes.SegmentVolume;
                    }


                    //------------------------------------------------------------------------------------------------------------------
                    // Get planning item from which to evaluate dose. This will be a sum for a multi phase protocol and a planSetup if not;
                    PlanningItem planningItem = null;
                    if (protocol.isMultiPhase)
                    {
                        planningItem = pl.Course.PlanSums.FirstOrDefault(x => x.Id.Equals(planSumId, StringComparison.CurrentCultureIgnoreCase));
                    }
                    else
                        planningItem = pl;

                    if (planningItem == null)
                    {
                        string errorMessage = "Could not find plan or plan sum.";
                        Helpers.SeriLog.AddLog(errorMessage);
                        throw new Exception(errorMessage);
                    }

                    //Define low dose isodose structure that is needed to ensure the bladdermin structure includes low dose in the bladder.
                    Structure lowDoseOverlap = CreateLowDoseIsoStructure(pl.TreatmentOrientation, planningItem, protocol, bladderHiRes);
                    
                    //------------------------------------------------------------------------------------------------------------------------
                    //Initialize variables for volume reduction loop.

                    double blaMinVol = bladderHiRes.Volume;

                    BladderMinMargins bladderMinMargins = new BladderMinMargins();

                    bool keepShrinking = true;
                    ProtocolResult lastAcceptableBladderMinResult = new ProtocolResult();
                    BladderMinMarginResults lastAcceptableMarginResult = new BladderMinMarginResults();

                    //---------------------------------------------------------------------------------------------------------------------------------
                    //Initiate volume reduction loop based on volume constraints at each iteration being met.

                    while (keepShrinking)
                    {
                        bladderMin.SegmentVolume = bladderMinTest.SegmentVolume; // Set bladdermin to result of previous succesful test;

                        bool internalMarginLimitReached = bladderMinMargins.IncrementMargins(); // increment margins and determine if internal margin limit has been reached.

                        if (internalMarginLimitReached)
                        {
                            Helpers.SeriLog.AddError("Margin reduction exceeds maximum. Rebasing bladder on existing bladderMin to continue reduction...");
                            bladderHiRes.SegmentVolume = bladderMinTest.SegmentVolume;

                        }

                        // Reduce the bladder volume by the latest margins;
                        ReduceBladderMin(pl.TreatmentOrientation, bladderMinTest, lowDoseOverlap, bladderHiRes, bladderMinMargins);

                        // Hack due to Eclipse bug, needed so GetVolumeAtDose works.
                        planningItem.GetDVHCumulativeData(bladderMinTest, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);

                        // Evaluate whether bladdermin meets protocol constraints. 
                        var protocolResult = protocol.EvaluateBladderMin(planningItem, bladderMinTest);
                        keepShrinking = protocolResult.IsMet;

                        if (keepShrinking) // include the last acceptable bladdermin reductions in the total margins
                        {
                            lastAcceptableBladderMinResult = protocolResult;
                            lastAcceptableMarginResult = bladderMinMargins.GetCurrentMargins();
                        }

                    }
                    //---------------------------------------------------------------------------------------------------------------
                    //Clean up temp structures that are no longer needed
                    pl.StructureSet.RemoveStructure(bladderHiRes);
                    pl.StructureSet.RemoveStructure(lowDoseOverlap);
                    pl.StructureSet.RemoveStructure(bladderMinTest);

                    result = new BladderMinCreationResults(true, "Complete", lastAcceptableBladderMinResult, lastAcceptableMarginResult);
                }
                catch (Exception ex)
                {
                    result = new BladderMinCreationResults(false, ex.Message);
                }
            }));
            return result;
        }

        public Structure CreateLowDoseIsoStructure(PatientOrientation orientation, PlanningItem doseSource, Protocol protocol, Structure bladder)
        {
            Structure lowDoseIso;
            string lowDoseIsoId = "lowDoseIso";
            lowDoseIso = doseSource.StructureSet.Structures.FirstOrDefault(x => x.Id.Equals(lowDoseIsoId, StringComparison.OrdinalIgnoreCase));
            if (lowDoseIso == null)
                lowDoseIso = doseSource.StructureSet.AddStructure("CONTROL", lowDoseIsoId);
            if (doseSource.DoseValuePresentation == DoseValuePresentation.Absolute)
                lowDoseIso.ConvertDoseLevelToStructure(doseSource.Dose, protocol.LowDoseConstraintValue);
            else
                lowDoseIso.ConvertDoseLevelToStructure(doseSource.Dose, new DoseValue(protocol.LowDoseConstraintValue / ((PlanSetup)doseSource).TotalDose * 100, DoseValue.DoseUnit.Percent));
            if (!lowDoseIso.IsHighResolution)
            {
                lowDoseIso.ConvertToHighResolution();
            }
            //smooth low dose structure
            var aaSmoothOut = Helpers.ConvertMargins(orientation, StructureMarginGeometry.Outer, 20, 20, 20, 20, 20, 20);
            var aaSmoothIn = Helpers.ConvertMargins(orientation, StructureMarginGeometry.Inner, 20, 20, 20, 20, 20, 20);

            lowDoseIso.SegmentVolume = lowDoseIso.AsymmetricMargin(aaSmoothOut).AsymmetricMargin(aaSmoothIn).And(bladder.SegmentVolume);

            return lowDoseIso;
        }

        private void ReduceBladderMin(PatientOrientation orientation, Structure bladderMin, Structure lowDoseOverlap, Structure currentBladder, BladderMinMargins margins)
        {
            //Get margins with respect to patient orientation and perform the volume reduction
            AxisAlignedMargins aaMargins = margins.GenerateAxisAlignedInnerMargins(orientation);

            bladderMin.SegmentVolume = currentBladder.SegmentVolume.AsymmetricMargin(aaMargins);

            //Expand an inf margin then cropped out from the bladder to allow a more clinically relevant bladdermin with less dosimetry post-processing
            AxisAlignedMargins smoothingMargins = margins.GenerateAxisAlignedInfSmoothingMargins(orientation);
            bladderMin.SegmentVolume = bladderMin.SegmentVolume.AsymmetricMargin(smoothingMargins);
            bladderMin.SegmentVolume = bladderMin.SegmentVolume.And(currentBladder);

            //Find overlap of bladder and low dose structure and then OR it with the bladdermin to add that back to the bladdermin structure. 
            bladderMin.SegmentVolume = bladderMin.SegmentVolume.Or(currentBladder.SegmentVolume.And(lowDoseOverlap));
        }

    }
}
