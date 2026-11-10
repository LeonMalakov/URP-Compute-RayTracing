using UnityEngine;

namespace Boil.RenderPipeline.RayTracing {
    public static class RayTracingShaderParams {
        public static readonly int GridOriginWS = Shader.PropertyToID("_GridOriginWS");

        public static readonly int Grid0 = Shader.PropertyToID("_Grid0");
        public static readonly int Grid0Size = Shader.PropertyToID("_Grid0Size");
        public static readonly int Grid0VoxelSize = Shader.PropertyToID("_Grid0VoxelSize");
        public static readonly int Grid0MetaInfo = Shader.PropertyToID("_Grid0MetaInfo");

        public static readonly int Grid1 = Shader.PropertyToID("_Grid1");
        public static readonly int Grid1Size = Shader.PropertyToID("_Grid1Size");
        public static readonly int Grid1VoxelSize = Shader.PropertyToID("_Grid1VoxelSize");
        public static readonly int Grid1MetaInfo = Shader.PropertyToID("_Grid1MetaInfo");

        public static readonly int Triangles = Shader.PropertyToID("_Triangles");
        public static readonly int Materials = Shader.PropertyToID("_Materials");
        
        public static readonly int BaseMapArray = Shader.PropertyToID("_BaseMapArray");
        public static readonly int NormalMapArray = Shader.PropertyToID("_NormalMapArray");
        public static readonly int MetallicOcclusionMapArray = Shader.PropertyToID("_MetallicOcclusionMapArray");
        
        // Components marked as optional are only present when the pipeline requests it.
        // If for example there is no rendering layers texture, _GBuffer5 will contain the ShadowMask texture
        public static readonly int[] GBufferShaderPropertyIDs = new int[] {
            // Contains Albedo Texture
            Shader.PropertyToID("_GBuffer0"),

            // Contains Specular Metallic Texture
            Shader.PropertyToID("_GBuffer1"),

            // Contains Normals and Smoothness, referenced as _CameraNormalsTexture in other shaders
            Shader.PropertyToID("_GBuffer2"),

            // Contains Lighting texture
            Shader.PropertyToID("_GBuffer3"),

            // Contains Depth texture, referenced as _CameraDepthTexture in other shaders (optional)
            Shader.PropertyToID("_GBuffer4"),

            // Contains Rendering Layers Texture, referenced as _CameraRenderingLayersTexture in other shaders (optional)
            Shader.PropertyToID("_GBuffer5"),

            // Contains ShadowMask texture (optional)
            Shader.PropertyToID("_GBuffer6")
        };
    }
}