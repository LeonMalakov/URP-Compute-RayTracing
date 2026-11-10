using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Boil.RenderPipeline.RayTracing {
    public class RayTracingDebugOutputTexturePass : ScriptableRenderPass {
        public RayTracingDebugOutputTexturePass() {
            //The pass will read the current color texture. That needs to be an intermediate texture. It's not supported to use the BackBuffer as input texture. 
            //By setting this property, URP will automatically create an intermediate texture. 
            //It's good practice to set it here and not from the RenderFeature. This way, the pass is selfcontaining and you can use it to directly enqueue the pass from a monobehaviour without a RenderFeature.
            requiresIntermediateTexture = true;
        }

        // This is where the renderGraph handle can be accessed.
        // Each ScriptableRenderPass can use the RenderGraph handle to add multiple render passes to the render graph
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
            const string passName = "RayTracing Debug Output";

            // UniversalResourceData contains all the texture handles used by the renderer, including the active color and depth textures
            // The active color and depth textures are the main color and depth buffers that the camera renders into
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            // The destination texture is created here, 
            // the texture is created with the same dimensions as the active color texture
            //var source = resourceData.activeColorTexture;

            /*var destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.name = $"CameraColor-{passName}";
            destinationDesc.clearBuffer = false;*/
            //TextureHandle destination = renderGraph.CreateTexture(destinationDesc);
            
            URPCustom.RayTracingUniversalData rayTracingData = frameData.GetOrCreate<URPCustom.RayTracingUniversalData>();

            if (!rayTracingData.ShadowMap.IsValid()) {
                return;
            }
            
            if (RenderGraphUtils.CanAddCopyPassMSAA()) {
                /*// This simple pass copies the active color texture to a new texture.
                renderGraph.AddCopyPass(resourceData.activeColorTexture, destination, passName: passName);*/

                //Need to copy back otherwise the pass gets culled since the result of the previous copy is not read. This is just for demonstration purposes.
                renderGraph.AddCopyPass(rayTracingData.ShadowMap, resourceData.activeColorTexture, passName: passName);
            } else {
                Debug.Log("Can't add the copy pass due to MSAA");
            }
        }
    }
}