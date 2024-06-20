using BladderMin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static VMS.TPS.Helpers;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMS.TPS;
using System.Xml.Serialization;
using System.Xml;
using System.Data;
using System.Windows;

namespace BladderMin
{
    public struct ProtocolResult
    {
        public bool IsMet { get; private set; }
        public double BladderMinVolume { get; private set; }
        public List<BladderConstraintResult> ConstraintResults;
        public ProtocolResult(bool isMet, double bladderMinVolume, List<BladderConstraintResult> constraintResults)
        {
            BladderMinVolume = bladderMinVolume;
            ConstraintResults = constraintResults;
            IsMet = isMet;
        }
    }
    public class Protocol
    {
        //Backing Fields
        private bool isNodesSelected = true;
        private Protocol_Preprocessor.BladderminProtocol proto;
        private readonly List<Tuple<string, Protocol_Preprocessor.BladderminProtocol>> library;
        //Properties
        public string Name { get; set; }
        public List<BladderConstraint> ProtocolConstraints { get; set; } = new List<BladderConstraint>();
        public double MinVolumeConstraint { get; set; } = 80;
        public bool IsNodesTreatable { get; set; } = false;
        public bool IsMultiPhase { get; set; } = false;
        public Protocol_Preprocessor.BladderminProtocol Proto
        {
            get { return proto; }
            set { proto = value; }
        }
        public List<Tuple<string, Protocol_Preprocessor.BladderminProtocol>> Library
        {
            get { return library; }
        }
        public string ProtocolType { get; private set; }
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
        //Constructor
        public Protocol(string selectedProtocolType, bool isNodesSelectedDefault)
        {
            //instance the protocol library
            library = SetProtocols();
            //SeriLog.AddLog("tock");
            //instance the selected protocol
            isNodesSelected = isNodesSelectedDefault;
            ProtocolType = selectedProtocolType;
            ProtocallInitializer();
            SetContraints();
        }
        //A method to return the protocol library
        public static List<Tuple<string, Protocol_Preprocessor.BladderminProtocol>> SetProtocols()
        {
            var lib = new LibraryBuffer();
            return lib.Library;
        }
        //A method to indicate whether nodes have been selected by the user and updates the protocol constraints as necessary
        public void SetNodesSelected(bool isNodesSelected)
        {
            this.isNodesSelected = isNodesSelected;
            SetContraints();
        }
        public ProtocolResult EvaluateBladderMin(PlanningItem p, Structure s)
        {
            var constraintResults = new List<BladderConstraintResult>();
            foreach (var constraint in ProtocolConstraints)
            {
                var (isMet, vol) = constraint.GetVolumeAtConstraint(p, s);
                constraintResults.Add(new BladderConstraintResult { IsMet = isMet, Volume = vol });
            }
            var protocolConstraintsAreMet = constraintResults.All(x => x.IsMet) && s.Volume > MinVolumeConstraint;
            return new ProtocolResult(protocolConstraintsAreMet, s.Volume, constraintResults);
        }
        //Initialize the selected protocol
        private void ProtocallInitializer()
        {
            var protocol = ProtocolType; //fetch the protocol name from the protocol type property
            var proto = from books in Library //from the protocols in the library
                        where books.Item1 == protocol //where the name of the protocol is the one we want
                        select books.Item2; //select the protocols that have that name
            var insist1 = proto.FirstOrDefault(); //insist there is only a single protocol with that name
            this.proto = insist1; //set the protocol field to the protocol we want
        }
        //Sets the constraints depending on the protocol selected and whether nodes are selected or not.
        private void SetContraints()
        {
            var proto = Proto; //fetch the protocol from the protocol property
            var name = from ele in proto.ProtocolMetaData //from the protocol meta data 
                       select ele.Name; //select the protocol name
            var nodes = from ele in proto.ProtocolMetaData //from the protocol meta data
                        select ele.NodesTreatable; //determine if nodes treatable
            var multiPhase = from ele in proto.ProtocolMetaData //from the protocol meta data
                             select ele.MultiPhase; //determine if multi phase
            var constraintsCol = from ele in proto.Constraints //from the protocol constraints
                                 where !Convert.ToBoolean(String.Concat(ele.Nodes)) //where nodes aren't treated
                                 select ele; //select the constraints
            var constraintsWithNodesCol = from ele in proto.Constraints //from the protocol constraints
                                          where Convert.ToBoolean(String.Concat(ele.Nodes)) //where nodes are treated
                                          select ele; //select the constraints
            IsMultiPhase = Convert.ToBoolean(String.Concat(multiPhase));
            IsNodesTreatable = Convert.ToBoolean(String.Concat(nodes));
            Name = name.FirstOrDefault();
            if (isNodesSelected)
            {
                ProtocolConstraints = new List<BladderConstraint>();
                foreach (var constraint in constraintsWithNodesCol)
                {
                    foreach (var cons in constraint.Constraint)
                    {
                        var constraintName = cons.Name;
                        var doseValue = cons.DoseValue;
                        Double.TryParse(String.Concat(doseValue), out double doseValueDouble);
                        var doseUnit = cons.DoseUnit;
                        Enum.TryParse(String.Concat(doseUnit), out DoseValue.DoseUnit doseUnitEnum);
                        var volumePresentation = cons.VolumePresentation;
                        Enum.TryParse(String.Concat(doseUnit), out VolumePresentation volumePresentationEnum);
                        var volume = cons.Volume;
                        Double.TryParse(String.Concat(volume), out double volumeDouble);
                        ProtocolConstraints.Add(new BladderConstraint(String.Concat(constraintName),
                            new DoseValue(doseValueDouble, doseUnitEnum), volumePresentationEnum, volumeDouble));
                        //SeriLog.AddLog(string.Format($"Protocol selected: {Name} \n Nodes treated: {isNodesSelected}"));
                    }
                }
            }
            else
            {
                ProtocolConstraints = new List<BladderConstraint>();
                foreach (var constraint in constraintsCol)
                {
                    foreach (var cons in constraint.Constraint)
                    {
                        var constraintName = cons.Name;
                        var doseValue = cons.DoseValue;
                        Double.TryParse(String.Concat(doseValue), out double doseValueDouble);
                        var doseUnit = cons.DoseUnit;
                        Enum.TryParse(String.Concat(doseUnit), out DoseValue.DoseUnit doseUnitEnum);
                        var volumePresentation = cons.VolumePresentation;
                        Enum.TryParse(String.Concat(doseUnit), out VolumePresentation volumePresentationEnum);
                        var volume = cons.Volume;
                        Double.TryParse(String.Concat(volume), out double volumeDouble);
                        ProtocolConstraints.Add(new BladderConstraint(String.Concat(constraintName),
                            new DoseValue(doseValueDouble, doseUnitEnum), volumePresentationEnum, volumeDouble));
                        //SeriLog.AddLog(string.Format($"Protocol selected: {Name} \n Nodes treated: {isNodesSelected}"));
                    }
                }
            }
            SeriLog.AddLog(string.Format($"Protocol selected: {Name} \n Nodes treated: {isNodesSelected}"));
        }
    }
    //class that holds an action and makes sure it is only acted on one time
    //public class HoldAction
    //{
    //    private readonly Action A;
    //    private bool proc;
    //    public HoldAction(Action a)
    //    {
    //        A = a;
    //    }
    //    public void Act()
    //    {
    //        if (proc) { return; }
    //        A();
    //        proc = true;
    //    }
    //}
}
