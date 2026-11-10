using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace URPCustom {
    public class SetupDepthFromPortalMaskPass : ScriptableRenderPass
    {
        private const int MAX_MATERIALS = 8;
        private const int WARM_UP_MATERIALS = 1;
        
        internal int ActiveCameraIndex { get; set; }
        
        private readonly CustomPassMaterialPool _materialPool;
        
        /// <summary>
        /// Shader resource ids used to communicate with the shader implementation
        /// </summary>
        static class ShaderConstants
        {
            public static readonly int PortalMaskTexture = Shader.PropertyToID("_PortalMaskTexture");
            public static readonly int ActiveCameraIndex = Shader.PropertyToID("_ActiveCameraIndex");
            public static readonly int _ZWriteShaderHandle = Shader.PropertyToID("_ZWrite");
        }
        
        public SetupDepthFromPortalMaskPass(RenderPassEvent evt, Shader copyDepthShader, bool copyToDepth = false, string customPassName = null)
        {
            profilingSampler = customPassName != null ? new ProfilingSampler(customPassName) : ProfilingSampler.Get(URPProfileId.CopyDepth);
            renderPassEvent = evt;
            _materialPool = new CustomPassMaterialPool(copyDepthShader, MAX_MATERIALS, WARM_UP_MATERIALS); 
        }

        /// <summary>
        /// Cleans up resources used by the pass.
        /// </summary>
        public void Dispose()
        {
            _materialPool.Dispose();
        }
        
        public void ResetUsage() {
            _materialPool.ResetUsage();
        }

        private class PassData
        {
            internal TextureHandle portalMaskTexture;
            internal UniversalCameraData cameraData;
            internal Material copyDepthMaterial;
            internal bool copyToDepth;
            internal bool isDstBackbuffer;
            internal int CameraIndex;
        }
        
        private static void ExecutePass(RasterCommandBuffer cmd, PassData passData, RTHandle portalMaskTexture)
        {
            var copyDepthMaterial = passData.copyDepthMaterial;
            var copyToDepth = passData.copyToDepth;

            if (copyDepthMaterial == null)
            {
                Debug.LogErrorFormat("Missing {0}. Copy Depth render pass will not execute. Check for missing reference in the renderer resources.", copyDepthMaterial);
                return;
            }
            using (new ProfilingScope(cmd, ProfilingSampler.Get(URPProfileId.CopyDepth)))
            {
                cmd.SetKeyword(ShaderGlobalKeywords._OUTPUT_DEPTH, copyToDepth);

                // We must perform a yflip if we're rendering into the backbuffer and we have a flipped source texture.
                bool yflip = passData.isDstBackbuffer && passData.cameraData.IsHandleYFlipped(portalMaskTexture);

                Vector2 viewportScale = portalMaskTexture.useScaling ? new Vector2(portalMaskTexture.rtHandleProperties.rtHandleScale.x, portalMaskTexture.rtHandleProperties.rtHandleScale.y) : Vector2.one;
                Vector4 scaleBias = yflip ? new Vector4(viewportScale.x, -viewportScale.y, 0, viewportScale.y) : new Vector4(viewportScale.x, viewportScale.y, 0, 0);

                // When we render to the backbuffer, we update the viewport to cover the entire screen just in case it hasn't been updated already.
                if (passData.isDstBackbuffer)
                    cmd.SetViewport(passData.cameraData.pixelRect);

                copyDepthMaterial.SetTexture(ShaderConstants.PortalMaskTexture, portalMaskTexture);
                copyDepthMaterial.SetInt(ShaderConstants.ActiveCameraIndex, passData.CameraIndex);
                copyDepthMaterial.SetFloat(ShaderConstants._ZWriteShaderHandle, copyToDepth ? 1.0f : 0.0f);
                copyDepthMaterial.SetInt(ShaderConstants.ActiveCameraIndex, passData.CameraIndex);
                Blitter.BlitTexture(cmd, portalMaskTexture, scaleBias, copyDepthMaterial, 0);
            }
        }
        
        public void Render(RenderGraph renderGraph, TextureHandle destination, TextureHandle portalMaskTexture, UniversalResourceData resourceData, UniversalCameraData cameraData, string passName = "Copy Depth")
        {
            Material material = _materialPool.GetNextMaterial();
                
            if(material == null) {
                return;
            }
            
            // Having a different pass name than profilingSampler.name is bad practice but this method was public before we cleaned up this naming 
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData, profilingSampler))
            {
                passData.copyDepthMaterial = material;
                passData.cameraData = cameraData;
                passData.copyToDepth = true;
                passData.isDstBackbuffer = false;
                passData.CameraIndex = ActiveCameraIndex;


                // Writes depth using custom depth output
                builder.SetRenderAttachmentDepth(destination, AccessFlags.WriteAll);
#if UNITY_EDITOR
                // binding a dummy color target as a workaround to an OSX issue in Editor scene view (UUM-47698).
                // Also required for preview camera rendering for grid drawn with builtin RP (UUM-55171).
                if (cameraData.isSceneViewCamera || cameraData.isPreviewCamera)
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
#endif
          

                passData.portalMaskTexture = portalMaskTexture;
                builder.UseTexture(portalMaskTexture, AccessFlags.Read);
                
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    ExecutePass(context.cmd, data, data.portalMaskTexture);
                });
            }
        }
    }
}
