#ifndef EXTERNAL_UNIVERSAL_SUMMARIZE_DEPTH_PASS_INCLUDED
#define EXTERNAL_UNIVERSAL_SUMMARIZE_DEPTH_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/_External/ExternalUtility.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/_External/ExternalConstants.hlsl"

float4 _ZBufferParamsPerCamera[MAX_CAMERAS];
uint _ActiveCameraIndex;
TEXTURE2D(_SourceActiveDepthTexture);
SAMPLER(sampler_SourceActiveDepthTexture);

TEXTURE2D(_SourcePortalDepthTexture);
SAMPLER(sampler_SourcePortalDepthTexture);

TEXTURE2D(_PortalMaskDepth);
SAMPLER(sampler_PortalMaskDepth);

TEXTURE2D(_PortalMaskTexture);

float SamplePortalMask(float2 normalizedScreenSpaceUV)
{
    float2 uv = UnityStereoTransformScreenSpaceTex(normalizedScreenSpaceUV);
    return SAMPLE_TEXTURE2D_X(_PortalMaskTexture, sampler_LinearClamp, uv).x;
}

float SampleSourceActiveDepth(float2 normalizedScreenSpaceUV)
{
    float2 uv = UnityStereoTransformScreenSpaceTex(normalizedScreenSpaceUV);
    return SAMPLE_DEPTH_TEXTURE(_SourceActiveDepthTexture, sampler_SourceActiveDepthTexture, uv);
    //return SAMPLE_TEXTURE2D_X(_SourceActiveDepthTexture, sampler_LinearClamp, uv).x;
}

float SampleSourcePortalDepth(float2 normalizedScreenSpaceUV)
{
    float2 uv = UnityStereoTransformScreenSpaceTex(normalizedScreenSpaceUV);
    return SAMPLE_DEPTH_TEXTURE(_SourcePortalDepthTexture, sampler_SourcePortalDepthTexture, uv);
}

float SamplePortalMaskDepth(float2 normalizedScreenSpaceUV)
{
    float2 uv = UnityStereoTransformScreenSpaceTex(normalizedScreenSpaceUV);
    return SAMPLE_DEPTH_TEXTURE(_PortalMaskDepth, sampler_PortalMaskDepth, uv);
}

float GetLinearEyeDepth(float rawDepth, float4 zBufferParams)
{
    /*#if defined(_ORTHOGRAPHIC)
    return LinearDepthToEyeDepth(rawDepth);
    #else*/
    return LinearEyeDepth(rawDepth, zBufferParams);
    //#endif
}

float RawDepthFromLinearEyeDepth(float linearDepth, float4 zBufferParam)
{
    // защита от нулевой или отрицательной линейной глубины
    linearDepth = max(linearDepth, 1e-6);

    // защита от некорректного параметра (теоретически zBufferParam.z != 0 для перспективы)
    if (abs(zBufferParam.z) < 1e-8)
        return 0.0;

    float raw = (1.0 / linearDepth - zBufferParam.w) / zBufferParam.z;
    return saturate(raw);
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
    float4 zBufferParamsForPortal = _ZBufferParamsPerCamera[index];
    float4 zBufferParamsForActive = _ZBufferParamsPerCamera[_ActiveCameraIndex];
    
    
    float depthA = SampleSourceActiveDepth(input.texcoord);

    if(index == 0)
    {
        return depthA;
    }

    float depthPortalMask = SamplePortalMaskDepth(input.texcoord);

    if(abs(depthA - depthPortalMask) > PORTALS_DEPTH_OFFSET_COMP)
    {
        return depthA;
    }
    
    float depthB = SampleSourcePortalDepth(input.texcoord);
    float linearDepthA = GetLinearEyeDepth(depthA, zBufferParamsForActive);
    float linearDepthB = GetLinearEyeDepth(depthB, zBufferParamsForPortal);
    float resultLinearDepth = linearDepthA + linearDepthB;

    float resultDepth = RawDepthFromLinearEyeDepth(resultLinearDepth, zBufferParamsForActive);

    resultDepth -= PORTALS_DEPTH_OFFSET;
    
    if(abs(resultDepth - PORTALS_DEPTH_FAR_VALUE) < 1e-2)
    {
        resultDepth = PORTALS_DEPTH_FAR_VALUE;
    }
    
    return resultDepth;
    
    //return RawDepthFromLinearEyeDepth(linearDepthA, zBufferParamsForActive);
    //return RawDepthFromLinearEyeDepth(linearDepthB, zBufferParamsForActive);
}

#endif
