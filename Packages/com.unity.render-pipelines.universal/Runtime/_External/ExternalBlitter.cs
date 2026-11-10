using UnityEngine;
using UnityEngine.Rendering;

namespace URPCustom {
    public class ExternalBlitter {
        static Mesh s_TriangleMesh;
        
        static class BlitShaderIDs
        {
            public static readonly int _BlitTexture = Shader.PropertyToID("_BlitTexture");
            public static readonly int _BlitCubeTexture = Shader.PropertyToID("_BlitCubeTexture");
            public static readonly int _BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
            public static readonly int _BlitScaleBiasRt = Shader.PropertyToID("_BlitScaleBiasRt");
            public static readonly int _BlitMipLevel = Shader.PropertyToID("_BlitMipLevel");
            public static readonly int _BlitTexArraySlice = Shader.PropertyToID("_BlitTexArraySlice");
            public static readonly int _BlitTextureSize = Shader.PropertyToID("_BlitTextureSize");
            public static readonly int _BlitPaddingSize = Shader.PropertyToID("_BlitPaddingSize");
            public static readonly int _BlitDecodeInstructions = Shader.PropertyToID("_BlitDecodeInstructions");
            public static readonly int _InputDepth = Shader.PropertyToID("_InputDepthTexture");
        }
        
        public static void Initialize()
        {
            /*UNITY_NEAR_CLIP_VALUE*/
            float nearClipZ = -1;
            if (SystemInfo.usesReversedZBuffer)
                nearClipZ = 1;
            if (SystemInfo.graphicsShaderLevel < 30)
            {
                if (!s_TriangleMesh)
                {
                    s_TriangleMesh = new Mesh();
                    s_TriangleMesh.vertices = GetFullScreenTriangleVertexPosition(nearClipZ);
                    s_TriangleMesh.uv = GetFullScreenTriangleTexCoord();
                    s_TriangleMesh.triangles = new int[3] { 0, 1, 2 };
                }
            }

            // Should match Common.hlsl
            static Vector3[] GetFullScreenTriangleVertexPosition(float z /*= UNITY_NEAR_CLIP_VALUE*/)
            {
                var r = new Vector3[3];
                for (int i = 0; i < 3; i++)
                {
                    Vector2 uv = new Vector2((i << 1) & 2, i & 2);
                    r[i] = new Vector3(uv.x * 2.0f - 1.0f, uv.y * 2.0f - 1.0f, z);
                }
                return r;
            }

            // Should match Common.hlsl
            static Vector2[] GetFullScreenTriangleTexCoord()
            {
                var r = new Vector2[3];
                for (int i = 0; i < 3; i++)
                {
                    if (SystemInfo.graphicsUVStartsAtTop)
                        r[i] = new Vector2((i << 1) & 2, 1.0f - (i & 2));
                    else
                        r[i] = new Vector2((i << 1) & 2, i & 2);
                }
                return r;
            }
        }

        /// <summary>
        /// Releases all the internal Blitter resources. Must be called when the Blitter object is to be disposed.
        /// </summary>
        public static void Cleanup()
        {
            CoreUtils.Destroy(s_TriangleMesh);
            s_TriangleMesh = null;
        }
        
        public static void BlitTexture(RasterCommandBuffer cmd, RTHandle source, Vector4 scaleBias, Material material, MaterialPropertyBlock propBlock, int pass)
        {
            propBlock.SetVector(BlitShaderIDs._BlitScaleBias, scaleBias);
            propBlock.SetTexture(BlitShaderIDs._BlitTexture, source);
            DrawTriangle(cmd, material, pass, propBlock);
        }
        
        static internal void DrawTriangle(RasterCommandBuffer cmd, Material material, int shaderPass, MaterialPropertyBlock propertyBlock)
        {
            if (SystemInfo.graphicsShaderLevel < 30)
                cmd.DrawMesh(s_TriangleMesh, Matrix4x4.identity, material, 0, shaderPass, propertyBlock);
            else
                cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Triangles, 3, 1, propertyBlock);
        }
    }
}