#ifndef RAY_TRACING
#define RAY_TRACING

#include "RayTracingCommon.hlsl"
#include "RayTracingInput.hlsl"

struct TraceReflectionsResult
{
    bool hit;
    int materialIndex;
    half2 mainUV;
};

struct TraceGIResult
{
    bool hit;
    int lightmapIndex;
    half2 lightmapUV;
};

TraceReflectionsResult CreateTraceReflectionsResult(bool hit, int materialIndex, half2 mainUV)
{
    TraceReflectionsResult result;
    result.hit = hit;
    result.materialIndex = materialIndex;
    result.mainUV = mainUV;
    return result;
}

TraceGIResult CreateTraceGIResult(bool hit, int lightmapIndex, half2 hitLightmapUV)
{
    TraceGIResult result;
    result.hit = hit;
    result.lightmapIndex = lightmapIndex;
    result.lightmapUV = hitLightmapUV;
    return result;
}

#ifdef TRACE_GI
    #define TRACE_RESULT TraceGIResult
    #define TRACE_RESULT_NO_HIT CreateTraceGIResult(false, 0, half2(0.0, 0.0))
    #define TRACE_RESULT_IS_HIT(result) result.hit
#elif defined(TRACE_REFLECTIONS)
    #define TRACE_RESULT TraceReflectionsResult
    #define TRACE_RESULT_NO_HIT CreateTraceReflectionsResult(false, 0, half2(0.0, 0.0))
    #define TRACE_RESULT_IS_HIT(result) result.hit
#else
    #define TRACE_RESULT bool
    #define TRACE_RESULT_NO_HIT false
    #define TRACE_RESULT_IS_HIT(result) result
#endif

TRACE_RESULT CheckVoxelIntersections(TraceContext context, int voxelIndex0, int voxelIndex1, half3 startPos,
                                     half3 rayDirection)
{
    VoxelMetaInfo1 voxelMetaInfo = FindVoxelMetaInfo1(voxelIndex0, voxelIndex1);

    for (uint i = voxelMetaInfo.startIndex; i < voxelMetaInfo.endIndex; i++)
    {
        TriangleInfo triangleInfo = _Triangles[i];

        bool hit;

        #ifdef TRACE_GI
            half3 hitPos;
            hit = RayTriangleIntersectWithPosition(startPos, rayDirection, triangleInfo.v0, triangleInfo.v1, triangleInfo.v2, hitPos);
            if(hit)
            {
                half2 hitLightmapUV = GetTriangleUVAtBarycentricPosition(hitPos, triangleInfo.lightmapUV0, triangleInfo.lightmapUV1, triangleInfo.lightmapUV2);
                return CreateTraceGIResult(true, triangleInfo.lightmapIndex, hitLightmapUV);
            }
        #elif defined(TRACE_REFLECTIONS)
            half3 hitPos;
            hit = RayTriangleIntersectWithPosition(startPos, rayDirection, triangleInfo.v0, triangleInfo.v1, triangleInfo.v2, hitPos);
            if(hit)
            {
                half2 hitMainUV = GetTriangleUVAtBarycentricPosition(hitPos, triangleInfo.mainUV0, triangleInfo.mainUV1, triangleInfo.mainUV2);
                return CreateTraceReflectionsResult(true, triangleInfo.materialIndex, hitMainUV);
            }
        #else
            hit = RayTriangleIntersect(startPos, rayDirection, triangleInfo.v0, triangleInfo.v1, triangleInfo.v2);
            if(hit)
            {
                return true;
            }
        #endif

        // TODO: надо ближайшее найти
        // Для теней и так норм, а для отражений и GI надо.
    }

    return TRACE_RESULT_NO_HIT;
}

