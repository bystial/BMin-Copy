using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMS.TPS
{
    public interface IEsapiWorker
    {
        PlanSetup PS {get; set;}
        StructureSet SS { get; set;}
        Patient P { get; set;}
        Application App { get; set;}
        Dispatcher Dispatcher { get; set; }

        void Run(Action<PlanSetup> a);
        void Run(Action<Patient, PlanSetup> a);

        bool RunStructure(Action<StructureSet> a);

        Task<bool> AsyncRun(Action<Patient, PlanSetup> a);

        Task<bool> AsyncRun(Action<Patient, PlanSetup, Application> a);
        Task<bool> AsyncRunStructureContext(Action<Patient, StructureSet> a);
        Task<bool> AsyncAppRun(Action<Application> a);
    }
}
