using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace BladderMin
{
    public class Bladdermin
    {
        //Properties
        //Define margins and constraints (so you can call them outside the loop if needed).
        public double supMargin = 0; //in mm
        public double antMargin = 0;
        public double infMargin = 25;

        //Define placeholders for bladdermin constraints.
        public double blaMinVol = 0;
        public double blaMinVHigh = 0;
        public double blaMinVInt = 0;
        public double blaMinVLow = 0;

        public List<string> blaMinConstraints = new List<string>() { };
        public List<string> marginsValues = new List<string>() { };


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
                //Get Dose constraint values.
                pl.GetDVHCumulativeData(bladderMin, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);
                blaMinVHigh = pl.GetVolumeAtDose(bladderMin, protocol.dHigh, VolumePresentation.Relative);
                blaMinVInt = pl.GetVolumeAtDose(bladderMin, protocol.dInt, VolumePresentation.Relative);
                blaMinVLow = pl.GetVolumeAtDose(bladderMin, protocol.dLow, VolumePresentation.Relative);
            }
        }

        public void ReduceBladderMin(PlanSetup pl, PlanSum planSum, Structure bladdermin, Protocol protocol)
        {
            private bool _constraintsMet = true;
    }

    }
}
