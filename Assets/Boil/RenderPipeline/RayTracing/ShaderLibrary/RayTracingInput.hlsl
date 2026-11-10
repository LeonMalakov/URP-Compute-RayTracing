#ifndef RAY_TRACING_INPUT
#define RAY_TRACING_INPUT

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "RayTracingStructs.hlsl"
#include "RayTracing.hlsl"

half3 _GridOriginWS;

StructuredBuffer<uint> _Grid0;
int3 _Grid0Size;
half _Grid0VoxelSize;
StructuredBuffer<VoxelMetaInfo0> _Grid0MetaInfo; // ForEach / Index = VoxelIndex

StructuredBuffer<uint> _Grid1;
int3 _Grid1Size;
half _Grid1VoxelSize;
StructuredBuffer<VoxelMetaInfo1> _Grid1MetaInfo; // NotForEach / Index != VoxelIndex

StructuredBuffer<TriangleInfo> _Triangles;
StructuredBuffer<MaterialInfo> _Materials;

/*TEXTURE2D_ARRAY(_BaseMapArray); SAMPLER(samplerLinear);
TEXTURE2D_ARRAY(_NormalMapArray);
TEXTURE2D_ARRAY(_MetallicOcclusionMapArray);*/
Texture2DArray<half4> _BaseMapArray;
Texture2DArray<half4> _NormalMapArray;
Texture2DArray<half4> _MetallicOcclusionMapArray;
SamplerState samplerLinearRepeat;

TraceContext CreateTraceContext()
{
    TraceContext context;
    
    context.grid0.gridMask = _Grid0;
    context.grid0.gridSize = _Grid0Size;
    context.grid0.gridOriginWS = _GridOriginWS;
    context.grid0.voxelSize = _Grid0VoxelSize;
    
    context.grid1.gridMask = _Grid1;
    context.grid1.gridSize = _Grid1Size;
    context.grid1.gridOriginWS = _GridOriginWS;
    context.grid1.voxelSize = _Grid1VoxelSize;
    
    return context;
}

VoxelMetaInfo1 FindVoxelMetaInfo1(int voxelIndex0, int voxelIndex1)
{
    VoxelMetaInfo0 voxelMetaInfo0 = _Grid0MetaInfo[voxelIndex0];
    
    for(uint i = voxelMetaInfo0.startIndex; i < voxelMetaInfo0.endIndex; i++)
    {
        VoxelMetaInfo1 voxelMetaInfo1 = _Grid1MetaInfo[i];
        if(voxelMetaInfo1.voxelIndex == voxelIndex1)
        {
            return voxelMetaInfo1;
        }
    }

    return (VoxelMetaInfo1)0;
}

MaterialInfo GetMaterialInfo(int materialIndex)
{
    return _Materials[materialIndex];
}

half4 SampleBaseMap(int index, half2 uv)
{
    return _BaseMapArray.SampleLevel(samplerLinearRepeat, half3(uv, index), 0);
    //return SAMPLE_TEXTURE2D_ARRAY(_BaseMapArray, samplerLinear, uv, index);
}

half4 SampleNormalMap(int index, half2 uv)
{
    return _NormalMapArray.SampleLevel(samplerLinearRepeat, half3(uv, index), 0);
}

half4 SampleMetallicOcclusionMap(int index, half2 uv)
{
    return _MetallicOcclusionMapArray.SampleLevel(samplerLinearRepeat, half3(uv, index), 0);
}



#endif