using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Boil.RenderPipeline.RayTracing {
    public static class RayTracingPassCommon {
        public static RayTracingPassData ImportAndUseRayTracingData(RayTracingDataStorage dataStorage, RenderGraph renderGraph, IComputeRenderGraphBuilder builder) {
            RayTracingPassData data = new RayTracingPassData {
                GridOriginWS = dataStorage.GridOriginWS,

                Grid0 = renderGraph.ImportBuffer(dataStorage.Grid0Buffer),
                Grid0Size = dataStorage.Grid0Size,
                Grid0VoxelSize = dataStorage.Grid0VoxelSize,
                Grid0Meta = renderGraph.ImportBuffer(dataStorage.Grid0MetaBuffer),

                Grid1 = renderGraph.ImportBuffer(dataStorage.Grid1Buffer),
                Grid1Size = dataStorage.Grid1Size,
                Grid1VoxelSize = dataStorage.Grid1VoxelSize,
                Grid1Meta = renderGraph.ImportBuffer(dataStorage.Grid1MetaBuffer),

                Triangles = renderGraph.ImportBuffer(dataStorage.TrianglesBuffer),
                Materials = renderGraph.ImportBuffer(dataStorage.MaterialsBuffer),
                
                BaseMapArray = renderGraph.ImportTexture(dataStorage.BaseMapArray),
                NormalMapArray = renderGraph.ImportTexture(dataStorage.NormalMapArray),
                MetallicOcclusionMapArray = renderGraph.ImportTexture(dataStorage.MetallicOcclusionMapArray)
            };

            // Можно не делать и ниче не сломается, но мб так правильнее.
            builder.UseBuffer(data.Grid0);
            builder.UseBuffer(data.Grid0Meta);
            builder.UseBuffer(data.Grid1);
            builder.UseBuffer(data.Grid1Meta);
            builder.UseBuffer(data.Triangles);
            builder.UseBuffer(data.Materials);
            //
            
            builder.UseTexture(data.BaseMapArray);
            builder.UseTexture(data.NormalMapArray);
            builder.UseTexture(data.MetallicOcclusionMapArray);

            return data;
        }
        
        public static void SetupRayTracingData(ComputeGraphContext context, ComputeShader computeShader, int kernelIndex, ref RayTracingPassData data) {
            ComputeCommandBuffer cmd = context.cmd;
            
            cmd.SetComputeVectorParam(computeShader, RayTracingShaderParams.GridOriginWS, data.GridOriginWS);

            cmd.SetComputeBufferParam(computeShader, kernelIndex, RayTracingShaderParams.Grid0, data.Grid0);
            cmd.SetComputeIntParams(computeShader, RayTracingShaderParams.Grid0Size, data.Grid0Size.x, data.Grid0Size.y,
                data.Grid0Size.z);
            cmd.SetComputeFloatParam(computeShader, RayTracingShaderParams.Grid0VoxelSize, data.Grid0VoxelSize);
            cmd.SetComputeBufferParam(computeShader, kernelIndex, RayTracingShaderParams.Grid0MetaInfo, data.Grid0Meta);

            cmd.SetComputeBufferParam(computeShader, kernelIndex, RayTracingShaderParams.Grid1, data.Grid1);
            cmd.SetComputeIntParams(computeShader, RayTracingShaderParams.Grid1Size, data.Grid1Size.x, data.Grid1Size.y,
                data.Grid1Size.z);
            cmd.SetComputeFloatParam(computeShader, RayTracingShaderParams.Grid1VoxelSize, data.Grid1VoxelSize);
            cmd.SetComputeBufferParam(computeShader, kernelIndex, RayTracingShaderParams.Grid1MetaInfo, data.Grid1Meta);

            cmd.SetComputeBufferParam(computeShader, kernelIndex, RayTracingShaderParams.Triangles, data.Triangles);
            cmd.SetComputeBufferParam(computeShader, kernelIndex, RayTracingShaderParams.Materials, data.Materials);
            
            cmd.SetComputeTextureParam(computeShader, kernelIndex, RayTracingShaderParams.BaseMapArray, data.BaseMapArray);
            cmd.SetComputeTextureParam(computeShader, kernelIndex, RayTracingShaderParams.NormalMapArray, data.NormalMapArray);
            cmd.SetComputeTextureParam(computeShader, kernelIndex, RayTracingShaderParams.MetallicOcclusionMapArray, data.MetallicOcclusionMapArray);
        }

        public static void SetupGBufferData(ComputeGraphContext context, ComputeShader computeShader, int kernelIndex, TextureHandle[] gBuffer) {
            // Here, we read all the gBuffer components as an example even though the shader only needs one.
            // We still need to set it explicitly since it is not accessible globally (so the
            // shader won't have access to it by default).
            for (int i = 0; i < gBuffer.Length; i++) {
                context.cmd.SetComputeTextureParam(computeShader, kernelIndex, RayTracingShaderParams.GBufferShaderPropertyIDs[i],
                    gBuffer[i]);
            }
        }
    }
}