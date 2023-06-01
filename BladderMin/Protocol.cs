using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using static VMS.TPS.Helpers;

namespace BladderMin
{
    public struct ProtocolResult
    {
        public bool IsMet { get; private set; }
        public List<BladderConstraintResult> ConstraintResults;
        public ProtocolResult(List<BladderConstraintResult> constraintResults)
        {
            ConstraintResults = constraintResults;
            if (constraintResults.All(x => x.IsMet))
                IsMet = true;
            else
                IsMet = false;
        }
    }
    public class Protocol
    {
        //Properties
        public string Name { get; set; }

        public List<BladderConstraint> ProtocolConstraints { get; set; } = new List<BladderConstraint>();

        public bool isNodesTreatable { get; set; } = true;
        public bool isMultiPhase { get; set; } = false;

        private bool _isNodesSelected = false;

        public DoseValue LowDoseConstraintValue
        {
            get
            {
                if (ProtocolConstraints != null)
                    return ProtocolConstraints.Select(x => x.Dose).OrderByDescending(x => x.Dose).Last();
                else
                    return new DoseValue(0, DoseValue.DoseUnit.cGy);
            }
        }

        public BladderMinProtocolTypes ProtocolType { get; private set; }

        //A method to indicated whether nodes have been selected by the user and updates the protocol constraints as necessary
        public void SetNodesSelected(bool isNodesSelected)
        {
            _isNodesSelected = isNodesSelected;
            SetContraints();
        }

        public ProtocolResult EvaluateBladderMin(PlanningItem p, Structure s)
        {
            var results = new List<BladderConstraintResult>();
            foreach (var constraint in ProtocolConstraints)
            {
                var (isMet, vol) = constraint.GetVolumeAtConstraint(p, s);
                results.Add(new BladderConstraintResult { IsMet = isMet, Volume = vol });
            }
            return new ProtocolResult(results);
        }
        //Sets the constraints depending on the protocol selected and whether nodes are selected or not.
        private void SetContraints()
        {
            switch (ProtocolType)
            {
                case BladderMinProtocolTypes.Prostate60in20:
                    {
                        Name = "Prostate 60 Gy in 20#";
                        ProtocolConstraints = new List<BladderConstraint>
                        {
                            new BladderConstraint("V60 ≤ 5%", new DoseValue(6000, DoseValue.DoseUnit.cGy), VolumePresentation.Relative, 5),
                            new BladderConstraint("V48 ≤ 25%", new DoseValue(4800, DoseValue.DoseUnit.cGy), VolumePresentation.Relative, 25),
                            new BladderConstraint("V38 ≤ 50%", new DoseValue(3800, DoseValue.DoseUnit.cGy), VolumePresentation.Relative, 50),
                        };

                        SeriLog.AddLog("Protocol selected: Prostate 60 Gy in 20#");
                        break;
                    }
                case BladderMinProtocolTypes.Prostate70in28:
                    {
                        Name = "Prostate 70 Gy in 28#";
                        ProtocolConstraints = new List<BladderConstraint>
                        {
                            new BladderConstraint("V65 ≤ 25%", new DoseValue(6500, DoseValue.DoseUnit.cGy), VolumePresentation.Relative, 25),
                        };
                        if (_isNodesSelected) //Toggle box for nodal coverage
                        {
                            ProtocolConstraints.Add(new BladderConstraint("V56 ≤ 50%", new DoseValue(5600, DoseValue.DoseUnit.cGy), VolumePresentation.Relative, 50));
                            SeriLog.AddLog(string.Format("Protocol selected: Prostate 70 Gy in 28#  \n Nodes treated: Y"));
                        }
                        else
                        {
                            ProtocolConstraints.Add(new BladderConstraint("V47 ≤ 50%", new DoseValue(4700, DoseValue.DoseUnit.cGy), VolumePresentation.Relative, 50));
                            SeriLog.AddLog(string.Format("Protocol selected: Prostate 70 Gy in 28#  \n Nodes treated: N"));
                        }
                        break;

                    }
                case BladderMinProtocolTypes.Prostate78in39:
                    {
                        Name = "Prostate 78 Gy in 39# (2 phase)";
                        isMultiPhase = true;
                        ProtocolConstraints = new List<BladderConstraint>
                        {
                            new BladderConstraint("V70 ≤ 25%", new DoseValue(7000, DoseValue.DoseUnit.cGy), VolumePresentation.Relative, 25),
                        };
                        if (_isNodesSelected) //Toggle box for nodal coverage
                        {
                            ProtocolConstraints.Add(new BladderConstraint("V60 ≤ 50%", new DoseValue(6000, DoseValue.DoseUnit.cGy), VolumePresentation.Relative, 50));
                            SeriLog.AddLog(string.Format("Protocol selected: Prostate 78 Gy in 39# 2-phase  \n Nodes treated: Y"));
                        }
                        else
                        {
                            ProtocolConstraints.Add(new BladderConstraint("V50 ≤ 50%", new DoseValue(6000, DoseValue.DoseUnit.cGy), VolumePresentation.Relative, 50));
                            SeriLog.AddLog(string.Format("Protocol selected: Prostate 78 Gy in 39# 2-phase  \n Nodes treated: N"));
                        }
                        break;
                    }
                case BladderMinProtocolTypes.ProstateSABR:
                    {
                        Name = "Prostate SABR 36.25 Gy in 5#";
                        isNodesTreatable = false;
                        ProtocolConstraints = new List<BladderConstraint>
                        {
                             new BladderConstraint("V36 ≤ 10%", new DoseValue(3600, DoseValue.DoseUnit.cGy), VolumePresentation.Relative, 10),
                            new BladderConstraint("V33 ≤ 20%", new DoseValue(3300, DoseValue.DoseUnit.cGy), VolumePresentation.Relative, 20),
                            new BladderConstraint("V18 ≤ 45%", new DoseValue(1800, DoseValue.DoseUnit.cGy), VolumePresentation.Relative, 45),
                        };
                        SeriLog.AddLog("Protocol selected: Prostate SABR 36.25 Gy in 5#");
                        break;
                    }
                default:
                    {
                        throw new Exception("Unrecognized Protocol");
                    }
            }
        }

        // The Protocol class constructor
        public Protocol(BladderMinProtocolTypes selectedProtocolType, bool isNodesSelectedDefault)
        {
            //Instantiate the protocol
            _isNodesSelected = isNodesSelectedDefault;
            ProtocolType = selectedProtocolType;
            SetContraints();
        }
    }
}
