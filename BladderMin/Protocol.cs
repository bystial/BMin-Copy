using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS;
using VMS.TPS.Common.Model.Types;
using static VMS.TPS.Helpers;

namespace BladderMin
{
    public class Protocol
    {
        //Properties
        public string Name { get; set; }

        public double vHigh { get; set; }
        public double vInt { get; set; }
        public double vLow { get; set; }

        public DoseValue dHigh { get; set; }
        public DoseValue dInt { get; set; }
        public DoseValue dLow { get; set; }
        public double dLowIso { get; set; }
        public List<string> ProtocolConstraints { get; set; }

        public bool isNodesTreatable { get; set; } = true;
        public bool isMultiPhase { get; set; } = false;

        public Protocol(BladderMinProtocolTypes selectedProtocolType, bool IsSelected)
        {
            //Instantiate the protocol
            vHigh = 0;
            vInt = 0;
            vLow = 0;

            dHigh = new DoseValue(0, DoseValue.DoseUnit.cGy);
            dInt = new DoseValue(0, DoseValue.DoseUnit.cGy);
            dLow = new DoseValue(0, DoseValue.DoseUnit.cGy);
            dLowIso = 0;

            switch (selectedProtocolType)
            {
                case BladderMinProtocolTypes.Prostate60in20:
                    {
                        Name = "Prostate 60 Gy in 20#";
                        vHigh = 5;
                        vInt = 25;
                        vLow = 50;

                        dHigh = new DoseValue(6000, DoseValue.DoseUnit.cGy);
                        dInt = new DoseValue(4800, DoseValue.DoseUnit.cGy);
                        dLow = new DoseValue(3800, DoseValue.DoseUnit.cGy);
                        dLowIso = 63.3;

                        ProtocolConstraints = new List<string>
                    {
                        "V60 ≤ 5%",
                        "V48 ≤ 25%",
                        "V38 ≤ 50%"
                    };

                        SeriLog.AddLog("Protocol selected: Prostate 60 Gy in 20#");
                        break;
                    }
                case BladderMinProtocolTypes.Prostate70in28:
                    {
                        Name = "Prostate 70 Gy in 28#";
                        if (IsSelected) //Toggle box for nodal coverage
                        {
                            vHigh = 25;
                            vLow = 50;

                            dHigh = new DoseValue(6500, DoseValue.DoseUnit.cGy);
                            dLow = new DoseValue(5600, DoseValue.DoseUnit.cGy);
                            dLowIso = 80.0;

                            ProtocolConstraints = new List<string>
                        {
                            "V65 ≤ 25%",
                            "V56 ≤ 50%"
                        };

                            SeriLog.AddLog(string.Format("Protocol selected: Prostate 70 Gy in 28#  \n Nodes treated: Y"));
                        }
                        else
                        {
                            vHigh = 25;
                            vLow = 50;

                            dHigh = new DoseValue(6500, DoseValue.DoseUnit.cGy);
                            dLow = new DoseValue(4700, DoseValue.DoseUnit.cGy);
                            dLowIso = 67.1;

                            ProtocolConstraints = new List<string>
                        {
                                "V65 ≤ 25%",
                                "V47 ≤ 50%"
                        };
                            SeriLog.AddLog(string.Format("Protocol selected: Prostate 70 Gy in 28#  \n Nodes treated: N"));
                        }
                        break;

                    }
                case BladderMinProtocolTypes.Prostate78in39:
                    {
                        Name = "Prostate 78 Gy in 39# (2 phase)";
                        isMultiPhase = true;
                        if (IsSelected) //Toggle box for nodal coverage
                        {
                            vHigh = 25;
                            vLow = 50;

                            dHigh = new DoseValue(7000, DoseValue.DoseUnit.cGy);
                            dLow = new DoseValue(6000, DoseValue.DoseUnit.cGy);
                            dLowIso = 76.9;

                            ProtocolConstraints = new List<string>
                        {
                            "V70 ≤ 25%",
                            "V60 ≤ 50%"
                        };

                            SeriLog.AddLog(string.Format("Protocol selected: Prostate 78 Gy in 39# 2-phase  \n Nodes treated: Y"));

                        }
                        else
                        {
                            vHigh = 25;
                            vLow = 50;

                            dHigh = new DoseValue(7000, DoseValue.DoseUnit.cGy);
                            dLow = new DoseValue(5000, DoseValue.DoseUnit.cGy);
                            dLowIso = 64.1;

                            ProtocolConstraints = new List<string>
                        {
                            "V70 ≤ 25%",
                            "V56 ≤ 50%"
                        };

                            SeriLog.AddLog(string.Format("Protocol selected: Prostate 78 Gy in 39# 2-phase  \n Nodes treated: N"));
                        }
                        break;
                    }
                case BladderMinProtocolTypes.ProstateSABR:
                    {
                        Name = "Prostate SABR 36.25Gy in 5#";
                        isNodesTreatable = false;
                        vHigh = 10;
                        vInt = 20;
                        vLow = 45;

                        dHigh = new DoseValue(3600, DoseValue.DoseUnit.cGy);
                        dInt = new DoseValue(3300, DoseValue.DoseUnit.cGy);
                        dLow = new DoseValue(1800, DoseValue.DoseUnit.cGy);
                        dLowIso = 49.7;

                        ProtocolConstraints = new List<string>
                    {
                        "V36 ≤ 10%",
                        "V33 ≤ 20%",
                        "V18 ≤ 45%"
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
    }
}
