﻿using System;
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
using BladderMin;

namespace VMS.TPS
{
    //Methods and classes that help do things to that are not necessarily tied to specific clinical logic
    public static class Helpers
    {
        //A class for logging errors and exceptions into a log file that can then be read by the user.
        public static class SeriLog
        {
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
      
         //Checks if a structure already exists.
        public static bool CheckStructureExists(PlanSetup planSetup, string structureId)
        {
            if (planSetup.StructureSet.Structures.Select(x => x.Id).Any(y => y == structureId))
            {
                return true;
            }
            return false;
        }

        //Converts inner margins to align with Patient Orientation
        public static AxisAlignedMargins ConvertInnerMargins(PatientOrientation patientOrientation, StructureMarginGeometry geometry, double rightMargin, double antMargin, double infMargin, double leftMargin, double postMargin, double supMargin)
        {
            switch (patientOrientation)
            {
                case PatientOrientation.HeadFirstSupine:
                    return new AxisAlignedMargins(geometry, rightMargin, antMargin, infMargin, leftMargin, postMargin, supMargin);
                case PatientOrientation.HeadFirstProne:
                    return new AxisAlignedMargins(geometry, leftMargin, postMargin, infMargin, rightMargin, antMargin, supMargin);
                case PatientOrientation.FeetFirstSupine:
                    return new AxisAlignedMargins(geometry, leftMargin, antMargin, supMargin, rightMargin, postMargin, infMargin);
                case PatientOrientation.FeetFirstProne:
                    return new AxisAlignedMargins(geometry, rightMargin, postMargin, supMargin, leftMargin, antMargin, infMargin);
                default:
                    throw new Exception("This orientation is not currently supported");
            }

        }

        //Converts outer margins to align with Patient Orientation
        public static AxisAlignedMargins ConvertOuterMargins(PatientOrientation patientOrientation, double rightMargin, double antMargin, double infMargin, double leftMargin, double postMargin, double supMargin)
        {
            switch (patientOrientation)
            {
                case PatientOrientation.HeadFirstSupine:
                    return new AxisAlignedMargins(StructureMarginGeometry.Outer, rightMargin, antMargin, infMargin, leftMargin, postMargin, supMargin);
                case PatientOrientation.HeadFirstProne:
                    return new AxisAlignedMargins(StructureMarginGeometry.Outer, leftMargin, postMargin, infMargin, rightMargin, antMargin, supMargin);
                case PatientOrientation.FeetFirstSupine:
                    return new AxisAlignedMargins(StructureMarginGeometry.Outer, leftMargin, antMargin, supMargin, rightMargin, postMargin, infMargin);
                case PatientOrientation.FeetFirstProne:
                    return new AxisAlignedMargins(StructureMarginGeometry.Outer, rightMargin, postMargin, supMargin, leftMargin, antMargin, infMargin);
                default:
                    throw new Exception("This orientation is not currently supported");
            }

        }
    }
}
