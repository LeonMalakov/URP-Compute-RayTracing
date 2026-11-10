using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Boil.RenderPipeline.RayTracing {
    public class RayTracingShadowsRenderPass : ScriptableRenderPass, IDisposable {
        private static class ShaderParams {
            public static readonly string KernelName = "CSMain";
            public static readonly Vector2Int NumThreads = new Vector2Int(8, 8);

            public static readonly int Result = Shader.PropertyToID("Result");
            public static readonly int ScreenToWorld = Shader.PropertyToID("_ScreenToWorld");
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


        public RayTracingShadowsRenderPass(RayTracingSettings settings) {
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
            
            const string passTitle = "RayTracing_Shadows";
            // Add a raster render pass to the render graph. The PassData type parameter determines
            // the type of the passData output variable.
            using (IComputeRenderGraphBuilder builder = renderGraph.AddComputePass(passTitle, out PassData passData)) {
                // UniversalResourceData contains all the texture references used by URP,
                // including the active color and depth textures of the camera.
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                /*UniversalRenderingData universalRenderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();*/
                URPCustom.RayTracingUniversalData rayTracingData = frameData.GetOrCreate<URPCustom.RayTracingUniversalData>();

                passData.CameraData = cameraData;
                
                // Create a temporary texture.
                RenderTextureDescriptor textureProperties =
                    new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0);
                textureProperties.enableRandomWrite = true;
                TextureHandle texture =
                    UniversalRenderer.CreateRenderGraphTexture(renderGraph, textureProperties, "ResultTex", false);

                rayTracingData.ShadowMap = texture;
                
                // Populate passData with the data needed by the rendering function
                // of the render pass.
                // Use the camera's active color texture
                // as the source texture for the copy operation.
                passData.ComputeShader = _settings.shadowsComputeShader;
                passData.KernelIndex = passData.ComputeShader.FindKernel(ShaderParams.KernelName);
                //passData.ActiveColorTexture = resourceData.activeColorTexture;
                passData.ResultTexture = texture;
                passData.GBuffer = resourceData.gBuffer;
                //passData.RayTracingData.CameraInstanceId = cameraData.camera.GetInstanceID();

                passData.RayTracingData = RayTracingPassCommon.ImportAndUseRayTracingData(dataStorage, renderGraph, builder);


                // Create a destination texture for the copy operation based on the settings,
                // such as dimensions, of the textures that the camera uses.
                // Set msaaSamples to 1 to get a non-multisampled destination texture.
                // Set depthBufferBits to 0 to ensure that the CreateRenderGraphTexture method
                // creates a color texture and not a depth texture.
                RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
                desc.msaaSamples = 1;
                desc.depthBufferBits = 0;

                // For demonstrative purposes, this sample creates a temporary destination texture.
                // UniversalRenderer.CreateRenderGraphTexture is a helper method
                // that calls the RenderGraph.CreateTexture method.
                // Using a RenderTextureDescriptor instance instead of a TextureDesc instance
                // simplifies your code.
                /*TextureHandle destination = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, desc, "CopyTexture", false);*/

                // Declare that this render pass uses the source texture as a read-only input.
                //builder.UseTexture(passData.ActiveColorTexture);
                builder.UseTexture(passData.ResultTexture, AccessFlags.Write);

                foreach (TextureHandle gBufferTexture in passData.GBuffer) {
                    builder.UseTexture(gBufferTexture);
                }

                // Declare that this render pass uses the temporary destination texture
                // as its color render target.
                // This is similar to cmd.SetRenderTarget prior to the RenderGraph API.
                //builder.SetRenderAttachment(destination, 0);

                // RenderGraph automatically determines that it can remove this render pass
                // because its results, which are stored in the temporary destination texture,
                // are not used by other passes.
                // For demonstrative purposes, this sample turns off this behavior to make sure
                // that render graph executes the render pass. 
                builder.AllowPassCulling(false);

                // Set the ExecutePass method as the rendering function that render graph calls
                // for the render pass. 
                // This sample uses a lambda expression to avoid memory allocations.
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
            cmd.SetComputeTextureParam(data.ComputeShader, data.KernelIndex, ShaderParams.Result, data.ResultTexture);
        }
    }
}