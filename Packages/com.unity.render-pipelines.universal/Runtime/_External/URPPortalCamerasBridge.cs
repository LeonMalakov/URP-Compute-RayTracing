using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPCustom {
    public static class URPPortalCamerasBridge {
        public const string TEXTURE_PREFIX = "PortalCameras_";
        public static readonly TagHandle PortalMaskCameraTag = TagHandle.GetExistingTag("PortalMaskCamera");
        public static readonly TagHandle PortalCameraTag = TagHandle.GetExistingTag("PortalCamera");
        
        public class BufferCollection : IDisposable {
            // Текстуры камеры маски порталов.
            public RTHandle PortalMask;
            public RTHandle PortalMaskDepth;

            // Текстуры портальных камер + main камеры.
            public RTHandle NormalsTexture;
            public RTHandle[] GBufferRTHandles;
            public RTHandle DepthTexture;
            public RTHandle DepthAttachment;
            
            // Временные текстуры для копирования.
            public RTHandle DrawDepthTexture;
            public RTHandle DrawDepthTempTexture;

            public readonly List<Vector4> CamerasZBufferParams = new();

            public void Dispose() {
                PortalMask?.Release();
                PortalMaskDepth?.Release();
                
                NormalsTexture?.Release();
                DepthTexture?.Release();
                DepthAttachment?.Release();
                
                DrawDepthTexture?.Release();
                DrawDepthTempTexture?.Release();
                
                if (GBufferRTHandles != null) {
                    for (int i = 0; i < GBufferRTHandles.Length; i++) {
                        GBufferRTHandles[i]?.Release();
                    }
                }
            }
        }

        public static bool Active;
        public static BufferCollection Buffers;
    }

    public static class URPPortalCamerasBridgeUtility {
        public static int GetCameraIndex(UniversalCameraData cameraData) {
            int priorityInt = Mathf.RoundToInt(cameraData.camera.depth);
            
            return priorityInt switch {
                1 => 1, // Portal Camera
                2 => 0, // Main Camera,
                _ => -1 // Other (PortalMaskCamera)
            };
        }
        
        public static void TrySetCameraZBufferParams(this URPPortalCamerasBridge.BufferCollection buffers, UniversalCameraData cameraData, Vector4 zBufferParams) {
            int cameraIndex = GetCameraIndex(cameraData);

            if (cameraIndex < 0) {
                return;
            }
            
            if(buffers.CamerasZBufferParams.Count <= cameraIndex) {
                // Expand the list to accommodate the camera index
                while (buffers.CamerasZBufferParams.Count <= cameraIndex) {
                    buffers.CamerasZBufferParams.Add(Vector4.zero);
                }
            }

            buffers.CamerasZBufferParams[cameraIndex] = zBufferParams;
        }
    }
}