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
    public class EsapiWorker
    {
        private readonly IEsapiWorker imp;
        public EsapiWorker(IEsapiWorker imp)
        {
            this.imp = imp;
        }
        public void Run(Action<PlanSetup> a)
        {
            imp.Run(a);
        }
        public void Run(Action<Patient, PlanSetup> a)
        {
            imp.Run(a);
        }
        public bool RunStructure(Action<StructureSet> a)
        {
            imp.RunStructure(a);
            return true;
        }
        public async Task<bool> AsyncRun(Action<Patient, PlanSetup> a)
        {
            await imp.AsyncRun(a);
            return true;
        }
        public async Task<bool> AsyncRun(Action<Patient, PlanSetup, Application> a)
        {
            await imp.AsyncRun(a);
            return true;
        }
        public delegate void D(Patient p, StructureSet s);
        public async Task<bool> AsyncRunStructureContext(Action<Patient, StructureSet> a)
        {
            await imp.AsyncRunStructureContext(a);
            return true;
        }
        public async Task<bool> AsyncAppRun(Action<Application> a)
        {
            await imp.AsyncAppRun(a);
            return true;
        }
    }
    public class EsapiWorker_Default : IEsapiWorker
    {
        private PlanSetup pl;
        private StructureSet ss;
        private Patient p;
        private Application app;
        private Dispatcher dispatcher;
        public PlanSetup PS
        {
            get { return pl; }
            set { pl = value; }
        }
        public StructureSet SS
        {
            get { return ss; }
            set { ss = value; }
        }
        public Patient P
        {
            get { return p; }
            set { p = value; }
        }
        public Application App
        {
            get { return app; }
            set { app = value; }
        }
        public Dispatcher Dispatcher
        {
            get { return dispatcher; }
            set { dispatcher = value; }
        }
        public void Run(Action<PlanSetup> a)
        {
            dispatcher.BeginInvoke(a, p);
        }
        public void Run(Action<Patient, PlanSetup> a)
        {
            dispatcher.BeginInvoke(a, p, pl);
        }
        public bool RunStructure(Action<StructureSet> a)
        {
            dispatcher.Invoke(a, pl);
            return true;
        }
        public async Task<bool> AsyncRun(Action<Patient, PlanSetup> a)
        {
            await dispatcher.BeginInvoke(a, p, pl);
            return true;
        }
        public async Task<bool> AsyncRun(Action<Patient, PlanSetup, Application> a)
        {
            await dispatcher.BeginInvoke(a, p, pl, app);
            return true;
        }
        public delegate void D(Patient p, StructureSet s);
        public async Task<bool> AsyncRunStructureContext(Action<Patient, StructureSet> a)
        {
            await dispatcher.BeginInvoke(a, p, ss);
            //var D = new SendOrPostCallback(_ => a(_p, _ss));
            //_context.Send(D, null);
            return true;
        }
        public async Task<bool> AsyncAppRun(Action<Application> a)
        {
            await dispatcher.BeginInvoke(a, pl);
            return true;
        }
    }
}
