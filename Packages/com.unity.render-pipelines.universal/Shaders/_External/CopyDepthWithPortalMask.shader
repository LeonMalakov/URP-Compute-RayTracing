Shader "Hidden/Universal Render Pipeline Custom/CopyDepthWithPortalMask"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}

        Pass
        {
            Name "CopyDepthWithPortalMask"
            ZTest Always
            ZWrite[_ZWrite]
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            
            #pragma multi_compile _ _OUTPUT_DEPTH

            #include "Packages/com.unity.render-pipelines.universal/Shaders/_External/CopyDepthWithPortalMaskPass.hlsl"

            ENDHLSL
        }
    }
}
