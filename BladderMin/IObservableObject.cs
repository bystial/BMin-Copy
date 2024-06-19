using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VMS.TPS
{
    public interface IObservableObject : INotifyPropertyChanged
    {
        void OnPropertyChanged(string propertyName);
        void RaisePropertyChangedEvent([CallerMemberName] string propertyName = null);
    }
}
