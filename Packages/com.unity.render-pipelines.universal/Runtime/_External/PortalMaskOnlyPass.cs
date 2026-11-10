using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace URPCustom {
    public class PortalMaskOnlyPass : ScriptableRenderPass
    {
        internal List<ShaderTagId> shaderTagIds { get; set; }
        
        private FilteringSettings m_FilteringSettings;

        // Statics
        private static readonly List<ShaderTagId> DefaultShaderTagIds = new() {
            new ShaderTagId("PortalMask")
        };
        
        public PortalMaskOnlyPass(RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask)
        {
            profilingSampler = ProfilingSampler.Get(URPProfileId.DrawDepthNormalPrepass);
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            renderPassEvent = evt;
            useNativeRenderPass = false;
            this.shaderTagIds = DefaultShaderTagIds;
        }
        
        private static void ExecutePass(RasterCommandBuffer cmd, PassData passData, RendererList rendererList)
        {            
            // Draw
            cmd.DrawRendererList(rendererList);
        }
        
        /// <inheritdoc/>
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
            {
                throw new ArgumentNullException("cmd");
            }

            // This needs to be reset as the renderer might change this in runtime (UUM-36069)
            shaderTagIds = DefaultShaderTagIds;
        }

        /// <summary>
        /// Shared pass data
        /// </summary>
        private class PassData
        {
            internal TextureHandle cameraDepthTexture;
            internal TextureHandle portalMaskTexture;
            internal RendererListHandle rendererList;
        }

        private RendererListParams InitRendererListParams(UniversalRenderingData renderingData, UniversalCameraData cameraData, UniversalLightData lightData)
        {
            var sortFlags = cameraData.defaultOpaqueSortFlags;
            var drawSettings = RenderingUtils.CreateDrawingSettings(this.shaderTagIds, renderingData, cameraData, lightData, sortFlags);
            drawSettings.perObjectData = PerObjectData.None;
            return new RendererListParams(renderingData.cullResults, drawSettings, m_FilteringSettings);
        }

        internal void Render(RenderGraph renderGraph, ContextContainer frameData, TextureHandle portalMaskTexture, TextureHandle cameraDepthTexture, TextureHandle renderingLayersTexture, uint batchLayerMask, bool setGlobalDepth, bool setGlobalTextures)
        {
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData, profilingSampler))
            {
                passData.portalMaskTexture = portalMaskTexture;
                builder.SetRenderAttachment(portalMaskTexture, 0, AccessFlags.Write);
                passData.cameraDepthTexture = cameraDepthTexture;
                builder.SetRenderAttachmentDepth(cameraDepthTexture, AccessFlags.Write);

                var param = InitRendererListParams(renderingData, cameraData, lightData);
                param.filteringSettings.batchLayerMask = batchLayerMask;
                passData.rendererList = renderGraph.CreateRendererList(param);
                builder.UseRendererList(passData.rendererList);
                if (cameraData.xr.enabled)
                    builder.EnableFoveatedRasterization(cameraData.xr.supportsFoveatedRendering && cameraData.xrUniversal.canFoveateIntermediatePasses);
                
                // Required here because of RenderingLayerUtils.SetupProperties
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    //RenderingLayerUtils.SetupProperties(context.cmd, data.maskSize);
                    ExecutePass(context.cmd, data, data.rendererList);
                });
            }
        }
    }
}
