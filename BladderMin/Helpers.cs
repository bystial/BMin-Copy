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

namespace VMS.TPS
{
    //Methods and classes that help do things to simplify code
    public static class Helpers
    {
        //Logs a message everytime the script is run.
        public static class Logger
        {
            private static string logpath = @"\\sdappvimg004\esapi$\klawyer\V15PluginTester\TestScript\Logs\";
            public static void AddLog(string log_entry)
            {
                string path = Path.Combine(logpath, string.Format("log_{0}_{1}.txt", DateTime.Now.ToShortDateString(), DateTime.Now.ToString("hh_mm")));
                using (var data = new StreamWriter(path, true))
                {
                    data.WriteLine(log_entry);
                }
            }
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
        

    }
}
