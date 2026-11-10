using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Boil.RenderPipeline.ScreenSpaceReflections {
    public class SSRRendererFeature : ScriptableRendererFeature {
        [System.Serializable]
        public class SSRSettings {
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingDeferredLights;
            public string globalTextureName = "_SSRTexture";
            public RenderTextureFormat textureFormat = RenderTextureFormat.DefaultHDR;
            public FilterMode filterMode = FilterMode.Bilinear;
            public bool enableDebugCopyToCameraTarget = false; // если нужно визуально проверить содержимое
        }

        public SSRSettings settings = new SSRSettings();

        private SSRRenderPass _pass;

        public override void Create() {
            _pass = new SSRRenderPass(settings) {
                renderPassEvent = settings.renderPassEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            // передаём camera color target, чтобы можно было, например, сделать initial blit (опционально)
            _pass.Setup(renderer.cameraColorTargetHandle);
            
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            if (_pass != null)
                _pass.Dispose();
        }

        // ---------------------------
        class SSRRenderPass : ScriptableRenderPass {
            SSRSettings _settings;
            RenderTargetIdentifier _cameraColorTarget;
            RenderTexture _ssrRT = null;
            int _width = 0;
            int _height = 0;
            int _msaa = 1;

            int _globalTextureId;

            public SSRRenderPass(SSRSettings settings) {
                _settings = settings;
                _globalTextureId = Shader.PropertyToID(_settings.globalTextureName);
            }

            // called from AddRenderPasses
            public void Setup(RenderTargetIdentifier cameraColorTarget) {
                _cameraColorTarget = cameraColorTarget;
            }

            // called before Execute; use it to allocate / reconfigure RT
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) {
                Camera camera = renderingData.cameraData.camera;
                int w = camera.pixelWidth;
                int h = camera.pixelHeight;
                int msaa = Mathf.Max(1, renderingData.cameraData.cameraTargetDescriptor.msaaSamples);

                // recreate RT if size changed or null
                if (_ssrRT == null || _width != w || _height != h || _msaa != msaa) {
                    // release old
                    if (_ssrRT != null) {
                        _ssrRT.Release();
                        Object.DestroyImmediate(_ssrRT);
                        _ssrRT = null;
                    }

                    RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
                    desc.depthBufferBits = 0; // we only need color
                    desc.msaaSamples =
                        1; // render SSR at single-sample; msaa isn't necessary for full-screen reflection buffer
                    desc.colorFormat = (UnityEngine.RenderTextureFormat)_settings.textureFormat;

                    _ssrRT = new RenderTexture(desc) {
                        name = "SSRTexture",
                        filterMode = _settings.filterMode,
                        useMipMap = false,
                        autoGenerateMips = false,
                    };
                    _ssrRT.Create();

                    _width = w;
                    _height = h;
                    _msaa = desc.msaaSamples;
                }

                // We don't allocate with cmd here because we created persistent RT.
                // But set global texture so shaders can read it even before we write it in Execute.
                cmd.SetGlobalTexture(_globalTextureId, _ssrRT);
            }

            // main execution — here we clear the RT (or write initial debug copy).
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
                if (_ssrRT == null)
                    return;

                CommandBuffer cmd = CommandBufferPool.Get("SSRCreateTexture");

                // update camera matrices as globals (shader expects these)
                Camera cam = renderingData.cameraData.camera;
                Matrix4x4 camToWorld = cam.cameraToWorldMatrix;
                Matrix4x4 invProj = cam.projectionMatrix.inverse;
                Matrix4x4 proj = cam.projectionMatrix;

                cmd.SetGlobalMatrix("_CameraToWorld", camToWorld);
                cmd.SetGlobalMatrix("_CameraInverseProjection", invProj);
                cmd.SetGlobalMatrix("unity_CameraProjection", proj);

                // bind our persistent RT as render target and clear it to black (or transparent)
                RenderTargetIdentifier rtId = new RenderTargetIdentifier(_ssrRT);
                cmd.SetRenderTarget(rtId);
                cmd.ClearRenderTarget(false, true, Color.clear);

                // optional: for debug, copy current camera color into SSR buffer so you can see it
                if (_settings.enableDebugCopyToCameraTarget) {
                    // NOTE: _cameraColorTarget was set in Setup
                    cmd.Blit(_cameraColorTarget, rtId);
                }

                // Make sure shaders can read it in subsequent passes
                cmd.SetGlobalTexture(_globalTextureId, rtId);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            // Cleanup after all camera rendering is done (feature Dispose handles persistent RT)
            public override void OnCameraCleanup(CommandBuffer cmd) {
                // do not release _ssrRT here — we own it as persistent resource and will release on Dispose()
                // but still ensure global texture points to something valid; if RT destroyed, set black.
                // (nothing required here)
            }

            public void Dispose() {
                if (_ssrRT != null) {
                    _ssrRT.Release();
                    Object.DestroyImmediate(_ssrRT);
                    _ssrRT = null;
                }
            }
        }
    }
}