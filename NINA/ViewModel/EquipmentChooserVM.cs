﻿using NINA.Utility;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NINA.ViewModel {
    abstract class EquipmentChooserVM : BaseVM {

        public EquipmentChooserVM() {
            SetupDialogCommand = new RelayCommand(OpenSetupDialog);
            GetEquipment();
        }

        private ObservableCollection<Model.IDevice> _devices;
        public ObservableCollection<Model.IDevice> Devices {
            get {
                if (_devices == null) {
                    _devices = new ObservableCollection<Model.IDevice>();
                }
                return _devices;
            }
            set {
                _devices = value;
            }
        }

        public abstract void GetEquipment();

        private Model.IDevice _selectedDevice;
        public Model.IDevice SelectedDevice {
            get {
                return _selectedDevice;
            }
            set {
                _selectedDevice = value;
                RaisePropertyChanged();
            }
        }

        ICommand _setupDialogCommand;
        public ICommand SetupDialogCommand {
            get {
                return _setupDialogCommand;
            }
            set {
                _setupDialogCommand = value;
            }
        }

        private void OpenSetupDialog(object o) {
            if (SelectedDevice?.HasSetupDialog == true) {
                SelectedDevice.SetupDialog();
            }
        }


    }
}
