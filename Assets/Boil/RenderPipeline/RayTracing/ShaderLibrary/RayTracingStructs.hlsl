#ifndef RAY_TRACING_STRUCTS
#define RAY_TRACING_STRUCTS

struct VoxelMetaInfo0
{
    uint startIndex;
    uint endIndex;
};

struct VoxelMetaInfo1
{
    int voxelIndex;
    uint startIndex;
    uint endIndex;
};

struct TriangleInfo
{
    half3 v0;
    half3 v1;
    half3 v2;

    int lightmapIndex;
    half2 lightmapUV0;
    half2 lightmapUV1;
    half2 lightmapUV2;

    int materialIndex;
    half2 mainUV0;
    half2 mainUV1;
    half2 mainUV2;
};

struct MaterialInfo
{
    int baseMapIndex;
    int normalMapIndex;
    int metallicOcclusionMapIndex;
};

struct VoxelBounds
{
    half3 min;
    half3 max;
};

struct VoxelGrid
{
    StructuredBuffer<uint> gridMask;
    int3 gridSize;
    half3 gridOriginWS;
    half voxelSize;
};

struct TraceContext
{
    VoxelGrid grid0;
    VoxelGrid grid1;
};

#endif