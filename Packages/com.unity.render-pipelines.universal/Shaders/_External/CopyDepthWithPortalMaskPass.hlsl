#ifndef EXTERNAL_UNIVERSAL_COPY_DEPTH_WITH_PORTAL_MASK_PASS_INCLUDED
#define EXTERNAL_UNIVERSAL_COPY_DEPTH_WITH_PORTAL_MASK_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/_External/ExternalUtility.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/_External/ExternalConstants.hlsl"

// Можно убрать сдвиг, если объект портала не будет рендерится мейн камерой.


uint _ActiveCameraIndex;
TEXTURE2D(_PortalMaskTexture);
TEXTURE2D(_SourceDepthTexture);
int _RevertCameraIndexComparison;
int _ClearOther;

float SamplePortalMask(float2 normalizedScreenSpaceUV)
{
    float2 uv = UnityStereoTransformScreenSpaceTex(normalizedScreenSpaceUV);
    return SAMPLE_TEXTURE2D_X(_PortalMaskTexture, sampler_LinearClamp, uv).x;
}

float SampleSourceDepth(float2 normalizedScreenSpaceUV)
{
    float2 uv = UnityStereoTransformScreenSpaceTex(normalizedScreenSpaceUV);
    return SAMPLE_TEXTURE2D_X(_SourceDepthTexture, sampler_LinearClamp, uv).x;
}

#if defined(_OUTPUT_DEPTH)
float frag(Varyings input) : SV_Depth
#else
float frag(Varyings input) : SV_Target
#endif
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    
    float mask = SamplePortalMask(input.texcoord);
    int index = UnpackR8ToUInt8(mask);

    bool revertComparison = _RevertCameraIndexComparison != 0;
    bool clearOther = _ClearOther != 0;
    
    if(index != _ActiveCameraIndex ^ revertComparison)
    {
        if(!clearOther)
        {
            discard;
        } else
        {
            return PORTALS_DEPTH_FAR_VALUE;
        }
    }

    float depth = SampleSourceDepth(input.texcoord);

    if(clearOther)
    {
        depth += PORTALS_DEPTH_OFFSET;
    }
    
    return depth;
    
    /*return !revertComparison
        ? depth
        : depth + DEPTH_OFFSET; */
}

#endif
