using System;
using UnityEngine.Rendering.Universal;

namespace Boil.RenderPipeline.RayTracing {
    public class RayTracingRenderFeature : ScriptableRendererFeature {
        public RayTracingSettings settings = new();
        
        private RayTracingShadowsRenderPass _shadowsPass;
        private RayTracingReflectionsRenderPass _reflectionsPass;
        private RayTracingGlobalIlluminationRenderPass _globalIlluminationPass;
        private RayTracingDebugOutputTexturePass _debugPass;
        
        public override void Create() {
            RenderPassEvent renderEvent = RenderPassEvent.BeforeRenderingDeferredLights;
            
            _shadowsPass = new RayTracingShadowsRenderPass(settings) {
                renderPassEvent = renderEvent
            };
            
            _reflectionsPass = new RayTracingReflectionsRenderPass(settings) {
                renderPassEvent = renderEvent
            };

            _globalIlluminationPass = new RayTracingGlobalIlluminationRenderPass(settings) {
                renderPassEvent = renderEvent
            };
            
            _debugPass = new RayTracingDebugOutputTexturePass() {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            
            _shadowsPass?.Dispose();
            _shadowsPass = null;
            
            _reflectionsPass?.Dispose();
            _reflectionsPass = null;
            
            _globalIlluminationPass?.Dispose();
            _globalIlluminationPass = null;
        }

        private void OnDisable() {
            Dispose(true);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            if (IsValid()) {
                renderer.EnqueuePass(_shadowsPass);
                renderer.EnqueuePass(_reflectionsPass);
                renderer.EnqueuePass(_globalIlluminationPass);
                
                if (settings.debugOutputTexture) {
                    renderer.EnqueuePass(_debugPass);
                }
            }
        }

        private bool IsValid() {
            return _shadowsPass != null
                   && settings.shadowsComputeShader != null
                   && _reflectionsPass != null
                   && settings.reflectionsComputeShader != null
                   && _globalIlluminationPass != null
                   && settings.globalIlluminationComputeShader != null;
        }
    }
}