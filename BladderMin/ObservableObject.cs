using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VMS.TPS
{public class ObservableObject
    {
        private readonly IObservableObject obsObj;
        public ObservableObject(IObservableObject obsObj)
        {
            this.obsObj = obsObj;
        }
        public void OnPropertyChanged(string propertyName)
        {
            obsObj.OnPropertyChanged(propertyName);
        }
        public void RaisePropertyChangedEvent([CallerMemberName] string propertyName = null)
        {
            obsObj.OnPropertyChanged(propertyName);
        }
    }
    public class ObservableObject_Default : IObservableObject
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public void RaisePropertyChangedEvent([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
