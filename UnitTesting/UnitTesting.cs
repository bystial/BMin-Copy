using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using VMS.TPS;
using BladderMin;
using Moq;
using System.Net.NetworkInformation;

using PropertyChanged;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Text.RegularExpressions;

namespace UnitTesting
{
    [TestClass]
    public class UnitTesting
    {
        [TestMethod]
        public void Test_Tests()
        {
            Assert.IsFalse(2 == 1);
        }
    }
    [TestClass]
    public class TestViewModel
    {
        private IViewModel GetMockViewModel()
        {
            Mock<IViewModel> mockVM = new Mock<IViewModel>();
            object param = null;
            mockVM.Setup(x => x.Start(param));
            return mockVM.Object;
        }
        [TestMethod]
        public void TestVM_Start_IsFalse()
        {
            IViewModel vm = GetMockViewModel();
            var ew = vm.ScriptWorking;
            Assert.IsFalse(ew);
        }
    }
    [TestClass]
    public class TestEsapiWorker
    {
        Action<Patient, PlanSetup> actionCallBack = null;
        [TestMethod]
        public async Task AsyncRun_Default_ReturnsTrue()
        {
            Mock<IEsapiWorker> mockEW = new Mock<IEsapiWorker>();
            mockEW
                .Setup(x => x.AsyncRun(It.IsAny<Action<Patient, PlanSetup>>()))
                .Callback((Action<Patient, PlanSetup> a) => actionCallBack = a);
            var test = mockEW.Object;
            EsapiWorker ew = new EsapiWorker(test);
            var res = await ew.AsyncRun((p, ps) => { return; });
            actionCallBack(test.P, test.PS);
            Assert.IsTrue(res);
        }
    }
}