TRACE_RESULT TraceSubGrid(TraceContext context, int3 voxelCoords0, int voxelIndex0, half3 startPos, half3 rayDirection)
{
    const int maxSteps = 1000;

    half stepSize = context.grid1.voxelSize * 0.1; // Min distance?
    half3 stepVector = rayDirection * stepSize;
    half3 currentPos = startPos;
    half3 prevPos = currentPos;
    int3 lastVoxelCoords = int3(-1, -1, -1);

    VoxelBounds voxelBounds0 = GetVoxelBounds(context.grid0.gridOriginWS, context.grid0.voxelSize, voxelCoords0);
    bool processingInsideVoxel = false;

    for (int i = 0; i < maxSteps; i++)
    {
        /*if(!IsVoxelBoundsContainsPoint(voxelBounds0, currentPos))
        {
            if(processingInsideVoxel)
                break;
            else 
                continue;
        }
        processingInsideVoxel = true;*/


        if (IsVoxelBoundsContainsPoint(voxelBounds0, currentPos))
        {
            processingInsideVoxel = true;

            int3 voxelCoords =
                GetVoxelCoordsByWorldPos(context.grid1.gridOriginWS, context.grid1.voxelSize, currentPos);

            if (!IsInt3Equal(lastVoxelCoords, voxelCoords))
            {
                int voxelIndex = GetVoxelIndex(context.grid1.gridSize, voxelCoords);
                bool hasGeometry = GetMaskValue(context.grid1.gridMask, voxelIndex);
                if (hasGeometry)
                {
                    //return true;

                    TRACE_RESULT result = CheckVoxelIntersections(context, voxelIndex0, voxelIndex, prevPos, rayDirection);

                    if (TRACE_RESULT_IS_HIT(result))
                        return result;
                }

                lastVoxelCoords = voxelCoords;
            }
        }
        else if (processingInsideVoxel)
        {
            break;
        }

        prevPos = currentPos;
        currentPos += stepVector;
    }

    return TRACE_RESULT_NO_HIT;
}

TRACE_RESULT TraceVoxelGrid(TraceContext context, half3 rayOrigin, half3 rayDirection)
{
    const int maxSteps = 1000;

    half stepSize = context.grid0.voxelSize * 0.1; // Min distance?
    half3 stepVector = rayDirection * stepSize;
    half3 currentPos = rayOrigin;
    half3 prevPos = currentPos;
    int3 lastVoxelCoords = int3(-1, -1, -1);

    VoxelBounds gridBounds = GetGridBounds(context.grid0.gridOriginWS, context.grid0.voxelSize, context.grid0.gridSize);

    for (int i = 0; i < maxSteps; i++)
    {
        if (!IsVoxelBoundsContainsPoint(gridBounds, currentPos))
            return TRACE_RESULT_NO_HIT;

        int3 voxelCoords = GetVoxelCoordsByWorldPos(context.grid0.gridOriginWS, context.grid0.voxelSize, currentPos);


        if (!IsInt3Equal(lastVoxelCoords, voxelCoords))
        {
            int voxelIndex = GetVoxelIndex(context.grid0.gridSize, voxelCoords);
            bool hasGeometry = GetMaskValue(context.grid0.gridMask, voxelIndex);
            if (hasGeometry)
            {
                //return true;

                TRACE_RESULT result = TraceSubGrid(context, voxelCoords, voxelIndex, prevPos, rayDirection);

                if (TRACE_RESULT_IS_HIT(result))
                    return result;
            }

            lastVoxelCoords = voxelCoords;
        }

        prevPos = currentPos;
        currentPos += stepVector;
    }

    return TRACE_RESULT_NO_HIT;
}

TRACE_RESULT Trace(half3 rayOrigin, half3 rayDirection)
{
    TraceContext context = CreateTraceContext();

    /*if(context.grid0.gridSize.x == 8 && context.grid0.gridSize.y == 2 && context.grid0.gridSize.z == 8)
    {
        return true;
    }*/

    return TraceVoxelGrid(context, rayOrigin, rayDirection);
}


#endif
