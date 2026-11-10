#ifndef RAY_TRACING_COMMON
#define RAY_TRACING_COMMON

#include "RayTracingStructs.hlsl"

bool HasNoHitDueToNormal(half3 normalWS, half3 rayDirection)
{
    return dot(normalWS, rayDirection) < 0.0;
}

bool IsInt3Equal(int3 a, int3 b)
{
    return a.x == b.x && a.y == b.y && a.z == b.z;
}

int GetVoxelIndex(int3 gridSize, int3 voxelCoords)
{
    return voxelCoords.x + voxelCoords.z * gridSize.x + voxelCoords.y * gridSize.x * gridSize.z;
}

bool GetMaskValue(StructuredBuffer<uint> grid, int voxelIndex)
{
    uint voxelIndexU = (uint)voxelIndex;
    uint wordIndex = voxelIndexU / 32;
    uint bitIndex = voxelIndexU % 32;

    uint word = grid[wordIndex];
    return ((word >> bitIndex) & 1u) != 0;
}

int3 GetVoxelCoordsByWorldPos(half3 gridOriginWS, half voxelSize, half3 worldPos)
{
    half3 localPos = worldPos - gridOriginWS;
    int3 coords = (int3)floor(localPos / voxelSize);
    //coords = clamp(coords, int3(0, 0, 0), gridSize - 1);
    return coords;
}

VoxelBounds GetVoxelBounds(half3 gridOriginWS, half voxelSize, int3 voxelCoords)
{
    VoxelBounds bounds;
    bounds.min = gridOriginWS + half3(voxelCoords.x * voxelSize, voxelCoords.y * voxelSize, voxelCoords.z * voxelSize);
    bounds.max = bounds.min + half3(voxelSize, voxelSize, voxelSize);
    return bounds;
}

VoxelBounds GetGridBounds(half3 gridOriginWS, half voxelSize, int3 gridSize)
{
    VoxelBounds bounds;
    bounds.min = gridOriginWS;
    bounds.max = gridOriginWS + half3(gridSize.x * voxelSize, gridSize.y * voxelSize, gridSize.z * voxelSize);
    return bounds;
}

bool IsVoxelBoundsContainsPoint(VoxelBounds bounds, half3 pointWS)
{
    return pointWS.x >= bounds.min.x && pointWS.x <= bounds.max.x &&
           pointWS.y >= bounds.min.y && pointWS.y <= bounds.max.y &&
           pointWS.z >= bounds.min.z && pointWS.z <= bounds.max.z;
}

bool RayTriangleIntersect(half3 rayOrigin, half3 rayDirection, half3 v0, half3 v1, half3 v2)
{
    const half EPSILON = 1e-6;
    const half ZERO_EPSILON = 0.0;
    const half ONE_EPSILON = 1.0;
    
    half3 edge1 = v1 - v0;
    half3 edge2 = v2 - v0;

    half3 p = cross(rayDirection, edge2);
    half det = dot(edge1, p);

    // Backface culling
    if (det < EPSILON) // TODO: убрать abs для culling 
        return false;

    half detInv = rcp(det); // TODO: use rcp
    half3 tvec = rayOrigin - v0;
    
    float u = dot(tvec, p) * detInv;
    if (u < ZERO_EPSILON || u > ONE_EPSILON) return false;

    half3 q = cross(tvec, edge1);
    half v = dot(rayDirection, q) * detInv;
    if (v < ZERO_EPSILON || u + v > ONE_EPSILON) return false;

    // Distance check.
    //float t = dot(edge2, q) * detInv;
    //if (t < tMin || t > tMax) return;
    //if (t < 0.01 || t > 1000) return false;

    return true;
}

bool RayTriangleIntersectWithPosition(half3 rayOrigin, half3 rayDirection, half3 v0, half3 v1, half3 v2, out half3 hitPosition)
{
    const float EPSILON = 1e-6;
    
    half3 edge1 = v1 - v0;
    half3 edge2 = v2 - v0;

    half3 p = cross(rayDirection, edge2);
    half det = dot(edge1, p);

    // Backface culling
    if (det < EPSILON)
        return false;

    half detInv = rcp(det);
    float3 tvec = rayOrigin - v0;
    
    float u = dot(tvec, p) * detInv;
    if (u < 0.0 || u > 1.0) return false;

    float3 q = cross(tvec, edge1);
    float v = dot(rayDirection, q) * detInv;
    if (v < 0.0 || u + v > 1.0) return false;

    // Distance check.
    float t = dot(edge2, q) * detInv;
    /*if (t < tMin || t > tMax) return;*/
    
    hitPosition = half3(u, v, t); // Это локальные барицентрические координаты + дистанция
    return true;
}

half2 GetTriangleUVAtBarycentricPosition(half3 pos, half2 uv0, half2 uv1, half2 uv2)
{
    half u = pos.x;
    half v = pos.y;
    half w = 1.0 - u - v;
    
    return  uv0 * w + uv1 * u + uv2 * v;
}

#endif