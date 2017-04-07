﻿using NINA.Model.MyCamera;
using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;

namespace ZWOptical.ASISDK {
    public static class ASICameras {

        private static readonly ASICamera[] _cameras = new ASICamera[16];

        public static int Count {
            get { return ASICameraDll.GetNumOfConnectedCameras(); }
        }

        public static ASICamera GetCamera(int cameraId) {
            if (cameraId >= Count || cameraId < 0)
                throw new IndexOutOfRangeException();

            return _cameras[cameraId] ?? (_cameras[cameraId] = new ASICamera(cameraId));
        }
    }

}