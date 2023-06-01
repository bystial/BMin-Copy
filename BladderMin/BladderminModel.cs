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

    public class BladderMinSearchParameters
    {
        public double InitialSupMargin;
        public double InitialAntMargin;
        public double InitialInfMargin;
        public double SupMarginIncrement;
    }

    public struct BladderMinCreationResults
    {
        public bool Success;
        public string Message;
        public ProtocolResult ProtocolResult;
        public double SupMargin;
        public double AntMargin;
        public double BladderMinVol;
        public BladderMinCreationResults(bool success, string message, double volume = double.NaN, double supMargin = double.NaN, double antMargin = double.NaN, ProtocolResult protocolResult = new ProtocolResult())
        {
            Success = success;
            Message = message;
            SupMargin = supMargin;
            AntMargin = antMargin;
            ProtocolResult = protocolResult;
            BladderMinVol = volume;
        }

    }

    public class BladderminModel
    {
        //Define properties
        public List<BladderConstraint> protocolConstraintList;
        private List<string> _constraintValList;
        private double _blaMinVol;
        private double _supMargin;
        private double _antMargin;

        private bool _bigMargin = false;

        private Protocol _protocol;
        private string _bladderStructure;
        private string _planSumId;
        private EsapiWorker _ew;

        public List<string> blaMinConstraints = new List<string>() { };
        public List<string> marginsValues = new List<string>() { };

        //BladderminModel constructor
        public BladderminModel(EsapiWorker ew, Protocol bladderMinProtocol, string bladderStructure, string planSumId = "")
        {
            _ew = ew;
            _protocol = bladderMinProtocol;
            _bladderStructure = bladderStructure;
            _planSumId = planSumId;

            protocolConstraintList = _protocol.ProtocolConstraints;
        }

        public async Task<BladderMinCreationResults> CreateBladderMinStructure()
        {
            BladderMinCreationResults result = new BladderMinCreationResults(false, "Method did not complete");
            await Task.Run(() => _ew.AsyncRun((p, pl) =>
            {
                try
                {
                    //Get bladder structure.
                    Structure bladder = pl.StructureSet.Structures.FirstOrDefault(x => x.Id == _bladderStructure);
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
                    string prevBladderMin = "Blad_AUTO_prev";
                    Structure bladderMinPrev = pl.StructureSet.Structures.FirstOrDefault(x => x.Id.Equals(prevBladderMin, StringComparison.OrdinalIgnoreCase));
                    if (bladderMinPrev != null)
                    {
                        string warningMessage = $"{bladderMinPrev} already exists. Clearing structure...";
                        Helpers.SeriLog.AddLog(warningMessage);
                    }
                    else
                    {
                        bladderMinPrev = pl.StructureSet.AddStructure("CONTROL", prevBladderMin);
                        bladderMinPrev.SegmentVolume = bladderHiRes.SegmentVolume; // automatically a high res structure due to conversion of bladderHiRes
                        
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
                    PlanningItem doseSource = null;
                    if (_protocol.isMultiPhase)
                        doseSource = pl.Course.PlanSums.FirstOrDefault(x => x.Id.Equals(_planSumId, StringComparison.CurrentCultureIgnoreCase));
                    else
                        doseSource = pl;

                    if (doseSource == null) 
                    {
                        string errorMessage = "Could not find plan or plan sum.";
                        Helpers.SeriLog.AddLog(errorMessage);
                        throw new Exception(errorMessage);
                    }

                    //Define low dose isodose structure that is needed to ensure the bladdermin structure includes low dose in the bladder.
                    Structure lowDoseOverlap = Helpers.CreateLowDoseIsoStructure(doseSource, _protocol);
                    lowDoseOverlap.SegmentVolume = bladderHiRes.SegmentVolume.And(lowDoseOverlap.SegmentVolume);


                    //------------------------------------------------------------------------------------------------------------------------
                    //Define initial margins
                    double supMargin = 0; //in mm
                    double antMargin = 0;

                    double supMarginIncrement = 1;

                    double TotalSupMargin = 0;
                    double TotalAntMargin = 0;
                    double RunningSupMargin = 0;
                    double RunningAntMargin = 0;


                    //Define placeholders for bladdermin constraints.
                    double blaMinVol = bladderHiRes.Volume;
                   
                    bool updateSourceStructure = false;
                    bool keepShrinking = true;
                    ProtocolResult lastAcceptableBladderMinResult = new ProtocolResult();
                    _blaMinVol = blaMinVol;
                    //---------------------------------------------------------------------------------------------------------------------------------
                    //Initiate volume reduction loop based on volume constraints at each iteration being met.

                    while (keepShrinking)
                    {
                        bladderMinPrev.SegmentVolume = bladderMin.SegmentVolume; // Set previous bladdermin to current bladdermin in case this iteration shrinks the bladdermin structure too much.
                        (antMargin, supMargin, updateSourceStructure) = IncrementMargins(supMargin, supMarginIncrement);

                        //If sup margins have reached internal margin limit, set source bladderHiRes structure to bladdermin structure and shrink from here
                        if (updateSourceStructure)
                        {
                            bladderHiRes.SegmentVolume = bladderMin.SegmentVolume;
                            TotalAntMargin += RunningAntMargin;
                            TotalSupMargin += RunningSupMargin;
                        }

                        //Get margins with respect to patient orientation and perform the volume reduction
                        AxisAlignedMargins margins = Helpers.ConvertInnerMargins(pl.TreatmentOrientation, 0, antMargin, 0, 0, 0, supMargin);

                        // Reduce the bladder volume by the new margins;
                        ReduceBladderMin(pl.TreatmentOrientation, bladderMin, lowDoseOverlap, bladderHiRes, margins);

                        //Get bladdermin volume.
                        blaMinVol = bladderMin.Volume;

                        // Hack due to Eclipse bug, needed so GetVolumeAtDose works.
                        doseSource.GetDVHCumulativeData(bladderMin, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);

                        var protocolStatus = _protocol.EvaluateBladderMin(doseSource, bladderMin);
                        keepShrinking = protocolStatus.IsMet && blaMinVol > 80;

                        if (keepShrinking)
                        {
                            RunningSupMargin = supMargin;
                            RunningAntMargin = antMargin;
                            lastAcceptableBladderMinResult = protocolStatus;
                            _blaMinVol = blaMinVol;
                        }
                        else
                        {
                            TotalAntMargin += RunningAntMargin;
                            TotalSupMargin += RunningSupMargin;
                        }
                    }
                    //---------------------------------------------------------------------------------------------------------------
                    //Clean up temp structures that are no longer needed
                    pl.StructureSet.RemoveStructure(bladderHiRes);
                    pl.StructureSet.RemoveStructure(lowDoseOverlap);
                    pl.StructureSet.RemoveStructure(bladderMinPrev);

                    result = new BladderMinCreationResults(true, "Complete", _blaMinVol, TotalSupMargin, TotalAntMargin, lastAcceptableBladderMinResult);
                }
                catch (Exception ex)
                {
                    result = new BladderMinCreationResults(false, ex.Message);
                }
            }));
            return result;
        }

        private (double antMargin, double supMargin, bool updateSourceStructure) IncrementMargins(double supMargin, double supMarginIncrement)
        {
            double antMargin = Math.Ceiling((supMargin / 3));
            supMargin = supMargin + supMarginIncrement;
            double newSupMargin = supMargin % 51; // 50 is the max sup margin allowed.
            bool updateSourceStructure = supMargin > 50;
            if (updateSourceStructure)
            {
                Helpers.SeriLog.AddError("Margin reduction exceeds 5cm. Rebasing bladder on existing bladderMin to continue reduction...");
            }
            return (antMargin, newSupMargin, updateSourceStructure);
        }

        private void ReduceBladderMin(PatientOrientation orientation, Structure bladderMin, Structure lowDoseOverlap, Structure currentBladder, AxisAlignedMargins margins)
        {
            double infMargin = 25;

            bladderMin.SegmentVolume = currentBladder.SegmentVolume.AsymmetricMargin(margins);

            //Find overlap of bladder and low dose structure and then OR it with the bladdermin to add that back to the bladdermin structure. 
            bladderMin.SegmentVolume = bladderMin.SegmentVolume.Or(currentBladder.SegmentVolume.And(lowDoseOverlap));

            //Expand an inf margin then cropped out from the bladder to allow a more clinically relevant bladdermin with less dosimetry post-processing
            AxisAlignedMargins infMargins = Helpers.ConvertOuterMargins(orientation, 0, 0, infMargin, 0, 0, 0);
            bladderMin.SegmentVolume = bladderMin.SegmentVolume.AsymmetricMargin(infMargins);
            bladderMin.SegmentVolume = bladderMin.SegmentVolume.And(currentBladder);
        }

    }
}
