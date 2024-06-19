using BladderMin;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMS.TPS
{
    public interface IViewModel
    {
        //Eclipse
        string StructureSetId { get; set; }
        string PlanId { get; set; }
        ObservableCollection<string> PlanSumList { get; set; }
        ObservableCollection<string> StructureList { get; set; }
        string SelectedBladderContour { get; set; }
        string SelectedPlanSum { get; set; }
        //Model
        List<Protocol> ProtocolList { get; set; }
        List<string> ProtocolConstraintsList { get; set; }
        List<string> ConstraintValuesList { get; set; }
        bool IsNodesSelected { get; set; }
        Protocol SelectedProtocol { get; set; }
        string BlaMinVol { get; set; }
        string SupMargin { get; set; }
        string AntMargin { get; set; }
        //UI
        string StatusMessage { get; set; }  
        bool ScriptWorking { get; set; }    
        bool ButtonEnabled { get; set; }
        SolidColorBrush StatusColour { get; set; }
        Visibility NodalSelectionVisibility { get; }
        Visibility PlanSumSelectionVisibility { get; }
        ICommand StartCommand { get;}
        //Methods
        Task InitializeAsync();
        Task Initialize { get; set; }
        void Start(object obj);
        //Additions and Abstractions 
        SolidColorBrush WarningColour { get; set; }
        SolidColorBrush FailColour { get; set; }
        SolidColorBrush ScriptDoneColour { get; set; }
        EsapiWorker Ew {  get; set; }
    }
}
