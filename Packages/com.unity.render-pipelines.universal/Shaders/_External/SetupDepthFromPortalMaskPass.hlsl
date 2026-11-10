#ifndef EXTERNAL_UNIVERSAL_SETUP_DEPTH_FROM_PORTAL_MASK_PASS_INCLUDED
#define EXTERNAL_UNIVERSAL_SETUP_DEPTH_FROM_PORTAL_MASK_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/_External/ExternalUtility.hlsl"

#if UNITY_REVERSED_Z
    #define DEPTH_DEFAULT_VALUE 1.0
    #define DEPTH_CLOSEST_VALUE 0.0
    #define DEPTH_OP min
#else
    #define DEPTH_DEFAULT_VALUE 0.0
    #define DEPTH_CLOSEST_VALUE 1.0
    #define DEPTH_OP max
#endif

uint _ActiveCameraIndex;
TEXTURE2D(_PortalMaskTexture);


float SamplePortalMask(float2 normalizedScreenSpaceUV)
{
    float2 uv = UnityStereoTransformScreenSpaceTex(normalizedScreenSpaceUV);
    return SAMPLE_TEXTURE2D_X(_PortalMaskTexture, sampler_LinearClamp, uv).x;
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

    // UNITY_NEAR_CLIP_VALUE
    
    // TODO: if equals camera index, 0.0 else 1.0
    // TODO: reverse ?
    return index == _ActiveCameraIndex ? 0.0 : 1.0;
}

#endif
