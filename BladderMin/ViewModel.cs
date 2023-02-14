using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        protected void RaisePropertyChangedEvent([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get
            {
                return _isSelected;
            }
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                {
                    _isSelected = value;
                }
            }
        }
    }

    [AddINotifyPropertyChangedInterface]

    public class ViewModel : ObservableObject
    {

        //Initialize some variables to hold the Eclipse related data that will be bound to the UI.
        public string PatientId { get; set; }
        public string StructureSetId { get; set; }
        public ObservableCollection<string> StructureList { get; private set; } = new ObservableCollection<string>() { "Design1", "Design2", "Design3" };
        public string BladderContour { get; set; } = "Design1";
        public List<string> ProtocolList { get; private set; } = new List<string>();

        public List<string> ConstraintList { get; private set; } = new List<string>() { "DesignConstraint1", "DesignConstraint2" };
        public string SelectedProtocol { get; set; }

        //Variables for UI related bindings
        public string StatusMessage { get; set; } = "Design Time";
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
                string currentPatientId = "";
                string currentStructureSetId = "";

                List<string> structures = new List<string>();
                ObservableCollection<string> structureList = new ObservableCollection<string>();
                List<string> protocols = new List<string>()
                {
                    {"Prostate 60 Gy in 20#" },
                    {"Prostate 70 Gy in 28#" },
                    {"Prostate 76 Gy in 38# 3-phase" },
                    {"Prostate 78 Gy in 39# 2-phase" },
                    {"Prostate SABR 36.25 Gy in 5#" }
                };

                bool Done = await Task.Run(() => ew.AsyncRun((p, pl) =>
                {
                    p.BeginModifications();
                    //Get basic patient information and initialize drop down menu variables.
                    currentPatientId = p.Id;
                    currentStructureSetId = pl.StructureSet.Id;
                    structures = pl.StructureSet.Structures.Select(x => x.Id).ToList();
                    foreach (string structureId in structures)
                    {
                        structureList.Add(structureId);
                    }
                }
                ));
                if (Done)
                {
                    PatientId = currentPatientId;
                    StructureSetId = currentStructureSetId;
                    StructureList = structureList;
                    ProtocolList = protocols;
                    ScriptWorking = false;
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
            Helpers.Serilog.AddLog("BladderMin application started.");
            ButtonEnabled = false;
            ScriptWorking = true;

            // Clear status message
            StatusMessage = "Running...";
            StatusColour = new SolidColorBrush(Colors.Transparent);


            await Task.Run(() => ew.AsyncRun((p, pl) =>
            {
                //Get bladder structure.
                Structure bladder = pl.StructureSet.Structures.FirstOrDefault(x => x.Id == BladderContour);
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
                Structure bladderHiRes = pl.StructureSet.AddStructure("CONTROL", "bladHiRes_AUTO");
                bladderHiRes.SegmentVolume = bladder.SegmentVolume;
                bladderHiRes.ConvertToHighResolution(); //Make a high res structure. Better clinically for DVH estimates close to PTV structures etc. 

                //Create Bladdermin_AUTO structure.
                string bladderMinName = "Bladdermin_AUTO";
                Structure bladderMin = pl.StructureSet.AddStructure("CONTROL", bladderMinName);
                bladderMin.ConvertToHighResolution(); //Make a high res structure. Better clinically for DVH estimates close to PTV structures etc.
                bladderMin.SegmentVolume = bladderHiRes.SegmentVolume;


                //-------------------------------------------------------------------------------------------------------------
                //Define the 3 level volume dose constraints based on bladder constraints
                double vHigh = 25; //Needs to be written as an actual % (ie. 1% not 0.01)
                double vInt = 50;
                double vLow = 50;

                Structure lowDoseIso = null;
                //Define low dose isodose structure
                if (pl.Course.PlanSums != null)
                {
                    PlanSum planSum = pl.Course.PlanSums.FirstOrDefault();
                    lowDoseIso = planSum.StructureSet.AddStructure("CONTROL", "lowDoseIso");
                    lowDoseIso.ConvertDoseLevelToStructure(planSum.Dose, new DoseValue(6000, DoseValue.DoseUnit.cGy)); //Update when dose constraint changed.
                    lowDoseIso.ConvertToHighResolution();
                }
                else
                {
                    lowDoseIso = pl.StructureSet.AddStructure("CONTROL", "lowDoseIso");
                    lowDoseIso.ConvertDoseLevelToStructure(pl.Dose, new DoseValue(64.1, DoseValue.DoseUnit.Percent)); //Update when dose constraint changed.
                    lowDoseIso.ConvertToHighResolution();
                }


                //Define DoseValues for the constraints
                DoseValue dHigh = new DoseValue(7000, DoseValue.DoseUnit.cGy);
                DoseValue dInt = new DoseValue(6000, DoseValue.DoseUnit.cGy);
                DoseValue dLow = new DoseValue(5000, DoseValue.DoseUnit.cGy);

                //Define margins and constraints (so you can call them outside the loop if needed).
                double supMargin = 0; //in mm
                double antMargin = 0;

                //Can remove once testing complete and no need to report back on values for testing.
                double blaMinVol = 0;
                double blaMinVHigh = 0;
                double blaMinVInt = 0;
                double blaMinVLow = 0;


               //---------------------------------------------------------------------------------------------------------------------------------
               //Initiate volume reduction loop based on volume constraints at each iteration being met.
               bool constraintsMet = true;

               while(constraintsMet)
               {
                    antMargin = Math.Ceiling((supMargin / 3)); 
                    //Get margins with respect to patient orientation and perform the volume reduction
                    AxisAlignedMargins margins = Helpers.ConvertMargins(pl.TreatmentOrientation, 0, antMargin, 0, 0, 0, supMargin);
                    bladderMin.SegmentVolume = bladderHiRes.SegmentVolume.AsymmetricMargin(margins);

                    //Find overlap of bladder and low dose structure and then OR it with the bladdermin to add that back to the bladdermin structure. 
                    bladderMin.SegmentVolume = bladderMin.SegmentVolume.Or(bladderHiRes.SegmentVolume.And(lowDoseIso));

                    //Get Bladdermin volume. Can remove later if testing passes.
                    blaMinVol = bladderMin.Volume;

                    if (pl.Course.PlanSums != null)
                    {
                        PlanSum planSum = pl.Course.PlanSums.FirstOrDefault();
                        planSum.GetDVHCumulativeData(bladderMin, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);

                        blaMinVHigh = planSum.GetVolumeAtDose(bladderMin, dHigh, VolumePresentation.Relative);
                        blaMinVInt = planSum.GetVolumeAtDose(bladderMin, dInt, VolumePresentation.Relative);
                        blaMinVLow = planSum.GetVolumeAtDose(bladderMin, dLow, VolumePresentation.Relative);
                    }
                    else
                    {
                        //Get Dose constraint values.
                        pl.GetDVHCumulativeData(bladderMin, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);
                        blaMinVHigh = pl.GetVolumeAtDose(bladderMin, dHigh, VolumePresentation.Relative);
                        blaMinVInt = pl.GetVolumeAtDose(bladderMin, dInt, VolumePresentation.Relative);
                        blaMinVLow = pl.GetVolumeAtDose(bladderMin, dLow, VolumePresentation.Relative);
                    }


                    //Compare bladdermin constraint values to bladder constraints.
                    if (blaMinVInt > vInt || blaMinVHigh > vHigh || blaMinVol < 80) 
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

                        //Get dose constraints.
                        blaMinVol = bladderMin.Volume;

                        if (pl.Course.PlanSums != null)
                        {
                            PlanSum planSum = pl.Course.PlanSums.FirstOrDefault();
                            planSum.GetDVHCumulativeData(bladderMin, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);

                            blaMinVHigh = planSum.GetVolumeAtDose(bladderMin, dHigh, VolumePresentation.Relative);
                            blaMinVInt = planSum.GetVolumeAtDose(bladderMin, dInt, VolumePresentation.Relative);
                            blaMinVLow = planSum.GetVolumeAtDose(bladderMin, dLow, VolumePresentation.Relative);
                        }
                        else
                        {
                            pl.GetDVHCumulativeData(bladderMin, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);
                            blaMinVHigh = pl.GetVolumeAtDose(bladderMin, dHigh, VolumePresentation.Relative);
                            blaMinVInt = pl.GetVolumeAtDose(bladderMin, dInt, VolumePresentation.Relative);
                            blaMinVLow = pl.GetVolumeAtDose(bladderMin, dLow, VolumePresentation.Relative);
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
                string message = string.Format("VHigh is {0}, VInt is {1}, VLow is {2}. Margins are: sup {3}mm and ant {4}mm. Volume is {5}cc", blaMinVHigh, blaMinVInt, blaMinVLow, supMargin - 1, antMargin, blaMinVol);
                MessageBox.Show(message);
                ButtonEnabled = true;
                Helpers.SeriLog.AddLog("BladderMin application finished. Bladdermin structure created successfully.");
            }));
        }
    }
}
