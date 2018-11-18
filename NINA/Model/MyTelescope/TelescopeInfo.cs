﻿using NINA.Utility;
using NINA.Utility.Astrometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NINA.Model.MyTelescope {

    internal class TelescopeInfo : DeviceInfo {
        private double siderealTime;

        public double SiderealTime {
            get {
                return siderealTime;
            }
            set {
                siderealTime = value;
                RaisePropertyChanged();
            }
        }

        private double rightAscension;

        public double RightAscension {
            get {
                return rightAscension;
            }
            set {
                rightAscension = value;
                RaisePropertyChanged();
            }
        }

        private double declination;

        public double Declination {
            get {
                return declination;
            }
            set {
                declination = value;
                RaisePropertyChanged();
            }
        }

        private double siteLatitude;

        public double SiteLatitude {
            get {
                return siteLatitude;
            }
            set {
                siteLatitude = value;
                RaisePropertyChanged();
            }
        }

        private double siteLongitude;

        public double SiteLongitude {
            get {
                return siteLongitude;
            }
            set {
                siteLongitude = value;
                RaisePropertyChanged();
            }
        }

        private double siteElevation;

        public double SiteElevation {
            get {
                return siteElevation;
            }
            set {
                siteElevation = value;
                RaisePropertyChanged();
            }
        }

        private string rightAscensionString;

        public string RightAscensionString {
            get {
                return rightAscensionString;
            }
            set {
                rightAscensionString = value;
                RaisePropertyChanged();
            }
        }

        private string declinationString;

        public string DeclinationString {
            get {
                return declinationString;
            }
            set {
                declinationString = value;
                RaisePropertyChanged();
            }
        }

        private Coordinates coordinates;

        public Coordinates Coordinates {
            get {
                return coordinates;
            }
            set {
                coordinates = value;
                RaisePropertyChanged();
            }
        }

        private double timeToMeridianFlip;

        public double TimeToMeridianFlip {
            get {
                return timeToMeridianFlip;
            }
            set {
                timeToMeridianFlip = value;
                RaisePropertyChanged();
            }
        }

        private string altitudeString;

        public string AltitudeString {
            get { return altitudeString; }
            set { altitudeString = value; RaisePropertyChanged(); }
        }

        private string azimuthString;

        public string AzimuthString {
            get { return azimuthString; }
            set { azimuthString = value; RaisePropertyChanged(); }
        }

        private string siderealTimeString;

        public string SiderealTimeString {
            get { return siderealTimeString; }
            set { siderealTimeString = value; RaisePropertyChanged(); }
        }

        private string hoursToMeridianString;

        public string HoursToMeridianString {
            get { return hoursToMeridianString; }
            set { hoursToMeridianString = value; RaisePropertyChanged(); }
        }

        private bool atPark;

        public bool AtPark {
            get { return atPark; }
            set { atPark = value; RaisePropertyChanged(); }
        }

        private bool tracking;

        public bool Tracking {
            get { return tracking; }
            set { tracking = value; RaisePropertyChanged(); }
        }
    }
}