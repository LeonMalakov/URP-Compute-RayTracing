#ifndef UNIVERSAL_CLUSTER_DEFERRED
#define UNIVERSAL_CLUSTER_DEFERRED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DynamicScaling.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

// Custom.
TEXTURE2D_X(_RayTracingShadowMap);
SAMPLER(sampler_RayTracingShadowMap);
TEXTURE2D_X(_RayTracingReflectionsMap);
SAMPLER(sampler_RayTracingReflectionsMap);

struct Attributes
{
    float4 positionOS : POSITION;
    uint vertexID : SV_VertexID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 screenUV : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings VertexFullScreen(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionOS = input.positionOS.xyz;
    output.positionCS = float4(positionOS.xy, UNITY_RAW_FAR_CLIP_VALUE, 1.0); // Force triangle to be on zfar

    output.screenUV = output.positionCS.xyw;
    #if UNITY_UV_STARTS_AT_TOP
    output.screenUV.xy = output.screenUV.xy * float2(0.5, -0.5) + 0.5 * output.screenUV.z;
    #else
    output.screenUV.xy = output.screenUV.xy * 0.5 + 0.5 * output.screenUV.z;
    #endif

    output.screenUV.xy = DynamicScalingApplyScaleBias(output.screenUV.xy, float4(_RTHandleScale.xy, 0.0f, 0.0f));

    return output;
}

float4x4 _ScreenToWorld[2];

half3 DeferredLightContribution(Light light, InputData inputData, GBufferData gBufferData, half3 reflectionColor)
{
    #if defined(_LIGHT_LAYERS)
    UNITY_BRANCH if (!IsMatchingLightLayer(light.layerMask, gBufferData.meshRenderingLayers))
        return half3(0.0, 0.0, 0.0);
    #endif

    #if defined(_SIMPLELIT)
    {
        SurfaceData surfaceData = GBufferDataToSurfaceData(gBufferData);
        half3 attenuatedLightColor = light.color * (light.distanceAttenuation * light.shadowAttenuation);
        half3 diffuseColor = LightingLambert(attenuatedLightColor, light.direction, inputData.normalWS);
        half smoothness = exp2(10 * surfaceData.smoothness + 1);
        half3 specularColor = LightingSpecular(attenuatedLightColor, light.direction, inputData.normalWS, inputData.viewDirectionWS, half4(surfaceData.specular, 1), smoothness);

        // TODO: if !defined(_SPECGLOSSMAP) && !defined(_SPECULAR_COLOR), force specularColor to 0 in gbuffer code
        return half3(diffuseColor * surfaceData.albedo + specularColor);
    }
    #elif defined(_LIT)
    {
        #if SHADER_API_MOBILE || SHADER_API_SWITCH
            // Specular highlights are still silenced by setting specular to 0.0 during gbuffer pass and GPU timing is still reduced.
            bool materialSpecularHighlightsOff = false;
        #else
            bool materialSpecularHighlightsOff = (gBufferData.materialFlags & kMaterialFlagSpecularHighlightsOff);
        #endif

        BRDFData brdfData = GBufferDataToBRDFData(gBufferData);
        brdfData.specular *= reflectionColor;
        return half3(LightingPhysicallyBased(brdfData, light, inputData.normalWS, inputData.viewDirectionWS, materialSpecularHighlightsOff));
    }
    #endif

    return half3(0.0, 0.0, 0.0);
}

/*#define REFLECTIONS_MIP_COUNT 15
#define REFLECTIONS_MAX_KERNEL_SIZE 16

half3 SampleReflectionsMip(float2 screenUV, half mipLevel)
{
    #if !defined(_RAY_TRACING_REFLECTIONS_MAP)
    return half3(0.0, 0.0, 0.0);
    #else

    half3 reflectionColor = half3(0.0h, 0.0h, 0.0h);
    uint kernelSize = min(mipLevel + 1, REFLECTIONS_MAX_KERNEL_SIZE);
    
    [unroll(REFLECTIONS_MAX_KERNEL_SIZE)]
    for (uint x = 0; x < REFLECTIONS_MAX_KERNEL_SIZE; x++)
    {
        if (x >= kernelSize) break;
        [unroll(REFLECTIONS_MAX_KERNEL_SIZE)]
        for (uint y = 0; y < REFLECTIONS_MAX_KERNEL_SIZE; y++)
        {
            if (y >= kernelSize) break;
            half texSizeInv = 1 / 1080;
            float2 offset = float2(x - kernelSize * 0.5, y - kernelSize * 0.5) * texSizeInv;
            float2 uv = screenUV + offset;
            half3 sample = SAMPLE_TEXTURE2D_X(_RayTracingReflectionsMap, sampler_RayTracingReflectionsMap, uv).rgb;
            reflectionColor += sample;
        }
    }

    reflectionColor /= (kernelSize * kernelSize);
    
    //half3 reflectionColor = SAMPLE_TEXTURE2D_X(_RayTracingReflectionsMap, sampler_RayTracingReflectionsMap, screenUV).rgb;
    
    return reflectionColor;
    
    #endif
}*/

half3 SampleReflections(float2 screenUV)
{
    #if !defined(_RAY_TRACING_REFLECTIONS_MAP)
    return half3(1.0h, 1.0h, 1.0h);
    #else
    // TODO: небольшой мип.
    half3 reflectionColor = SAMPLE_TEXTURE2D_X(_RayTracingReflectionsMap, sampler_RayTracingReflectionsMap, screenUV).rgb;
    return reflectionColor;
    #endif
}

half3 AddReflections(
    float2 screenUV,
    InputData inputData,
    GBufferData gBufferData,
    half3 baseColor,
    float reflectionStrength
)
{
    #if !defined(_RAY_TRACING_REFLECTIONS_MAP)
    return baseColor;
    #else
    BRDFData brdfData = GBufferDataToBRDFData(gBufferData);
    
    // Сэмплируем отражения (уже рассчитанные трассировкой или SSR)
    half3 reflectionColor = SAMPLE_TEXTURE2D_X(_RayTracingReflectionsMap, sampler_RayTracingReflectionsMap, screenUV).rgb;

    // Ambient-probe average, including Environment reflection intensity, supplied by URP.
    // This contribution is independent of the main light's NdotL and shadow attenuation.
    half3 environmentLighting = max(_GlossyEnvironmentColor.rgb, half3(0.0h, 0.0h, 0.0h));
    half roughnessAttenuation = 1.0h - brdfData.perceptualRoughness * 0.85h;
    reflectionColor *= environmentLighting * roughnessAttenuation;
    
    // Fresnel эффект — усиливаем отражения под острыми углами
    //float NdotV = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
    //float fresnel = brdfData.grazingTerm + (1.0 - brdfData.grazingTerm) * pow(1.0 - NdotV, 5.0);

    half NoV = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
    half fresnelTerm = Pow4(1.0 - NoV);
    
    // Roughness влияет на силу отражений (матовые поверхности отражают слабее)
    //float roughnessFactor = saturate(1.0 - brdfData.perceptualRoughness * 0.85);

    // Итоговая сила отражений
    //float reflectionFactor = fresnel * roughnessFactor * brdfData.reflectivity * reflectionStrength;

    // Ослабляем "чистоту" отражений для неметаллов (используем specular для тонирования)
    //float3 reflectionTint = brdfData.specular;

    // Итоговое значение отражения
    //float3 reflectionFinal = reflectionColor * reflectionTint * reflectionFactor;

    // --- Смешиваем с базовым освещением ---
    // Для металлов отражения сильнее перекрывают базовое освещение,
    // для неметаллов отражения мягко подмешиваются.
    //float metallicLike = brdfData.reflectivity;
    //float3 finalColor = lerp(baseColor, reflectionFinal, metallicLike);

    float surfaceReduction = 1.0 / (brdfData.roughness2 + 1.0);
    half3 environmentBrdfSpecular = half3(surfaceReduction * lerp(brdfData.specular, brdfData.grazingTerm, fresnelTerm));

    half3 indirectSpecular = reflectionColor * environmentBrdfSpecular * gBufferData.occlusion * reflectionStrength;
    return indirectSpecular + baseColor;
    #endif
}

half4 DeferredShadingClustered(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float2 screen_uv = (input.screenUV.xy / input.screenUV.z);

    #if defined(SUPPORTS_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
    float2 undistorted_screen_uv = screen_uv;
    UNITY_BRANCH if (_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
    {
        screen_uv = input.positionCS.xy * _ScreenSize.zw;
    }
    #endif

    GBufferData gBufferData = UnpackGBuffers(input.positionCS.xy);

    half3 color = 0.0;
    half alpha = 1.0;

    #if defined(SUPPORTS_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
    UNITY_BRANCH if (_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
    {
        input.positionCS.xy = undistorted_screen_uv * _ScreenSize.xy;
    }
    #endif

    float4 posWS = mul(_ScreenToWorld[SLICE_ARRAY_INDEX], float4(input.positionCS.xy, gBufferData.depth, 1.0));
    posWS.xyz *= rcp(posWS.w);

    InputData inputData = (InputData)0;

    inputData.positionWS = posWS.xyz;
    inputData.normalWS = gBufferData.normalWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(posWS.xyz);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(screen_uv);

    #if defined(_SCREEN_SPACE_OCCLUSION)
        // What we want is really to apply the minimum occlusion value between the baked occlusion from surfaceDataOcclusion and real-time occlusion from SSAO.
        // But we already applied the baked occlusion during gbuffer pass, so we have to cancel it out here.
        // We must also avoid divide-by-0 that the reciprocal can generate.
        half surfaceDataOcclusion = gBufferData.occlusion;
        half occlusion = aoFactor.indirectAmbientOcclusion < surfaceDataOcclusion ? aoFactor.indirectAmbientOcclusion * rcp(surfaceDataOcclusion) : 1.0;
        alpha = occlusion;
    #endif

    // Main light
    Light mainLight = GetMainLight();
    mainLight.distanceAttenuation = 1.0;
    bool materialReceiveShadowsOff = (gBufferData.materialFlags & kMaterialFlagReceiveShadowsOff) != 0;
    UNITY_BRANCH if (!materialReceiveShadowsOff)
    {
        #if defined(_RAY_TRACING_SHADOW_MAP)
            mainLight.shadowAttenuation = SAMPLE_TEXTURE2D_X(_RayTracingShadowMap, sampler_RayTracingShadowMap, float3(screen_uv, 0)).r;
        #else
            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
            float4 shadowCoord = float4(screen_uv, 0.0, 1.0);
            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
            float4 shadowCoord = TransformWorldToShadowCoord(posWS.xyz);
            #else
            float4 shadowCoord = float4(0, 0, 0, 0);
            #endif
            mainLight.shadowAttenuation = MainLightShadow(shadowCoord, posWS.xyz, gBufferData.shadowMask, _MainLightOcclusionProbes);
        #endif
    }

    #if defined(_LIGHT_COOKIES)
        half3 cookieColor = SampleMainLightCookie(posWS.xyz);
        mainLight.color *= half3(cookieColor);
    #endif

    #if defined(_SCREEN_SPACE_OCCLUSION)
        mainLight.shadowAttenuation *= aoFactor.directAmbientOcclusion;
    #endif

    half3 reflectionColor = SampleReflections(screen_uv);
    color += DeferredLightContribution(mainLight, inputData, gBufferData, reflectionColor);

    // Additional light loop
    // We do additional directional lights last because otherwise FXC complains...
    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, gBufferData.shadowMask, aoFactor);

        UNITY_BRANCH if (materialReceiveShadowsOff)
        {
            light.shadowAttenuation = 1.0;
        }

        color += DeferredLightContribution(light, inputData, gBufferData, reflectionColor);
    LIGHT_LOOP_END

    UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, inputData, gBufferData.shadowMask, aoFactor);

        UNITY_BRANCH if (materialReceiveShadowsOff)
        {
            light.shadowAttenuation = 1.0;
        }

        color += DeferredLightContribution(light, inputData, gBufferData, reflectionColor);
    }

    color = AddReflections(screen_uv, inputData, gBufferData, color, 1.0);
    
    return half4(color, alpha);
}

#endif //UNIVERSAL_CLUSTER_DEFERRED