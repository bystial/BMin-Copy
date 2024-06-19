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

namespace BladderMinCrossOut
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
        //Properties
        public string Name { get; set; }
        public List<BladderConstraint> ProtocolConstraints { get; set; } = new List<BladderConstraint>();
        public double minVolumeConstraint { get; set; } = 80;
        public bool isNodesTreatable { get; set; } = true;
        public bool isMultiPhase { get; set; } = false;
        private bool _isNodesSelected = true;
        private Protocol_Preprocessor.BladderminProtocol proto;
        public Protocol_Preprocessor.BladderminProtocol Proto
        {
            get { return proto; }
            set { proto = value; }
        }
        public Protocol(string selectedProtocolType, bool isNodesSelectedDefault)
        {
            //Instantiate the protocol
            _isNodesSelected = isNodesSelectedDefault;
            ProtocolType = selectedProtocolType;
            ProtocallInitializer();
            SetContraints();
        }
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
        public string ProtocolType { get; private set; }
        //A method to indicate whether nodes have been selected by the user and updates the protocol constraints as necessary
        public void SetNodesSelected(bool isNodesSelected)
        {
            _isNodesSelected = isNodesSelected;
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
            var protocolConstraintsAreMet = constraintResults.All(x => x.IsMet) && s.Volume > minVolumeConstraint;
            return new ProtocolResult(protocolConstraintsAreMet, s.Volume, constraintResults);
        }
        private void ProtocallInitializer()
        {
            var protocol = ProtocolType;
            SerializeData data = new SerializeData();
            string protocolFile = $@"{data.path}{protocol}.xml";
            var proto = data.SerializeProtocol(protocolFile);
            Proto = proto;
        }
        //Sets the constraints depending on the protocol selected and whether nodes are selected or not.
        private void SetContraints()
        {
            var proto = Proto;
            var name = from ele in proto.ProtocolMetaData //from the protocol meta data 
                       select ele.Name; //select the protocol name
            var nodes = from ele in proto.ProtocolMetaData //from the protocol meta data
                        select ele.NodesTreatable; //determine if nodes treatable
            var multiPhase = from ele in proto.ProtocolMetaData //from the protocol meta data
                             select ele.MultiPhase; //determine if multi phase
            var constraintsCol = from ele in proto.Constraints //from the protocol constraints
                                 where !Convert.ToBoolean(String.Concat(ele.Nodes))
                                 select ele; //select the constraints
            var constraintsWithNodesCol = from ele in proto.Constraints //from the protocol constraints
                                          where Convert.ToBoolean(String.Concat(ele.Nodes))
                                          select ele; //select the constraints
            Name = name.FirstOrDefault();
            if (_isNodesSelected)
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
                        var volumePresentation = cons.VolumePresentaition;
                        Enum.TryParse(String.Concat(doseUnit), out VolumePresentation volumePresentationEnum);
                        var volume = cons.Volume;
                        Double.TryParse(String.Concat(volume), out double volumeDouble);
                        ProtocolConstraints.Add(new BladderConstraint(String.Concat(constraintName),
                            new DoseValue(doseValueDouble, doseUnitEnum), volumePresentationEnum, volumeDouble));
                        SeriLog.AddLog(string.Format($"Protocol selected: {Name} \n Nodes treated: {_isNodesSelected}"));
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
                        var volumePresentation = cons.VolumePresentaition;
                        Enum.TryParse(String.Concat(doseUnit), out VolumePresentation volumePresentationEnum);
                        var volume = cons.Volume;
                        Double.TryParse(String.Concat(volume), out double volumeDouble);
                        ProtocolConstraints.Add(new BladderConstraint(String.Concat(constraintName),
                            new DoseValue(doseValueDouble, doseUnitEnum), volumePresentationEnum, volumeDouble));
                        SeriLog.AddLog(string.Format($"Protocol selected: {Name} \n Nodes treated: {_isNodesSelected}"));
                    }
                }
            }
        }
    }
}
