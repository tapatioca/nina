﻿using NINA.Model;
using NINA.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace NINA.ViewModel {
    class PolarAlignmentVM : BaseVM {
        TelescopeModel _telescope;
        public TelescopeModel Telescope {
            get {
                return _telescope;
            }
            set {
                _telescope = value;
                RaisePropertyChanged();
            }
        }

        private PlatesolveVM _platesolveVM;
        public PlatesolveVM PlatesolveVM {
            get {
                return _platesolveVM;
            }

            set {
                _platesolveVM = value;
                RaisePropertyChanged();
            }
        }

        private double _rotation;
        public double Rotation {
            get {
                return _rotation;
            }
            set {
                _rotation = value;
                RaisePropertyChanged();
            }
        }

        public string HourAngleTime {
            get {
                return _hourAngleTime;
            }

            set {
                _hourAngleTime = value;
                RaisePropertyChanged();
            }
        }

        private IAsyncCommand _measureAzimuthErrorCommand;
        public IAsyncCommand MeasureAzimuthErrorCommand {
            get {
                return _measureAzimuthErrorCommand;
            }
            private set {
                _measureAzimuthErrorCommand = value;
                RaisePropertyChanged();
            }
        }

        private IAsyncCommand _measureAltitudeErrorCommand;
        public IAsyncCommand MeasureAltitudeErrorCommand {
            get {
                return _measureAltitudeErrorCommand;
            }
            private set {
                _measureAltitudeErrorCommand = value;
                RaisePropertyChanged();
            }
        }

        

        private string _hourAngleTime;
        

        DispatcherTimer _updateValues;

        public PolarAlignmentVM() {
            _updateValues = new DispatcherTimer();
            _updateValues.Interval = TimeSpan.FromSeconds(1);
            _updateValues.Tick += _updateValues_Tick;
            _updateValues.Start();

            MeasureAzimuthErrorCommand = new AsyncCommand<bool>(() => measurePolarError(Direction.Azimuth));
            MeasureAltitudeErrorCommand = new AsyncCommand<bool>(() => measurePolarError(Direction.Altitude));
        }


        public enum Direction {
            Altitude,
            Azimuth
        }

        public enum Hemisphere {
            North, 
            South
        }

        private string _polarErrorStatus;
        public string PolarErrorStatus {
            get {
                return _polarErrorStatus;
            }

            set {
                _polarErrorStatus = value;
                RaisePropertyChanged();
            }
        }

        private async Task<bool> measurePolarError(Direction direction, Hemisphere hem = Hemisphere.North) {

            double poleErr = await calculatePoleError();

            if(double.IsNaN(poleErr)) {
                /* something went wrong */
                PolarErrorStatus = "Something went wrong.";
                return false;
            }

            string msg = "";

            if(direction == Direction.Altitude) {                
                if(hem == Hemisphere.North) {
                    //if east
                    if (poleErr < 0) {
                        msg = poleErr + "° too low";
                    } else {
                        msg = poleErr + "° too high";
                    }
                } else {
                    //if east
                    if (poleErr < 0) {
                        msg = poleErr + "° too high";
                    } else {
                        msg = poleErr + "° too low";
                    }
                }
                
            } else if(direction == Direction.Azimuth) {
                //if northern
                if(hem == Hemisphere.North) {
                    if (poleErr < 0) {
                        msg = poleErr + "° too east";
                    } else {
                        msg = poleErr + "° too west";
                    }
                } else {
                    if (poleErr < 0) {
                        msg = poleErr + "° too west";
                    } else {
                        msg = poleErr + "° too east";
                    }
                }
                
            }

            PolarErrorStatus = msg;

            /*  Altitude
             *      Northern
             *          East side
             *              poleError < 0 -> too low
             *              poleError > 0 -> too high
             *  Azimuth
             *      Northern
             *          South side
             *              poleError < 0 -> too east
             *              poleError > 0 -> too west
             */

            return true;
        }

        private async Task<double> calculatePoleError() {
            double movement = 0.5d;

            PolarErrorStatus = "Solving image...";

            await PlatesolveVM.blindSolveWithCapture();

            PlateSolving.PlateSolveResult startSolveResult = PlatesolveVM.PlateSolveResult;
            if(!startSolveResult.Success) {
                return double.NaN;
            }

            Coordinates startSolve = new Coordinates(startSolveResult.Ra, startSolveResult.Dec);
            startSolve = startSolve.transformToJNOW();

            Coordinates startPosition = new Coordinates(Telescope.RightAscension, Telescope.Declination);


            Coordinates targetPosition = new Coordinates(startPosition.RA - movement, startPosition.Dec);
            PolarErrorStatus = "Slewing...";
            Telescope.slewToCoordinates(targetPosition.RA, targetPosition.Dec);

            PolarErrorStatus = "Settling...";
            await Task.Delay(3000);

            PolarErrorStatus = "Solving image...";

            await PlatesolveVM.blindSolveWithCapture();
            PlateSolving.PlateSolveResult targetSolveResult = PlatesolveVM.PlateSolveResult;
            if (!targetSolveResult.Success) {
                return double.NaN;
            }

            Coordinates targetSolve = new Coordinates(targetSolveResult.Ra, targetSolveResult.Dec);
            targetSolve = targetSolve.transformToJNOW();

            PolarErrorStatus = "Slewing back to origin...";
            Telescope.Telescope.SlewToCoordinates(startPosition.RA, startPosition.Dec);


            var decError = startSolve.Dec - targetSolve.Dec;
            // Calculate pole error
            var poleError = 3.81 * 3600.0 * decError / (4 * movement * Math.Cos(ToRadians(startPosition.Dec)));
            // Convert pole error from arcminutes to degrees
            poleError = poleError / 60.0;
            return poleError;
        }


        public static double ToRadians(double val) {
            return (Math.PI / 180) * val;
        }

        public class Coordinates {
            public double RA;
            public double Dec;
            public Epoch Epoch;

            public Coordinates(double ra, double dec, Epoch epoch = Epoch.J2000) {
                this.RA = ra;
                this.Dec = dec;
                this.Epoch = epoch;
            }

            public Coordinates transformToJNOW() {                
                var NOVAS31 = Utility.Utility.NOVAS31;
                double[] vector = new double[4];
                NOVAS31.RaDec2Vector(RA, Dec, 1000, ref vector);
                double[] translatedvector = new double [4];

                var util = Utility.Utility.AstroUtils;
                var jd = util.JulianDateUtc;

                NOVAS31.Precession(2451545.0, vector, jd, ref translatedvector);
                double newRA = 0.0, newDec = 0.0;
                NOVAS31.Vector2RaDec(translatedvector, ref newRA, ref newDec);

                return new Coordinates(newRA, newDec, Epoch.JNOW);
            }

        }

        public enum Epoch {
            J2000,
            JNOW
        }




        private void _updateValues_Tick(object sender, EventArgs e) {
            if(Telescope.Connected) {
                var ascomutil = Utility.Utility.AscomUtil;


                var polaris = new Coordinates(ascomutil.HMSToHours("02:31:49.09"), ascomutil.DMSToDegrees("89:15:50.8"));

                var polarisnow = polaris.transformToJNOW();

                /*var util = Utility.Utility.AstroUtils;
                var jd = util.JulianDateUtc;

                //J2000 Coordinates
                var polarisRA = ascomutil.HMSToHours("02:31:49.09");
                var polarisDec = ascomutil.DMSToDegrees("89:15:50.8");

                var NOVAS31 = Utility.Utility.NOVAS31;

                double[] coords = new double[4];
                double[] translatedcoords = new double[4];
                NOVAS31.RaDec2Vector(polarisRA, polarisDec, 2.738e+7, ref coords);
                //Convert J2000 to current coordinates
                NOVAS31.Precession(2451545.0, coords, jd, ref translatedcoords);
                NOVAS31.Vector2RaDec(translatedcoords, ref polarisRA, ref polarisDec);*/

                var hour_angle = Telescope.SiderealTime - polarisnow.RA;

                Rotation = -(hour_angle / 24) * 360;
                HourAngleTime = ascomutil.HoursToHMS(hour_angle);            
            }
        }
    }
}
