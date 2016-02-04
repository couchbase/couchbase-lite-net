using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Todo.WPF
{
    internal sealed class SimpleViewModel : INotifyPropertyChanged
    {
        private readonly SimpleModel _model;
        public event PropertyChangedEventHandler PropertyChanged;

        public string SyncURL
        {
            get { return _model.SyncURL; }
            set { SetValue(ref _model.SyncURL, value); }
        }

        public SimpleViewModel()
        {
            _model = new SimpleModel();
        }

        public SimpleViewModel(SimpleModel model)
        {
            _model = model;
            model.LoadValues();
        }

        public SimpleViewModel(SimpleViewModel source) : this()
        {
            LoadFrom(source);
        }

        public void LoadFrom(SimpleViewModel source)
        {
            SyncURL = source.SyncURL;
        }

        public void Save()
        {
            _model.SaveValues();
        }

        private void SetValue<T>(ref T value, T newValue, [CallerMemberName]string name = null)
        {
            value = newValue;
            if (PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(name.Substring(4)));
            }
        }
    }
}
