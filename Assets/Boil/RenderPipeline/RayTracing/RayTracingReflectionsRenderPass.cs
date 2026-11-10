using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Boil.RenderPipeline.RayTracing {
    public class RayTracingReflectionsRenderPass : ScriptableRenderPass, IDisposable {
        private static class ShaderParams {
            public static readonly string KernelName = "CSMain";
            public static readonly Vector2Int NumThreads = new Vector2Int(8, 8);

            public static readonly int Result = Shader.PropertyToID("Result");
            public static readonly int ScreenToWorld = Shader.PropertyToID("_ScreenToWorld");
            public static readonly int WorldSpaceCameraPos = Shader.PropertyToID("_WorldSpaceCameraPos");
        }
        
        private class PassData {
            internal UniversalCameraData CameraData;
            
            internal ComputeShader ComputeShader;
            internal int KernelIndex;

            internal TextureHandle ResultTexture;

            //internal TextureHandle ActiveColorTexture;
            public TextureHandle[] GBuffer;
          
            public RayTracingPassData RayTracingData;
        }

        private readonly RayTracingSettings _settings;


        public RayTracingReflectionsRenderPass(RayTracingSettings settings) {
            _settings = settings;
        }

        public void Dispose() {
           
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
            if (RayTracingVolume.Instance == null || RayTracingVolume.Instance.DataStorage == null) {
                return;
            }
            
            RayTracingDataStorage dataStorage = RayTracingVolume.Instance.DataStorage;
            
            if (!dataStorage.Ready) {
                return;
            }
            
            const string passTitle = "RayTracing_Reflections";
            // Add a raster render pass to the render graph. The PassData type parameter determines
            // the type of the passData output variable.
            using (IComputeRenderGraphBuilder builder = renderGraph.AddComputePass(passTitle, out PassData passData)) {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                URPCustom.RayTracingUniversalData rayTracingData = frameData.GetOrCreate<URPCustom.RayTracingUniversalData>();

                passData.CameraData = cameraData;
                
                // Create a temporary texture.
                RenderTextureDescriptor textureProperties =
                    new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0);
                textureProperties.enableRandomWrite = true;
                textureProperties.useMipMap = true;
                textureProperties.autoGenerateMips = false;
                TextureHandle texture =
                    UniversalRenderer.CreateRenderGraphTexture(renderGraph, textureProperties, "RayTracing_ReflectionsMap", false);

                rayTracingData.ReflectionsMap = texture;
                
                passData.ComputeShader = _settings.reflectionsComputeShader;
                passData.KernelIndex = passData.ComputeShader.FindKernel(ShaderParams.KernelName);
                passData.ResultTexture = texture;
                passData.GBuffer = resourceData.gBuffer;

                passData.RayTracingData = RayTracingPassCommon.ImportAndUseRayTracingData(dataStorage, renderGraph, builder);
                
                RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
                desc.msaaSamples = 1;
                desc.depthBufferBits = 0;
                
                builder.UseTexture(passData.ResultTexture, AccessFlags.Write);

                foreach (TextureHandle gBufferTexture in passData.GBuffer) {
                    builder.UseTexture(gBufferTexture);
                }
                
                builder.AllowPassCulling(false);
                
                builder.SetRenderFunc((PassData data, ComputeGraphContext context) => ExecutePass(data, context));
            }
        }

        private static void ExecutePass(PassData data, ComputeGraphContext context) {
            // Records a rendering command to copy, or blit, the contents of the source texture
            // to the color render target of the render pass.
            /*Blitter.BlitTexture(context.cmd, data.ActiveColorTexture,
                new Vector4(1, 1, 0, 0), 0, false);*/

            // context.cmd.SetComputeBufferParam(data.ComputeShader, ShaderParams.KernelIndex, ShaderParams.Mask, data.Mask);

            URPCustom.URPScreenToWorldCalculation.Calculate(data.CameraData);
            
            RayTracingPassCommon.SetupGBufferData(context, data.ComputeShader, data.KernelIndex, data.GBuffer);
            RayTracingPassCommon.SetupRayTracingData(context, data.ComputeShader, data.KernelIndex, ref data.RayTracingData);
            SetupData(data, context);

            int threadGroupX = Screen.width / ShaderParams.NumThreads.x;
            int threadGroupY = Screen.height / ShaderParams.NumThreads.y;
            context.cmd.DispatchCompute(data.ComputeShader, data.KernelIndex, threadGroupX, threadGroupY, 1);
        }

        private static void SetupData(PassData data, ComputeGraphContext context) {
            ComputeCommandBuffer cmd = context.cmd;
            cmd.SetComputeMatrixArrayParam(data.ComputeShader, ShaderParams.ScreenToWorld, URPCustom.URPScreenToWorldCalculation.ScreenToWorld);
            cmd.SetComputeFloatParams(data.ComputeShader, ShaderParams.WorldSpaceCameraPos, data.CameraData.worldSpaceCameraPos.x, data.CameraData.worldSpaceCameraPos.y, data.CameraData.worldSpaceCameraPos.z);
            cmd.SetComputeTextureParam(data.ComputeShader, data.KernelIndex, ShaderParams.Result, data.ResultTexture);
        }
    }
}