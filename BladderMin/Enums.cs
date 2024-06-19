using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMS.TPS
{
    public enum BladderMinProtocolTypes
    {
        [Description("Prostate 60 Gy in 20#")] Prostate60in20,
        [Description("Prostate 70 Gy in 28#")] Prostate70in28,
        [Description("Prostate 66 Gy in 33# (Single phase)")] Prostate66in33_single,
        [Description("Prostate 66 Gy in 33# (2 phase)")] Prostate66in33_2phase,
        [Description("Prostate 78 Gy in 39# (2 phase)")] Prostate78in39,
        [Description("Prostate SABR 36.25 Gy in 5#")] ProstateSABR,
    }
}
