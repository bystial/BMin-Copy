using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Reflection;
using System.ComponentModel;
using Serilog;
using System.Diagnostics;
using System.Windows;

namespace VMS.TPS
{
    //Methods and classes that help do things to simplify code
    public static class Helpers
    {
        public static class SeriLog
        {
            //A class for logging errors and exceptions into a log file that can then be read by the user.
            public static void Initialize(string user = "RunFromLauncher")
            {
                var SessionTimeStart = DateTime.Now;
                var AssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var directory = Path.Combine(AssemblyPath, @"Logs");
                var logpath = Path.Combine(directory, string.Format(@"log_{0}_{1}_{2}.txt", SessionTimeStart.ToString("dd-MMM-yyyy"), SessionTimeStart.ToString("hh-mm-ss"), user.Replace(@"\", @"_")));
                Log.Logger = new LoggerConfiguration().WriteTo.File(logpath, Serilog.Events.LogEventLevel.Information, 
                    "{Timestamp:dd-MMM-yyy HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}").CreateLogger();
            }
            public static void AddLog(string logInfo)
            {
                Log.Information(logInfo);
            }
            public static void AddError(string logInfo, Exception ex = null)
            {
                if (ex == null)
                    Log.Error(logInfo);
                else
                    Log.Error(ex, logInfo);
            }
        }

        //Defines dose values and volume constraints based on the user selected protocol
        public static void GetProtocolValues(string SelectedProtocol, bool IsSelected, ref double vHigh, ref double vInt, ref double vLow, ref double dLowIso, ref DoseValue dHigh, ref DoseValue dInt, ref DoseValue dLow, List<string> protocolConstraints)
        {
            if (SelectedProtocol == "Prostate 60 Gy in 20#")
            {
                vHigh = 5; //Needs to be written as an actual % (ie. 1% not 0.01)
                vInt = 25;
                vLow = 50;
                dHigh = new DoseValue(6000, DoseValue.DoseUnit.cGy);
                dInt = new DoseValue(4800, DoseValue.DoseUnit.cGy);
                dLow = new DoseValue(3800, DoseValue.DoseUnit.cGy);
                dLowIso = 63.3;
                SeriLog.AddLog("Protocol selected: Prostate 60 Gy in 20#");
                protocolConstraints.Add("V60 ≤ 5%");
                protocolConstraints.Add("V48 ≤ 25%");
                protocolConstraints.Add("V38 ≤ 50%");
                return;
            }
            if (SelectedProtocol == "Prostate 70 Gy in 28#")
            {
                if (IsSelected) //Toggle box for nodal coverage
                {
                    vHigh = 25;
                    vLow = 50;
                    dLowIso = 80.0;
                    dHigh = new DoseValue(6500, DoseValue.DoseUnit.cGy);
                    dLow = new DoseValue(5600, DoseValue.DoseUnit.cGy);
                    SeriLog.AddLog(string.Format("Protocol selected: Prostate 70 Gy in 28#  \n Nodes treated: Y"));
                    protocolConstraints.Add("V65 ≤ 25%");
                    protocolConstraints.Add("V56 ≤ 50%");
                    return;
                }
                vHigh = 25;
                vLow = 50;
                dLowIso = 67.1;
                dHigh = new DoseValue(6500, DoseValue.DoseUnit.cGy);
                dLow = new DoseValue(4700, DoseValue.DoseUnit.cGy);
                SeriLog.AddLog(string.Format("Protocol selected: Prostate 70 Gy in 28#  \n Nodes treated: N"));
                protocolConstraints.Add("V65 ≤ 25%");
                protocolConstraints.Add("V47 ≤ 50%");
                return;
            }
            if (SelectedProtocol == "Prostate 78 Gy in 39# 2-phase")
            {
                if (IsSelected) //Toggle box for nodal coverage
                {
                    vHigh = 25;
                    vLow = 50;
                    dHigh = new DoseValue(7000, DoseValue.DoseUnit.cGy);
                    dLow = new DoseValue(6000, DoseValue.DoseUnit.cGy);
                    SeriLog.AddLog(string.Format("Protocol selected: Prostate 78 Gy in 39# 2-phase  \n Nodes treated: Y"));
                    protocolConstraints.Add("V70 ≤ 25%");
                    protocolConstraints.Add("V60 ≤ 50%");
                    return;
                }
                vHigh = 25;
                vLow = 50;
                dHigh = new DoseValue(7000, DoseValue.DoseUnit.cGy);
                dLow = new DoseValue(5000, DoseValue.DoseUnit.cGy);
                SeriLog.AddLog(string.Format("Protocol selected: Prostate 78 Gy in 39# 2-phase  \n Nodes treated: N"));
                protocolConstraints.Add("V70 ≤ 25%");
                protocolConstraints.Add("V56 ≤ 50%");
                return;
            }
            if (SelectedProtocol == "Prostate SABR 36.25 Gy in 5#")
            {
                vHigh = 10;
                vInt = 20;
                vLow = 45;
                dLowIso = 49.7;
                dHigh = new DoseValue(3600, DoseValue.DoseUnit.cGy);
                dInt = new DoseValue(3300, DoseValue.DoseUnit.cGy);
                dLow = new DoseValue(1800, DoseValue.DoseUnit.cGy);
                SeriLog.AddLog("Protocol selected: Prostate SABR 36.25 Gy in 5#");
                protocolConstraints.Add("V36 ≤ 10%");
                protocolConstraints.Add("V33 ≤ 20%");
                protocolConstraints.Add("V18 ≤ 45%");
                return;
            }
            SeriLog.AddError("Protocol not supported.");
            throw new Exception("Protocol not supported.");
        }

        public static Structure CreateLowDoseIsoStructure(PlanSetup pl, double dLowIso, DoseValue dLow, PlanSum planSum)
        {
            Structure lowDoseIso;
            if (planSum != null)
            {
                lowDoseIso = planSum.StructureSet.AddStructure("CONTROL", "lowDoseIso");
                lowDoseIso.ConvertDoseLevelToStructure(planSum.Dose, dLow);
                lowDoseIso.ConvertToHighResolution();
            }
            else
            {
                lowDoseIso = pl.StructureSet.AddStructure("CONTROL", "lowDoseIso");
                lowDoseIso.ConvertDoseLevelToStructure(pl.Dose, new DoseValue(dLowIso, DoseValue.DoseUnit.Percent));
                lowDoseIso.ConvertToHighResolution();
            }

            return lowDoseIso;
        }

        //Checks if a structure already exists.
        public static bool CheckStructureExists(PlanSetup planSetup, string structureId)
        {
            if (planSetup.StructureSet.Structures.Select(x => x.Id).Any(y => y == structureId))
            {
                return true;
            }
            return false;
        }

        //Converts margins to align with Patient Orientation
        public static AxisAlignedMargins ConvertMargins(PatientOrientation patientOrientation, double rightMargin, double antMargin, double infMargin, double leftMargin, double postMargin, double supMargin)
        {
            switch (patientOrientation)
            {
                case PatientOrientation.HeadFirstSupine:
                    return new AxisAlignedMargins(StructureMarginGeometry.Inner, rightMargin, antMargin, infMargin, leftMargin, postMargin, supMargin);
                case PatientOrientation.HeadFirstProne:
                    return new AxisAlignedMargins(StructureMarginGeometry.Inner, leftMargin, postMargin, infMargin, rightMargin, antMargin, supMargin);
                case PatientOrientation.FeetFirstSupine:
                    return new AxisAlignedMargins(StructureMarginGeometry.Inner, leftMargin, antMargin, supMargin, rightMargin, postMargin, infMargin);
                case PatientOrientation.FeetFirstProne:
                    return new AxisAlignedMargins(StructureMarginGeometry.Inner, rightMargin, postMargin, supMargin, leftMargin, antMargin, infMargin);  
                default:
                    throw new Exception("This orientation is not currently supported");
            }

        }

        //Gets DVH values based on either a plan or a plan sum.
        public static void GetDVHValues (PlanSetup pl, PlanSum planSum, Structure bladderMin, DoseValue dHigh, DoseValue dInt, DoseValue dLow, out double blaMinVHigh, out double blaMinVInt, out double blaMinVLow)
        {
            if (planSum != null)
            {
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
        }



    }
}
