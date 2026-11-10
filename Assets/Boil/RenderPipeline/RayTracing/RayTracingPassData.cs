using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;

namespace Boil.RenderPipeline.RayTracing {
    public struct RayTracingPassData {
        public Vector3 GridOriginWS;

        public BufferHandle Grid0;
        public Vector3Int Grid0Size;
        public float Grid0VoxelSize;
        public BufferHandle Grid0Meta;

        public BufferHandle Grid1;
        public Vector3Int Grid1Size;
        public float Grid1VoxelSize;
        public BufferHandle Grid1Meta;

        public BufferHandle Triangles;
        public BufferHandle Materials;
        
        public TextureHandle BaseMapArray;
        public TextureHandle NormalMapArray;
        public TextureHandle MetallicOcclusionMapArray;
    }
}