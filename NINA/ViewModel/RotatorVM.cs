﻿using NINA.Model;
using NINA.Model.MyRotator;
using NINA.Utility;
using NINA.Utility.Mediator.Interfaces;
using NINA.Utility.Notification;
using NINA.Utility.Profile;
using NINA.ViewModel.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NINA.ViewModel {

    internal class RotatorVM : DockableVM, IRotatorVM {

        public RotatorVM(IProfileService profileService, IRotatorMediator rotatorMediator, IApplicationStatusMediator applicationStatusMediator) : base(profileService) {
            Title = "LblRotator";
            ImageGeometry = (System.Windows.Media.GeometryGroup)System.Windows.Application.Current.Resources["RotatorSVG"];

            this.rotatorMediator = rotatorMediator;
            this.rotatorMediator.RegisterHandler(this);
            this.applicationStatusMediator = applicationStatusMediator;

            ConnectCommand = new AsyncCommand<bool>(() => Connect());
            CancelConnectCommand = new RelayCommand(CancelConnectRotator);
            DisconnectCommand = new RelayCommand(DisconnectDiag);
            RefreshRotatorListCommand = new RelayCommand(RefreshRotatorList);
            MoveCommand = new AsyncCommand<float>(() => Move(TargetPosition), (p) => RotatorInfo.Connected);
            HaltCommand = new RelayCommand(Halt);

            updateTimer = new DeviceUpdateTimer(
                GetRotatorValues,
                UpdateRotatorValues,
                profileService.ActiveProfile.ApplicationSettings.DevicePollingInterval
            );

            profileService.ProfileChanged += (object sender, EventArgs e) => {
                RefreshRotatorList(null);
            };
        }

        private void Halt(object obj) {
            _moveCts?.Cancel();
            rotator?.Halt();
        }

        public async Task<float> Move(float targetPosition) {
            _moveCts = new CancellationTokenSource();
            float pos = float.NaN;
            await Task.Run(() => {
                try {
                    RotatorInfo.IsMoving = true;
                    rotator.MoveAbsolute(targetPosition);
                    while (RotatorInfo.IsMoving && RotatorInfo.Position != targetPosition) {
                        _moveCts.Token.ThrowIfCancellationRequested();
                    }
                    RotatorInfo.Position = targetPosition;
                    pos = targetPosition;
                    BroadcastRotatorInfo();
                } catch (OperationCanceledException) {
                }
            });
            return pos;
        }

        public async Task<float> MoveRelative(float offset) {
            float pos = -1;
            if (rotator?.Connected == true) {
                pos = rotator.Position + offset;
                await Move(pos);
            }
            return pos;
        }

        private void UpdateRotatorValues(Dictionary<string, object> rotatorValues) {
            object o = null;
            rotatorValues.TryGetValue(nameof(RotatorInfo.Connected), out o);
            RotatorInfo.Connected = (bool)(o ?? false);

            rotatorValues.TryGetValue(nameof(RotatorInfo.Position), out o);
            RotatorInfo.Position = (float)(o ?? 0f);

            rotatorValues.TryGetValue(nameof(RotatorInfo.IsMoving), out o);
            RotatorInfo.IsMoving = (bool)(o ?? false);

            BroadcastRotatorInfo();
        }

        private Dictionary<string, object> GetRotatorValues() {
            Dictionary<string, object> rotatorValues = new Dictionary<string, object>();
            rotatorValues.Add(nameof(RotatorInfo.Connected), rotator?.Connected ?? false);
            rotatorValues.Add(nameof(RotatorInfo.Position), rotator?.Position ?? 0);
            rotatorValues.Add(nameof(RotatorInfo.IsMoving), rotator?.IsMoving ?? false);
            return rotatorValues;
        }

        private DeviceUpdateTimer updateTimer;
        private RotatorInfo rotatorInfo;

        public RotatorInfo RotatorInfo {
            get {
                if (rotatorInfo == null) {
                    rotatorInfo = DeviceInfo.CreateDefaultInstance<RotatorInfo>();
                }
                return rotatorInfo;
            }
            set {
                rotatorInfo = value;
                RaisePropertyChanged();
            }
        }

        public RotatorInfo GetDeviceInfo() {
            return RotatorInfo;
        }

        public void RefreshRotatorList(object obj) {
            RotatorChooserVM.GetEquipment();
        }

        private RotatorChooserVM rotatorChooserVM;

        public RotatorChooserVM RotatorChooserVM {
            get {
                if (rotatorChooserVM == null) {
                    rotatorChooserVM = new RotatorChooserVM(profileService);
                }
                return rotatorChooserVM;
            }
            set {
                rotatorChooserVM = value;
            }
        }

        private float targetPosition;

        public float TargetPosition {
            get { return targetPosition; }
            set { targetPosition = value; RaisePropertyChanged(); }
        }

        private IRotator rotator;
        private IRotatorMediator rotatorMediator;
        private IApplicationStatusMediator applicationStatusMediator;

        public IAsyncCommand ConnectCommand { get; private set; }
        public ICommand CancelConnectCommand { get; private set; }
        public ICommand DisconnectCommand { get; private set; }
        public ICommand RefreshRotatorListCommand { get; private set; }
        public IAsyncCommand MoveCommand { get; private set; }
        public ICommand HaltCommand { get; private set; }

        private CancellationTokenSource _connectRotatorCts;
        private CancellationTokenSource _moveCts;
        private readonly SemaphoreSlim ss = new SemaphoreSlim(1, 1);

        public async Task<bool> Connect() {
            await ss.WaitAsync();
            try {
                Disconnect();
                updateTimer?.Stop();

                if (RotatorChooserVM.SelectedDevice.Id == "No_Device") {
                    profileService.ActiveProfile.RotatorSettings.Id = RotatorChooserVM.SelectedDevice.Id;
                    return false;
                }

                applicationStatusMediator.StatusUpdate(
                    new ApplicationStatus() {
                        Source = Title,
                        Status = Locale.Loc.Instance["LblConnecting"]
                    }
                );

                var rotator = (IRotator)RotatorChooserVM.SelectedDevice;
                _connectRotatorCts = new CancellationTokenSource();
                if (rotator != null) {
                    try {
                        var connected = await rotator?.Connect(_connectRotatorCts.Token);
                        _connectRotatorCts.Token.ThrowIfCancellationRequested();
                        if (connected) {
                            this.rotator = rotator;

                            RotatorInfo = new RotatorInfo {
                                Connected = true,
                                IsMoving = rotator.IsMoving,
                                Name = rotator.Name,
                                Description = rotator.Description,
                                Position = rotator.Position,
                                DriverInfo = rotator.DriverInfo,
                                DriverVersion = rotator.DriverVersion
                            };

                            Notification.ShowSuccess(Locale.Loc.Instance["LblRotatorConnected"]);

                            updateTimer.Interval = profileService.ActiveProfile.ApplicationSettings.DevicePollingInterval;
                            updateTimer.Start();

                            TargetPosition = rotator.Position;
                            profileService.ActiveProfile.RotatorSettings.Id = rotator.Id;
                            return true;
                        } else {
                            RotatorInfo.Connected = false;
                            this.rotator = null;
                            return false;
                        }
                    } catch (OperationCanceledException) {
                        if (RotatorInfo.Connected) { Disconnect(); }
                        return false;
                    }
                } else {
                    return false;
                }
            } finally {
                ss.Release();
                applicationStatusMediator.StatusUpdate(
                    new ApplicationStatus() {
                        Source = Title,
                        Status = string.Empty
                    }
                );
            }
        }

        private void CancelConnectRotator(object o) {
            _connectRotatorCts?.Cancel();
        }

        public void Disconnect() {
            if (RotatorInfo.Connected) {
                updateTimer?.Stop();
                rotator?.Disconnect();
                rotator = null;
                RotatorInfo = DeviceInfo.CreateDefaultInstance<RotatorInfo>();
                BroadcastRotatorInfo();
            }
        }

        private void DisconnectDiag(object obj) {
            var diag = MyMessageBox.MyMessageBox.Show("Disconnect Rotator?", "", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxResult.Cancel);
            if (diag == System.Windows.MessageBoxResult.OK) {
                Disconnect();
            }
        }

        private void BroadcastRotatorInfo() {
            rotatorMediator.Broadcast(GetDeviceInfo());
        }
    }

    internal class RotatorChooserVM : EquipmentChooserVM {

        public RotatorChooserVM(IProfileService profileService) : base(typeof(RotatorChooserVM), profileService) {
        }

        public override void GetEquipment() {
            Devices.Clear();

            Devices.Add(new DummyDevice(Locale.Loc.Instance["LblNoRotator"]));

            var ascomDevices = new ASCOM.Utilities.Profile();

            foreach (ASCOM.Utilities.KeyValuePair device in ascomDevices.RegisteredDevices("Rotator")) {
                try {
                    AscomRotator rotator = new AscomRotator(device.Key, device.Value);
                    Devices.Add(rotator);
                } catch (Exception) {
                    //only add filter wheels which are supported. e.g. x86 drivers will not work in x64
                }
            }

            Devices.Add(new ManualRotator(profileService));

            DetermineSelectedDevice(profileService.ActiveProfile.RotatorSettings.Id);
        }
    }
}