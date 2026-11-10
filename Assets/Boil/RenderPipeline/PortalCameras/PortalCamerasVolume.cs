using System;
using UnityEngine;
using URPCustom;

namespace Boil.RenderPipeline.PortalCameras {
    public class PortalCamerasVolume : MonoBehaviour {
       
        
        private void OnEnable() {
            URPPortalCamerasBridge.Buffers = new URPPortalCamerasBridge.BufferCollection();
            URPPortalCamerasBridge.Active = true;
        }

        private void OnDisable() {
            URPPortalCamerasBridge.Active = false;
            URPPortalCamerasBridge.Buffers.Dispose();
            URPPortalCamerasBridge.Buffers = null;
        }
    }
}