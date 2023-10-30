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
        [Description("Prostate 66 Gy in 33#")] Prostate66in33,
        [Description("Prostate 78 Gy in 39# (2 phase)")] Prostate78in39,
        [Description("Prostate SABR 36.25 Gy in 5#")] ProstateSABR,

    }
}
