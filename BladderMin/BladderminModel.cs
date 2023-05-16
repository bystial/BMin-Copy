using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace BladderMin
{
    public class BladderminModel
    {
        //Define properties
        public List<string> protocolConstraintList;
        private List<string> _constraintValList;
        private double _blaMinVol;
        private double _supMargin;
        private double _antMargin;

        private bool _bladderMinDone = false;

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

        public async Task CreateBladderMinStructure()
        {
            await Task.Run(() => _ew.AsyncRun((p, pl) =>
            {
                //Get bladder structure.
                Structure bladder = pl.StructureSet.Structures.FirstOrDefault(x => x.Id == _bladderStructure);
                //Checks that the user selected a bladder contour.
                if (bladder == null)
                {
                    Helpers.SeriLog.AddError("Could not find bladder contour.");
                    throw new Exception("Bladder structure cannot be null!");
                }

                //Creates a temporary high res bladder structure to ensure that the high res structure is being worked on.
                string blaHiresName = "bladHiRes_AUTO";
                if (Helpers.CheckStructureExists(pl, blaHiresName))
                {
                    string errorMessage = string.Format("'{0}' already exists. Please delete or rename structure before running the script again.", blaHiresName);
                    Helpers.SeriLog.AddError(errorMessage);
                    throw new Exception(errorMessage);
                }
                Structure bladderHiRes = pl.StructureSet.AddStructure("CONTROL", blaHiresName);
                bladderHiRes.SegmentVolume = bladder.SegmentVolume;
                bladderHiRes.ConvertToHighResolution(); //Make a high res structure. Better clinically for DVH estimates close to PTV structures etc. 

                //Create Bladdermin_AUTO structure.
                string bladderMinName = "Bladmin_AUTO";
                if (Helpers.CheckStructureExists(pl, bladderMinName))
                {
                    string errorMessage = string.Format("'{0}' already exists. Please delete or rename strcuture before running the script again.", bladderMinName);
                    Helpers.SeriLog.AddError(errorMessage);
                    throw new Exception(errorMessage);
                }
                Structure bladderMin = pl.StructureSet.AddStructure("CONTROL", bladderMinName);
                bladderMin.ConvertToHighResolution();
                bladderMin.SegmentVolume = bladderHiRes.SegmentVolume;

                //------------------------------------------------------------------------------------------------------------------
                //Find the plan sum if there is one.
                PlanSum planSum = pl.Course.PlanSums.FirstOrDefault(x => x.Id.Equals(_planSumId, StringComparison.CurrentCultureIgnoreCase));

                //Define low dose isodose structure that is needed to ensure the bladdermin structure includes low dose in the bladder.
                Structure lowDoseIso = Helpers.CreateLowDoseIsoStructure(pl, _protocol, planSum);

                //------------------------------------------------------------------------------------------------------------------------
                //Define margins and constraints (so you can call them outside the loop if needed).
                double supMargin = 0; //in mm
                double antMargin = 0;
                double infMargin = 25;

                //Define placeholders for bladdermin constraints.
                double blaMinVol = 0;
                double blaMinVHigh = 0;
                double blaMinVInt = 0;
                double blaMinVLow = 0;

                List<string> blaMinConstraints = new List<string>() { };
                List<string> marginsValues = new List<string>() { };

                //---------------------------------------------------------------------------------------------------------------------------------
                //Initiate volume reduction loop based on volume constraints at each iteration being met.
                bool constraintsMet = true;

                while (constraintsMet)
                {
                    antMargin = Math.Ceiling((supMargin / 3));
                    //Get margins with respect to patient orientation and perform the volume reduction
                    AxisAlignedMargins margins = Helpers.ConvertMargins(pl.TreatmentOrientation, 0, antMargin, 0, 0, 0, supMargin);
                    bladderMin.SegmentVolume = bladderHiRes.SegmentVolume.AsymmetricMargin(margins);

                    //Find overlap of bladder and low dose structure and then OR it with the bladdermin to add that back to the bladdermin structure. 
                    bladderMin.SegmentVolume = bladderMin.SegmentVolume.Or(bladderHiRes.SegmentVolume.And(lowDoseIso));

                    //Expand an inf margin then cropped out from the bladder to allow a more clinically relevant bladdermin with less dosimetry post-processing
                    AxisAlignedMargins infMargins = new AxisAlignedMargins(StructureMarginGeometry.Outer, 0, 0, infMargin, 0, 0, 0);
                    bladderMin.SegmentVolume = bladderMin.SegmentVolume.AsymmetricMargin(infMargins);
                    bladderMin.SegmentVolume = bladderMin.SegmentVolume.And(bladderHiRes);

                    //Get bladdermin volume.
                    blaMinVol = bladderMin.Volume;

                    //Get DVH values for bladdermin
                    GetDVHValues(pl, planSum, bladderMin, _protocol, out blaMinVHigh, out blaMinVInt, out blaMinVLow);

                    //Compare bladdermin constraint values to bladder constraints.
                    if (_protocol.Name == "Prostate 70 Gy in 28#" || _protocol.Name == "Prostate 78 Gy in 39# (2 phase)")
                    {
                        if (blaMinVLow > _protocol.vLow || blaMinVHigh > _protocol.vHigh || blaMinVol < 80)
                        {
                            constraintsMet = false;
                            if (supMargin != 0)
                            {
                                supMargin -= 1; //Sets up to take the previous iteration that passed before failing constraints.
                            }
                            antMargin = Math.Ceiling((supMargin / 3));

                            margins = Helpers.ConvertMargins(pl.TreatmentOrientation, 0, antMargin, 0, 0, 0, supMargin);
                            bladderMin.SegmentVolume = bladderHiRes.SegmentVolume.AsymmetricMargin(margins);

                            //Find overlap of bladder and low dose isodose structure and then boolen operator "OR" it with the bladdermin to add that back to the bladdermin structure. 
                            bladderMin.SegmentVolume = bladderMin.SegmentVolume.Or(bladderHiRes.SegmentVolume.And(lowDoseIso));

                            //Expand an inf margin then cropped out from the bladder to allow a more clinically relevant bladdermin with less dosimetry post-processing
                            bladderMin.SegmentVolume = bladderMin.SegmentVolume.AsymmetricMargin(infMargins);
                            bladderMin.SegmentVolume = bladderMin.SegmentVolume.And(bladderHiRes);

                            //Get bladdermin volume.
                            blaMinVol = bladderMin.Volume;

                            //Get DVH values for bladdermin
                            GetDVHValues(pl, planSum, bladderMin, _protocol, out blaMinVHigh, out blaMinVInt, out blaMinVLow);

                            //Summarize constraints and volume to the GUI.
                            blaMinConstraints.Add(blaMinVHigh.ToString("#.0"));
                            blaMinConstraints.Add(blaMinVLow.ToString("#.0"));

                            _constraintValList = blaMinConstraints;
                            _blaMinVol = blaMinVol;
                            _supMargin = supMargin;
                            _antMargin = antMargin;
                        }
                    }
                    if (_protocol.Name == "Prostate 60 Gy in 20#" || _protocol.Name == "Prostate SABR 36.25 Gy in 5#")
                    {
                        if (blaMinVLow > _protocol.vLow || blaMinVInt > _protocol.vInt || blaMinVHigh > _protocol.vHigh || blaMinVol < 80)
                        {
                            constraintsMet = false;
                            if (supMargin != 0)
                            {
                                supMargin -= 1; //Sets up to take the previous iteration that passed before failing constraints.
                            }
                            antMargin = Math.Ceiling((supMargin / 3));

                            margins = Helpers.ConvertMargins(pl.TreatmentOrientation, 0, antMargin, 0, 0, 0, supMargin);
                            bladderMin.SegmentVolume = bladderHiRes.SegmentVolume.AsymmetricMargin(margins);

                            //Find overlap of bladder and low dose isodose structure and then boolen operator "OR" it with the bladdermin to add that back to the bladdermin structure. 
                            bladderMin.SegmentVolume = bladderMin.SegmentVolume.Or(bladderHiRes.SegmentVolume.And(lowDoseIso));

                            //Expand an inf margin then cropped out from the bladder to allow a more clinically relevant bladdermin with less dosimetry post-processing
                            bladderMin.SegmentVolume = bladderMin.SegmentVolume.AsymmetricMargin(infMargins);
                            bladderMin.SegmentVolume = bladderMin.SegmentVolume.And(bladderHiRes);

                            //Get bladdermin volume.
                            blaMinVol = bladderMin.Volume;

                            //Get DVH values for bladdermin
                            GetDVHValues(pl, planSum, bladderMin, _protocol, out blaMinVHigh, out blaMinVInt, out blaMinVLow);

                            //Summarize constraints and volume to the GUI.
                            blaMinConstraints.Add(blaMinVHigh.ToString("#.0"));
                            blaMinConstraints.Add(blaMinVInt.ToString("#.0"));
                            blaMinConstraints.Add(blaMinVLow.ToString("#.0"));

                            _constraintValList = blaMinConstraints;
                            _blaMinVol = blaMinVol;
                            _supMargin = supMargin;
                            _antMargin = antMargin;
                        }
                    }
                    supMargin += 1;

                }

                //---------------------------------------------------------------------------------------------------------------
                //Clean up temp structures that are no longer needed
                pl.StructureSet.RemoveStructure(bladderHiRes);
                pl.StructureSet.RemoveStructure(lowDoseIso);

                _bladderMinDone = true;
            }));
        }

        //A tuple method to store the results from the CreateBladderMinStructure method.
        public Tuple<List<string>, double, double, double> GetResults()
        {
            if (_bladderMinDone)
            {
                return new Tuple<List<string>, double, double, double>(_constraintValList, _blaMinVol, _supMargin, _antMargin);
            }
            else return null;
        }

        //Gets DVH values based on either a plan or a plan sum
        public static void GetDVHValues(PlanSetup pl, PlanSum planSum, Structure bladderMin, Protocol protocol, out double blaMinVHigh, out double blaMinVInt, out double blaMinVLow)
        {
            if (planSum != null)
            {
                planSum.GetDVHCumulativeData(bladderMin, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);
                blaMinVHigh = planSum.GetVolumeAtDose(bladderMin, protocol.dHigh, VolumePresentation.Relative);
                blaMinVInt = planSum.GetVolumeAtDose(bladderMin, protocol.dInt, VolumePresentation.Relative);
                blaMinVLow = planSum.GetVolumeAtDose(bladderMin, protocol.dLow, VolumePresentation.Relative);
            }
            else
            {
                pl.GetDVHCumulativeData(bladderMin, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);
                blaMinVHigh = pl.GetVolumeAtDose(bladderMin, protocol.dHigh, VolumePresentation.Relative);
                blaMinVInt = pl.GetVolumeAtDose(bladderMin, protocol.dInt, VolumePresentation.Relative);
                blaMinVLow = pl.GetVolumeAtDose(bladderMin, protocol.dLow, VolumePresentation.Relative);
            }
        }
    }
}
