using BladderMin;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using static VMS.TPS.Helpers;

[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{

    [AddINotifyPropertyChangedInterface]

    public class ViewModel : ObservableObject
    {

        //Initialize some variables to hold the Eclipse related data that will be bound to the UI.
        public string StructureSetId { get; set; }
        public string PlanId { get; set; }
        public ObservableCollection<string> PlanSumList { get; private set; } = new ObservableCollection<string>(); //{"DesignPlan1", "DesignPlan2" };
        public ObservableCollection<string> StructureList { get; private set; } = new ObservableCollection<string>(); //{ "Design1", "Design2", "Design3" };
        public string BladderContour { get; set; } = "Bladder";
        public List<Protocol> ProtocolList { get; private set; } = new List<Protocol>();

        public List<string> ConstraintList { get; private set; } = new List<string>(); // { "DesignConstraint1", "DesignConstraint2", "DesignConstraint3" };
        public List<string> ConstraintValList { get; private set; } = new List<string>(); // { "DesignConstraintVal","DesignConstraintVal2", "DesignConstraintVal3"};

        private Protocol _selectedProtocol;
        public Protocol SelectedProtocol
        {
            get
            {
                return _selectedProtocol;
            }
            set
            {
                _selectedProtocol = value;
                RaisePropertyChangedEvent(nameof(PlanSumSelectionVisibility));
                RaisePropertyChangedEvent(nameof(NodalSelectionVisibility));
            }
        }

        public Visibility NodalSelectionVisibility
        {
            get
            {
                if (SelectedProtocol != null)
                {
                    if (SelectedProtocol.isNodesTreatable)
                        return Visibility.Visible;
                    else
                        return Visibility.Collapsed;
                }
                else
                    return Visibility.Visible;
            }
        }

    public Visibility PlanSumSelectionVisibility
        {
            get
            {
                if (SelectedProtocol != null)
                {
                    if (SelectedProtocol.isMultiPhase)
                        return Visibility.Visible;
                    else
                        return Visibility.Collapsed;
                }
                else
                    return Visibility.Collapsed;
            }
        }
        public string SelectedPlanSum { get; set; }
        public string BlaMinVol { get; set; }
        public string SupMargin { get; set; }
        public string AntMargin { get; set; }

        //Variables for UI related bindings
        public string StatusMessage { get; set; } //= "Design Time";
        public bool ScriptWorking { get; set; } = false;
        public bool ButtonEnabled { get; set; } = true;

        public SolidColorBrush StatusColour { get; set; }
        public SolidColorBrush WarningColour = new SolidColorBrush(Colors.Goldenrod);
        public SolidColorBrush ScriptDoneColour = new SolidColorBrush(Colors.PaleGreen);

        public ViewModel()
        {

        }
        public EsapiWorker ew = null;

        //Starts the EsapiWorker.
        public ViewModel(EsapiWorker _ew = null)
        {
            ew = _ew;
            Initialize();
        }

        //Initializes the thread that runs from Eclipse.
        private async void Initialize()
        {
            try
            {
                string currentStructureSetId = "";
                string currentPlanId = "";
                List<string> plans = new List<string>();
                List<string> planSums = new List<string>();
                List<string> structures = new List<string>();
                ObservableCollection<string> structureList = new ObservableCollection<string>();
                ObservableCollection<string> planSumList = new ObservableCollection<string>();
                List<BladderMinProtocolTypes> protocolOptions = new List<BladderMinProtocolTypes>()
                {
                    BladderMinProtocolTypes.Prostate60in20,
                    BladderMinProtocolTypes.Prostate70in28,
                    BladderMinProtocolTypes.Prostate78in39,
                    BladderMinProtocolTypes.ProstateSABR
                };
                foreach (var name in protocolOptions)
                {
                    ProtocolList.Add(new Protocol(name, false));
                }

                bool Done = await Task.Run(() => ew.AsyncRun((p, pl) =>
                {
                    p.BeginModifications();
                    //Get basic patient information and initialize drop down menu variables.
                    currentStructureSetId = pl.StructureSet.Id;
                    currentPlanId = pl.Id;
                    plans = pl.Course.PlanSetups.Select(x => x.Id).ToList();
                    planSums = pl.Course.PlanSums.Select(x => x.Id).ToList();
                    structures = pl.StructureSet.Structures.Select(x => x.Id).ToList();
                    foreach (string structureId in structures)
                    {
                        structureList.Add(structureId);
                    }
                    foreach (string planSumId in planSums)
                    {
                        planSumList.Add(planSumId);
                    }

                }
                ));
                if (Done)
                {
                    StructureSetId = currentStructureSetId;
                    PlanSumList = planSumList;
                    PlanId = currentPlanId;
                    StructureList = structureList;
                    //ProtocolList = protocols;
                    ScriptWorking = false;
                    if (plans == null)
                    {
                        StatusMessage = string.Format("There is no plan. Ensure a plan is in scope before running script.");
                        StatusColour = WarningColour;
                        ButtonEnabled = false;
                        ScriptWorking = false;
                        Helpers.SeriLog.AddError("There is no plan. Ensure a plan is in scope before running script.");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Helpers.SeriLog.AddError(string.Format("{0}\r\n{1}\r\n{2}", ex.Message, ex.InnerException, ex.StackTrace));
                MessageBox.Show(string.Format("{0}\r\n{1}\r\n{2}", ex.Message, ex.InnerException, ex.StackTrace));
            }
        }

        public ICommand StartCommand
        {
            get
            {
                return new DelegateCommand(Start);
            }
        }

        public async void Start(object param = null)
        {
            Helpers.SeriLog.AddLog("BladderMin application started.");
            ButtonEnabled = false;
            ScriptWorking = true;

            // Clear status message
            StatusMessage = "Running...";
            StatusColour = new SolidColorBrush(Colors.Transparent);

            await Task.Run(() => ew.AsyncRun((p, pl) =>
            {
                //Get bladder structure.
                Structure bladder = pl.StructureSet.Structures.FirstOrDefault(x => x.Id == BladderContour);
                //Checks that the user selected a bladder contour.
                if (bladder == null)
                {
                    Helpers.SeriLog.AddError("Could not find bladder contour.");
                    StatusMessage = string.Format("Bladder contour not found! Please select a Bladder contour.");
                    StatusColour = WarningColour;
                    ButtonEnabled = true;
                    ScriptWorking = false;
                    return;
                }

                //Bladder high res structure to ensure that the high res structure is being worked on.
                string blaHiresName = "bladHiRes_AUTO";
                if (Helpers.CheckStructureExists(pl, blaHiresName))
                {
                    StatusMessage = string.Format("'{0}' already exists. Please delete or rename strcuture before running the script again.", blaHiresName);
                    Helpers.SeriLog.AddError("Temp structure already exists. Please delete or rename structure before running the script again");
                    StatusColour = WarningColour;
                    ButtonEnabled = false;
                    ScriptWorking = false;
                    return;
                }
                Structure bladderHiRes = pl.StructureSet.AddStructure("CONTROL", blaHiresName);
                bladderHiRes.SegmentVolume = bladder.SegmentVolume;
                bladderHiRes.ConvertToHighResolution(); //Make a high res structure. Better clinically for DVH estimates close to PTV structures etc. 

                //Create Bladdermin_AUTO structure.
                string bladderMinName = "Bladdermin_AUTO";
                if (Helpers.CheckStructureExists(pl, bladderMinName))
                {
                    StatusMessage = string.Format("'{0}' already exists. Please delete or rename strcuture before running the script again.", bladderMinName);
                    Helpers.SeriLog.AddError("Bladdermin_AUTO structure already exists. Please delete or rename structure before running the script again");
                    StatusColour = WarningColour;
                    ButtonEnabled = false;
                    ScriptWorking = false;
                    return;
                }
                Structure bladderMin = pl.StructureSet.AddStructure("CONTROL", bladderMinName);
                bladderMin.ConvertToHighResolution();
                bladderMin.SegmentVolume = bladderHiRes.SegmentVolume;

                // List<string> protocolConstraints = new List<string>();
                Protocol protocol = SelectedProtocol;
                ConstraintList = protocol.ProtocolConstraints;

                //------------------------------------------------------------------------------------------------------------------
                //Create and define low dose isodose structure
                Structure lowDoseIso = null;

                //Find the plan sum if there is one.
                PlanSum planSum = pl.Course.PlanSums.FirstOrDefault(x => x.Id == SelectedPlanSum);

                //Define low dose isodose structure
                lowDoseIso = Helpers.CreateLowDoseIsoStructure(pl, protocol.dLowIso, protocol.dLow, planSum);

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
                    Helpers.GetDVHValues(pl, planSum, bladderMin, protocol.dHigh, protocol.dInt, protocol.dLow, out blaMinVHigh, out blaMinVInt, out blaMinVLow);

                    //Compare bladdermin constraint values to bladder constraints.
                    if (SelectedProtocol.Name == "Prostate 70 Gy in 28#" || SelectedProtocol.Name == "Prostate 78 Gy in 39# 2-phase")
                    {
                        if (blaMinVLow > protocol.vLow || blaMinVHigh > protocol.vHigh || blaMinVol < 80)
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
                            Helpers.GetDVHValues(pl, planSum, bladderMin, protocol.dHigh, protocol.dInt, protocol.dLow, out blaMinVHigh, out blaMinVInt, out blaMinVLow);

                            //Summarize constraints and volume to the GUI.
                            blaMinConstraints.Add(blaMinVHigh.ToString("#.0"));
                            blaMinConstraints.Add(blaMinVLow.ToString("#.0"));

                            ConstraintValList = blaMinConstraints;
                            BlaMinVol = blaMinVol.ToString("#.00");
                            SupMargin = supMargin.ToString();
                            AntMargin = antMargin.ToString();
                        }
                    }
                    if (SelectedProtocol.Name == "Prostate 60 Gy in 20#" || SelectedProtocol.Name == "Prostate SABR 36.25 Gy in 5#")
                    {
                        if (blaMinVLow > protocol.vLow || blaMinVInt > protocol.vInt || blaMinVHigh > protocol.vHigh || blaMinVol < 80)
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
                            Helpers.GetDVHValues(pl, planSum, bladderMin, protocol.dHigh, protocol.dInt, protocol.dLow, out blaMinVHigh, out blaMinVInt, out blaMinVLow);

                            //Summarize constraints and volume to the GUI.
                            blaMinConstraints.Add(blaMinVHigh.ToString("#.0"));
                            blaMinConstraints.Add(blaMinVInt.ToString("#.0"));
                            blaMinConstraints.Add(blaMinVLow.ToString("#.0"));

                            ConstraintValList = blaMinConstraints;
                            BlaMinVol = blaMinVol.ToString("#.00");
                            SupMargin = supMargin.ToString();
                            AntMargin = antMargin.ToString();
                        }
                    }
                    supMargin += 1;

                }


                //---------------------------------------------------------------------------------------------------------------
                //Clean up temp structures that are no longer needed
                pl.StructureSet.RemoveStructure(bladderHiRes);
                pl.StructureSet.RemoveStructure(lowDoseIso);

                //Closing statements
                ScriptWorking = false;
                StatusMessage = string.Format("Bladdermin structure created.");
                StatusColour = ScriptDoneColour;
                //Message box used for testing purposes only.
                //string message = string.Format("VHigh is {0}, VInt is {1}, VLow is {2}. Margins are: sup {3}mm and ant {4}mm. Volume is {5}cc", blaMinVHigh, blaMinVInt, blaMinVLow, supMargin - 1, antMargin, blaMinVol);
                //MessageBox.Show(message);
                ButtonEnabled = false;
                Helpers.SeriLog.AddLog("BladderMin application finished. Bladdermin structure created successfully.");
            }));
        }
    }
}
