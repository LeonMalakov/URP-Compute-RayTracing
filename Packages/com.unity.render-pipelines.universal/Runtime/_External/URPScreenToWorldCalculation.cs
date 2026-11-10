using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

namespace URPCustom {
    public static class URPScreenToWorldCalculation {
        public static readonly Matrix4x4[] ScreenToWorld = new Matrix4x4[2];
        
        private static float _renderWidth;
        private static float _renderHeight;

        public static void Calculate(UniversalCameraData cameraData) {
            CalculateRenderSize(cameraData);
            GetScreenToWorldMatrixInternal(cameraData);
        }
        
        private static void CalculateRenderSize(UniversalCameraData cameraData) {
            Camera camera = cameraData.camera;

            Vector2Int cameraTargetSizeCopy = new Vector2Int(cameraData.cameraTargetDescriptor.width,
                cameraData.cameraTargetDescriptor.height);

#if ENABLE_VR && ENABLE_XR_MODULE
            // Use eye texture's scaled width and height as screen params when XR is enabled
            if (cameraData.xr.enabled) {
                _renderWidth = camera.allowDynamicResolution
                    ? cameraData.xr.renderTargetScaledWidth
                    : cameraTargetSizeCopy.x;
                _renderHeight = camera.allowDynamicResolution
                    ? cameraData.xr.renderTargetScaledHeight
                    : cameraTargetSizeCopy.y;
            } else
#endif
            {
                // Support for dynamic resolution.
                _renderWidth = camera.allowDynamicResolution
                    ? Mathf.CeilToInt(ScalableBufferManager.widthScaleFactor * cameraTargetSizeCopy.x)
                    : cameraTargetSizeCopy.x;
                _renderHeight = camera.allowDynamicResolution
                    ? Mathf.CeilToInt(ScalableBufferManager.heightScaleFactor * cameraTargetSizeCopy.y)
                    : cameraTargetSizeCopy.y;
            }
        }

        private static Matrix4x4[] GetScreenToWorldMatrixInternal(UniversalCameraData cameraData) {
#if ENABLE_VR && ENABLE_XR_MODULE
            int eyeCount = cameraData.xr.enabled && cameraData.xr.singlePassEnabled ? 2 : 1;
#else
            int eyeCount = 1;
#endif

            Matrix4x4[] screenToWorld = ScreenToWorld; // deferred shaders expects 2 elements

            // pixel coordinates to NDC coordinates.
            Matrix4x4 screenToNDC = new Matrix4x4(
                new Vector4(2.0f / (float)_renderWidth, 0.0f, 0.0f, 0.0f),
                new Vector4(0.0f, 2.0f / (float)_renderHeight, 0.0f, 0.0f),
                new Vector4(0.0f, 0.0f, 1.0f, 0.0f),
                new Vector4(-1.0f, -1.0f, 0.0f, 1.0f)
            );

            if (DeferredConfig.IsOpenGL) {
                // We need to manunally adjust z in NDC space from [0; 1] (storage in depth texture) to [-1; 1].
                Matrix4x4 renormalizeZ = new Matrix4x4(
                    new Vector4(1.0f, 0.0f, 0.0f, 0.0f),
                    new Vector4(0.0f, 1.0f, 0.0f, 0.0f),
                    new Vector4(0.0f, 0.0f, 2.0f, 0.0f),
                    new Vector4(0.0f, 0.0f, -1.0f, 1.0f)
                );

                screenToNDC = renormalizeZ * screenToNDC;
            }

            for (int eyeIndex = 0; eyeIndex < eyeCount; eyeIndex++) {
                Matrix4x4 view = cameraData.GetViewMatrix(eyeIndex);
                Matrix4x4 gpuProj = cameraData.GetGPUProjectionMatrix(false, eyeIndex);

                screenToWorld[eyeIndex] = Matrix4x4.Inverse(gpuProj * view) * screenToNDC;
            }

            return screenToWorld;
        }
    }
}