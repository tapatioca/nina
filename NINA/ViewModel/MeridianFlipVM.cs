﻿using NINA.Model;
using NINA.Model.MyTelescope;
using NINA.PlateSolving;
using NINA.Utility;
using NINA.Utility.Astrometry;
using NINA.Utility.Mediator;
using NINA.Utility.Notification;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NINA.ViewModel {
    class MeridianFlipVM : BaseVM {
        public MeridianFlipVM() {
            CancelCommand = new RelayCommand(Cancel);
            RegisterMediatorMessages();
        }

        private void Cancel(object obj) {
            _tokensource?.Cancel();
        }

        private void RegisterMediatorMessages() {
            Mediator.Instance.Register((object o) => {
                Telescope = (ITelescope)o;
            }, MediatorMessages.TelescopeChanged);

            Mediator.Instance.RegisterAsyncRequest(
                new CheckMeridianFlipMessageHandle(async (CheckMeridianFlipMessage msg) => {
                    return await CheckMeridianFlip(msg.Sequence);
                })
            );
        }

        private IProgress<ApplicationStatus> _progress;
        private CancellationTokenSource _tokensource;

        private ApplicationStatus _status;
        public ApplicationStatus Status {
            get {
                return _status;
            }
            set {
                _status = value;
                _status.Source = "MeridianFlip";
                RaisePropertyChanged();

                Mediator.Instance.Request(new StatusUpdateMessage() { Status = _status });
            }
        }

        private AutomatedWorkflow _steps;
        public AutomatedWorkflow Steps {
            get {
                return _steps;
            }
            set {
                _steps = value;
                RaisePropertyChanged();
            }
        }

        private ITelescope _telescope;
        public ITelescope Telescope {
            get {
                return _telescope;
            }
            private set {
                _telescope = value;
                RaisePropertyChanged();
            }
        }

        private bool ShouldFlip(double exposureTime) {
            if (Settings.AutoMeridianFlip) {
                if (Telescope?.Connected == true) {

                    if ((Telescope.TimeToMeridianFlip - (Settings.PauseTimeBeforeMeridian / 60)) < (exposureTime / 60 / 60)) {
                        return true;
                    }
                }
            }
            return false;
        }

        private TimeSpan _remainingTime;
        public TimeSpan RemainingTime {
            get {
                return _remainingTime;
            }
            set {
                _remainingTime = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// Checks if auto meridian flip should be considered and executes it
        /// 1) Compare next exposure length with time to meridian - If exposure length is greater than time to flip the system will wait
        /// 2) Stop Guider
        /// 3) Execute the flip
        /// 4) If recentering is enabled, platesolve current position, sync and recenter to old target position
        /// 5) Resume Guider
        /// </summary>
        /// <param name="seq">Current Sequence row</param>
        /// <param name="tokenSource">cancel token</param>
        /// <param name="progress">progress reporter</param>
        /// <returns></returns>
        public async Task<bool> CheckMeridianFlip(CaptureSequence seq) {            
            if (ShouldFlip(seq.ExposureTime)) {
                var service = new WindowService();
                this._tokensource = new CancellationTokenSource();
                this._progress = new Progress<ApplicationStatus>(p => Status = p);
                var flip = DoMeridianFlip();

                service.ShowDialog(this, "Meridian Flip");
                var flipResult = await flip;

                await service.Close();
                return flipResult;
            } else {
                return false;
            }
        }

        private Coordinates _targetCoordinates;

        private async Task<bool> PassMeridian(CancellationToken token, IProgress<ApplicationStatus> progress) {
            var timeToFlip = Telescope.TimeToMeridianFlip * 60 * 60;
            progress.Report(new ApplicationStatus() { Status = "Stop Scope tracking" });
            _targetCoordinates = Telescope.Coordinates;
            Mediator.Instance.Request(new SetTelescopeTrackingMessage() { Tracking = false });     
            do {
                RemainingTime = TimeSpan.FromSeconds(timeToFlip);
                
                //progress.Report(string.Format("Next exposure paused until passing meridian. Remaining time: {0} seconds", RemainingTime));
                var delta = await Utility.Utility.Delay(1000, token);
                
                timeToFlip -= delta.TotalSeconds;

            } while (RemainingTime.TotalSeconds >= 1);
            progress.Report(new ApplicationStatus() { Status = "Resume Scope tracking" });
            Mediator.Instance.Request(new SetTelescopeTrackingMessage() { Tracking = true });
            return true;
        }

        private async Task<bool> DoFilp(CancellationToken token, IProgress<ApplicationStatus> progress) {
            progress.Report(new ApplicationStatus() { Status = "Flipping Scope" });
            var flipsuccess = Telescope.MeridianFlip(_targetCoordinates);

            await Settle(token, progress);

            return flipsuccess;
        }


        private async Task<bool> Recenter(CancellationToken token, IProgress<ApplicationStatus> progress) {
            if (Settings.RecenterAfterFlip) {
                progress.Report(new ApplicationStatus() { Status = "Initiating platesolve" });
                await Mediator.Instance.RequestAsync(new PlateSolveMessage() { SyncReslewRepeat = true, Progress = progress, Token = token, Silent = true });
            }
            return true;
        }

        private async Task<bool> StopAutoguider(CancellationToken token, IProgress<ApplicationStatus> progress) {
            var result = await Mediator.Instance.RequestAsync(new StopGuiderMessage() { Token = token });            
            return result;
        }

        private async Task<bool> SelectNewGuideStar(CancellationToken token, IProgress<ApplicationStatus> progress) {
            progress.Report(new ApplicationStatus() { Status = "Select new Guidestar" });
            return await Mediator.Instance.RequestAsync(new AutoSelectGuideStarMessage() { Token = token });
        }

        private async Task<bool> ResumeAutoguider(CancellationToken token, IProgress<ApplicationStatus> progress) {

            var result = await Mediator.Instance.RequestAsync(new StartGuiderMessage() { Token = token });

            return result;
        }

        private async Task<bool> Settle(CancellationToken token, IProgress<ApplicationStatus> progress) {
            RemainingTime = TimeSpan.FromSeconds(Settings.MeridianFlipSettleTime);
            do {
                progress.Report(new ApplicationStatus() { Status = Locale.Loc.Instance["LblSettle"] + " " + RemainingTime.ToString(@"hh\:mm\:ss") });

                var delta = await Utility.Utility.Delay(1000, token);

                RemainingTime = TimeSpan.FromSeconds(RemainingTime.TotalSeconds - delta.TotalSeconds);

            } while (RemainingTime.TotalSeconds >= 1);
            return true;
        }

        private async Task<bool> DoMeridianFlip() {
            try {
                var token = _tokensource.Token;
                Steps = new AutomatedWorkflow();

                Steps.Add(new WorkflowStep("StopAutoguider", Locale.Loc.Instance["LblStopAutoguider"], () => StopAutoguider(token, _progress)));

                Steps.Add(new WorkflowStep("PassMeridian", Locale.Loc.Instance["LblPassMeridian"], () => PassMeridian(token, _progress)));
                Steps.Add(new WorkflowStep("Flip", Locale.Loc.Instance["LblFlip"], () => DoFilp(token, _progress)));
                if (Settings.RecenterAfterFlip) {
                    Steps.Add(new WorkflowStep("Recenter", Locale.Loc.Instance["LblRecenter"], () => Recenter(token, _progress)));
                }


                Steps.Add(new WorkflowStep("SelectNewGuideStar", Locale.Loc.Instance["LblSelectNewGuideStar"], () => SelectNewGuideStar(token, _progress)));
                Steps.Add(new WorkflowStep("ResumeAutoguider", Locale.Loc.Instance["LblResumeAutoguider"], () => ResumeAutoguider(token, _progress)));

                Steps.Add(new WorkflowStep("Settle", Locale.Loc.Instance["LblSettle"], () => Settle(token, _progress)));

                await Steps.Process();
            } catch (Exception ex) {
                Logger.Error(ex);
                await ResumeAutoguider(new CancellationToken(), _progress);
                Mediator.Instance.Request(new SetTelescopeTrackingMessage() { Tracking = true });
                return false;
            }
            return true;
        }

        private ICommand _cancelCommand;
        public ICommand CancelCommand {
            get {
                return _cancelCommand;
            }
            set {
                _cancelCommand = value;
            }

        }
    }

    public class AutomatedWorkflow : BaseINPC, ICollection<WorkflowStep> {
        private AsyncObservableLimitedSizedStack<WorkflowStep> _internalStack;

        public AutomatedWorkflow() {
            _internalStack = new AsyncObservableLimitedSizedStack<WorkflowStep>(int.MaxValue);
        }

        public int Count {
            get {
                return _internalStack.Count;
            }
        }

        public bool IsReadOnly {
            get {
                return _internalStack.IsReadOnly;
            }
        }

        public void Add(WorkflowStep item) {
            _internalStack.Add(item);
        }

        public void Clear() {
            _internalStack.Clear();
        }

        public bool Contains(WorkflowStep item) {
            return _internalStack.Contains(item);
        }

        public void CopyTo(WorkflowStep[] array, int arrayIndex) {
            _internalStack.CopyTo(array, arrayIndex);
        }

        public IEnumerator<WorkflowStep> GetEnumerator() {
            return _internalStack.GetEnumerator();
        }

        public bool Remove(WorkflowStep item) {
            return _internalStack.Remove(item);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return _internalStack.GetEnumerator();
        }

        public async Task<bool> Process() {
            var success = true;

            var item = _internalStack.First();
            do {
                ActiveStep = item.Value;
                success = await ActiveStep.Process() && success;
                item = item.Next;
            }
            while (item != null);

            return success;
        }

        private WorkflowStep _activeStep;
        public WorkflowStep ActiveStep {
            get {
                return _activeStep;
            }
            set {
                _activeStep = value;
                RaisePropertyChanged();
            }
        }
    }


    public class WorkflowStep : BaseINPC {
        public WorkflowStep(string id, string title, Func<Task<bool>> action) {
            Id = id;
            Title = title;
            Action = action;
        }

        private string _id;
        public string Id {
            get {
                return _id;
            }
            set {
                _id = value;
                RaisePropertyChanged();
            }
        }

        private string _title;
        public string Title {
            get {
                return _title;
            }
            set {
                _title = value;
                RaisePropertyChanged();
            }
        }

        private bool _finished;
        public bool Finished {
            get {
                return _finished;
            }
            set {
                _finished = value;
                RaisePropertyChanged();
            }
        }

        private Func<Task<bool>> _action;
        public Func<Task<bool>> Action {
            get {
                return _action;
            }
            set {
                _action = value;
                RaisePropertyChanged();
            }
        }

        public async Task<bool> Process() {
            var success = await Action();
            Finished = success;
            return success;
        }

    }
}
