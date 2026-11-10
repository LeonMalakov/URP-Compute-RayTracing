using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Boil.RenderPipeline.RayTracing {
    [StructLayout(LayoutKind.Sequential)]
    public struct VoxelMetaInfo0 {
        public const int SIZE = sizeof(uint) * 2;

        public uint StartIndex;
        public uint EndIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VoxelMetaInfo1 {
        public const int SIZE = sizeof(uint) * 2 + sizeof(int);

        public int VoxelIndex;
        public uint StartIndex;
        public uint EndIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TriangleMetaInfo {
        public const int SIZE = sizeof(float) * 3 * 3 + 
                                sizeof(int) * 1 + sizeof(float) * 2 * 3 +
                                sizeof(int) * 1 + sizeof(float) * 2 * 3;

        public Vector3 V0;
        public Vector3 V1;
        public Vector3 V2;
        
        public int LightmapIndex;
        public Vector2 LightmapUV0;
        public Vector2 LightmapUV1;
        public Vector2 LightmapUV2;
        
        public int MaterialIndex;
        public Vector2 UV0;
        public Vector2 UV1;
        public Vector2 UV2;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct MaterialInfo {
        public const int SIZE = sizeof(int) * 3;

        public int BaseMapIndex;
        public int NormalMapIndex;
        public int MetallicOcclusionMapIndex;
    }

    public class RayTracingDataBuildingContext {
        public uint[] Grid0;
        public VoxelMetaInfo0[] Grid0Meta;
        public uint[] Grid1;

        public VoxelMetaInfo1[] Grid1Meta;
        public TriangleMetaInfo[] Triangles;

        public readonly Dictionary<int, List<TriangleMetaInfo>> Voxel1Triangles = new();
        public List<VoxelMetaInfo1> Grid1MetaList = new();
        public List<TriangleMetaInfo> TrianglesList = new();
        public List<MaterialInfo> MaterialsList = new();
        public List<Material> SourceMaterialsList = new();
        

        public void AddTriangle(int voxelIndex, TriangleMetaInfo triangle) {
            if (!Voxel1Triangles.ContainsKey(voxelIndex)) {
                Voxel1Triangles[voxelIndex] = new List<TriangleMetaInfo>();
            }

            Voxel1Triangles[voxelIndex].Add(triangle);
        }
    }
    
    public class RayTracingDataStorage : IDisposable {
        public static class ObjectShaderParams {
            public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
            public static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
            public static readonly int MetallicGlossMap = Shader.PropertyToID("_MetallicGlossMap");
        }
        
        public GraphicsBuffer Grid0Buffer;
        public GraphicsBuffer Grid0MetaBuffer;
        public GraphicsBuffer Grid1Buffer;
        public GraphicsBuffer Grid1MetaBuffer;
        public GraphicsBuffer TrianglesBuffer;
        public GraphicsBuffer MaterialsBuffer;
        public RTHandle StaticLightmapArray;
        public Vector3Int StaticLightmapSize;
        public Vector3 SunLightDirection;
        
        public RTHandle BaseMapArray;
        public Vector3Int BaseMapSize;
        public RTHandle NormalMapArray;
        public Vector3Int NormalMapSize;
        public RTHandle MetallicOcclusionMapArray;
        public Vector3Int MetallicOcclusionMapSize;

        public readonly Vector3 GridOriginWS = Vector3.zero;
        public readonly float Grid0VoxelSize = 2f;
        public readonly float Grid1VoxelSize = 0.25f;
        public readonly Vector3Int Grid0Size = new Vector3Int(8, 2, 8);
        public Vector3Int Grid1Size;
        public readonly int Voxels1InsideVoxel0 = 8;
        
        public readonly int BaseMapResolution = 256;
        public readonly int NormalMapResolution = 256;
        public readonly int MetallicOcclusionMapResolution = 256;

        private RayTracingDataBuildingContext _context;

        public bool Ready { get; private set; }

        public RayTracingDataStorage() { }

        public void Dispose() {
            Ready = false;
            Grid0Buffer?.Dispose();
            Grid0MetaBuffer?.Dispose();
            Grid1Buffer?.Dispose();
            Grid1MetaBuffer?.Dispose();
            TrianglesBuffer?.Dispose();
            MaterialsBuffer?.Dispose();
            StaticLightmapArray?.Release();
        }

        public void StartBuilding() {
            Grid1Size = Grid0Size * Voxels1InsideVoxel0;

            int grid0VoxelCount = Grid0Size.x * Grid0Size.y * Grid0Size.z;
            if (grid0VoxelCount < 32 || grid0VoxelCount % 32 != 0) {
                throw new Exception($"Grid0 voxel count must be >= 32 and multiple of 32, but got {grid0VoxelCount}");
            }

            int grid0BufferSize = grid0VoxelCount / 32;

            int grid1VoxelCount = Grid1Size.x * Grid1Size.y * Grid1Size.z;
            if (grid1VoxelCount < 32 || grid1VoxelCount % 32 != 0) {
                throw new Exception($"Grid1 voxel count must be >= 32 and multiple of 32, but got {grid1VoxelCount}");
            }

            int grid1BufferSize = grid1VoxelCount / 32;


            _context = new RayTracingDataBuildingContext() {
                Grid0 = new uint[grid0BufferSize],
                Grid0Meta = new VoxelMetaInfo0[grid0VoxelCount],
                Grid1 = new uint[grid1BufferSize],
                //Grid1Meta = grid1Meta,
                //Triangles = triangles,
            };
        }

        public void AddTriangle(TriangleMetaInfo triangle) => ProcessTriangle(_context, triangle);

        public void AddStaticLightMaps(LightmapData[] lightMaps) {
            LightmapData firstLightmap = lightMaps[0];
            var firstColorTex = firstLightmap.lightmapColor;
            int width = firstColorTex.width;
            int height = firstColorTex.height;
            int slices = lightMaps.Length;
            RenderTextureFormat format = RenderTextureFormat.ARGB32; // DXT5 но несжатый.
            
            // DXT5 RGBA_DXT5_SRGB
            if (firstColorTex.format != TextureFormat.DXT5) {
                Debug.LogError($"Lightmap format must be DXT5, but got {firstColorTex.format}");
            }

            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(width, height, format, 0) {
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = slices,
                enableRandomWrite = true,
                useMipMap = true, // ?
                autoGenerateMips = true,
                sRGB = true,
                /*msaaSamples = 1,
                bindMS = false,
                memoryless = RenderTextureMemoryless.None,
                useDynamicScale = false*/
            };
            
            RenderingUtils.ReAllocateHandleIfNeeded(ref StaticLightmapArray, descriptor, 
                FilterMode.Bilinear, TextureWrapMode.Clamp, name: "RayTracing_StaticLightmapArray");
            
            StaticLightmapSize = new Vector3Int(width, height, slices);
        }

        public int AddMaterial(Material material) {
            int index = _context.SourceMaterialsList.IndexOf(material);
            if (index >= 0) {
                return index;
            }

            _context.SourceMaterialsList.Add(material);
            return _context.SourceMaterialsList.Count - 1;
        }
        
        public void CompleteBuilding() {
            SealGrid(_context);
            SealMaterials();
            
            Grid0Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _context.Grid0.Length, sizeof(uint));
            Grid0MetaBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _context.Grid0Meta.Length,
                VoxelMetaInfo0.SIZE);
            Grid1Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _context.Grid1.Length, sizeof(uint));
            Grid1MetaBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _context.Grid1Meta.Length,
                VoxelMetaInfo1.SIZE);
            TrianglesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _context.Triangles.Length,
                TriangleMetaInfo.SIZE);
            MaterialsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _context.MaterialsList.Count,
                MaterialInfo.SIZE);

            Grid0Buffer.SetData(_context.Grid0);
            Grid0MetaBuffer.SetData(_context.Grid0Meta);
            Grid1Buffer.SetData(_context.Grid1);
            Grid1MetaBuffer.SetData(_context.Grid1Meta);
            TrianglesBuffer.SetData(_context.Triangles);
            MaterialsBuffer.SetData(_context.MaterialsList);

            _context = null;
            Ready = true;
        }

        private void SealMaterials() {
            List<Texture2D> baseMaps = new();
            List<Texture2D> normalMaps = new();
            List<Texture2D> metallicOcclusionMaps = new();

            int AddTexture(List<Texture2D> list, Texture2D texture) {
                if (texture == null) {
                    return -1;
                }

                int index = list.IndexOf(texture);
                if (index >= 0) {
                    return index;
                }

                list.Add(texture);
                return list.Count - 1;
                
            }
            
            for (int i = 0; i < _context.SourceMaterialsList.Count; i++) {
                Material material = _context.SourceMaterialsList[i];
                Texture2D baseMap = material.GetTexture(ObjectShaderParams.BaseMap) as Texture2D;
                Texture2D normalMap = material.GetTexture(ObjectShaderParams.BumpMap) as Texture2D;
                Texture2D metallicOcclusionMap = material.GetTexture(ObjectShaderParams.MetallicGlossMap) as Texture2D;
                
                int baseMapIndex = AddTexture(baseMaps, baseMap);
                int normalMapIndex = AddTexture(normalMaps, normalMap);
                int metallicOcclusionMapIndex = AddTexture(metallicOcclusionMaps, metallicOcclusionMap);
                
                _context.MaterialsList.Add(new MaterialInfo() {
                    BaseMapIndex = baseMapIndex,
                    NormalMapIndex = normalMapIndex,
                    MetallicOcclusionMapIndex = metallicOcclusionMapIndex,
                });
            }
            
            AllocateBaseMapArray(baseMaps.Count);
            AllocateNormalMapArray(normalMaps.Count);
            AllocateMetallicOcclusionMapArray(metallicOcclusionMaps.Count);
            
            // Заполняем массивы текстур.
            CopyTextureToArray(baseMaps, BaseMapResolution, RenderTextureFormat.ARGBHalf, BaseMapArray);
            CopyTextureToArray(normalMaps, NormalMapResolution, RenderTextureFormat.ARGBHalf, NormalMapArray);
            CopyTextureToArray(metallicOcclusionMaps, MetallicOcclusionMapResolution, RenderTextureFormat.ARGBHalf, MetallicOcclusionMapArray);
        }

        private void CopyTextureToArray(List<Texture2D> textureArray, int targetResolution, RenderTextureFormat targetFormat, RTHandle targetArray) {
            for (int i = 0; i < textureArray.Count; i++) {
                RenderTexture rt = RenderTexture.GetTemporary(
                    targetResolution, targetResolution, 0, targetFormat);
                Graphics.Blit(textureArray[i], rt);
                //rt.ConvertToEquirect(rt); // Не обязательно, если не требуется преобразование формата

                Graphics.CopyTexture(rt, 0, 0, targetArray, i, 0);
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private void AllocateBaseMapArray(int slices) {
            RenderTextureDescriptor descriptor = GetTextureArrayDescriptor(
                BaseMapResolution, slices, RenderTextureFormat.ARGBHalf);
            
            RenderingUtils.ReAllocateHandleIfNeeded(ref BaseMapArray, descriptor, 
                FilterMode.Bilinear, TextureWrapMode.Repeat, name: "RayTracing_Geometry_BaseMapArray");
            
            BaseMapSize = new Vector3Int(BaseMapResolution, BaseMapResolution, slices);
        }
        
        private void AllocateNormalMapArray(int slices) {
            RenderTextureDescriptor descriptor = GetTextureArrayDescriptor(
                NormalMapResolution, slices, RenderTextureFormat.ARGBHalf);
            
            RenderingUtils.ReAllocateHandleIfNeeded(ref NormalMapArray, descriptor, 
                FilterMode.Bilinear, TextureWrapMode.Repeat, name: "RayTracing_Geometry_NormalMapArray");
            
            NormalMapSize = new Vector3Int(NormalMapResolution, NormalMapResolution, slices);
        }
        
        private void AllocateMetallicOcclusionMapArray(int slices) {
            RenderTextureDescriptor descriptor = GetTextureArrayDescriptor(
                MetallicOcclusionMapResolution, slices, RenderTextureFormat.ARGBHalf);
            
            RenderingUtils.ReAllocateHandleIfNeeded(ref MetallicOcclusionMapArray, descriptor, 
                FilterMode.Bilinear, TextureWrapMode.Repeat, name: "RayTracing_Geometry_MetallicOcclusionMapArray");
            
            MetallicOcclusionMapSize = new Vector3Int(MetallicOcclusionMapResolution, MetallicOcclusionMapResolution, slices);
        }

        private RenderTextureDescriptor GetTextureArrayDescriptor(int resolution, int slices, RenderTextureFormat format) {
            return new RenderTextureDescriptor(resolution, resolution, format, 0) {
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = slices,
                enableRandomWrite = false,
                useMipMap = false, // ?
                autoGenerateMips = false,
                sRGB = true,
                /*msaaSamples = 1,
                bindMS = false,
                memoryless = RenderTextureMemoryless.None,
                useDynamicScale = false*/
            };
        }

        /*private void Process(RayTracingDataBuildingContext context) {
            // По часовой стрелке.
            ProcessTriangle(context, new TriangleMetaInfo() {
                V0 = new Vector3(3.75f, 2.4f, 6.75f),
                V1 = new Vector3(5.5f, 1f, 6.75f),
                V2 = new Vector3(3.75f, 1f, 6.75f)
            });

            ProcessTriangle(context, new TriangleMetaInfo() {
                V0 = new Vector3(5.5f, 2.4f, 6.75f),
                V1 = new Vector3(5.5f, 1f, 6.75f),
                V2 = new Vector3(3.75f, 2.4f, 6.75f)
            });

            /*ProcessTriangle(context, new TriangleMetaInfo() {
                V0 = new Vector3(0.9f, 0.6f, 3),
                V1 = new Vector3(1f, 0.6f, 3),
                V2 = new Vector3(0.7f, 0.7f, 3)
            });#1#

          
        }*/

        private void SealGrid(RayTracingDataBuildingContext context) {
            int triangleBufferIndex = 0;
            int grid1MetaIndex = 0;

            for (int y0 = 0; y0 < Grid0Size.y; y0++)
            for (int z0 = 0; z0 < Grid0Size.z; z0++)
            for (int x0 = 0; x0 < Grid0Size.x; x0++) {
                Vector3Int voxel0Coords = new Vector3Int(x0, y0, z0);
                int voxel0Index = RayTracingUtility.GetVoxelIndex(Grid0Size, voxel0Coords);
                int grid1MetaStartIndex = grid1MetaIndex;

                for (int y1 = 0; y1 <= Voxels1InsideVoxel0; y1++)
                for (int z1 = 0; z1 <= Voxels1InsideVoxel0; z1++)
                for (int x1 = 0; x1 <= Voxels1InsideVoxel0; x1++) {
                    Vector3Int voxel1Coords = voxel0Coords * Voxels1InsideVoxel0 + new Vector3Int(x1, y1, z1);
                    int voxel1Index = RayTracingUtility.GetVoxelIndex(Grid1Size, voxel1Coords);

                    if (!context.Voxel1Triangles.TryGetValue(voxel1Index, out var triangles)) {
                        continue;
                    }

                    int startIndex = triangleBufferIndex;

                    foreach (TriangleMetaInfo triangleInfo in triangles) {
                        context.TrianglesList.Add(triangleInfo);
                        triangleBufferIndex++;
                        //context.Triangles[triangleBufferIndex++] = triangleInfo;
                    }

                    context.Grid1MetaList.Add(new VoxelMetaInfo1() {
                        VoxelIndex = voxel1Index,
                        StartIndex = (uint)startIndex,
                        EndIndex = (uint)triangleBufferIndex
                    });
                    grid1MetaIndex++;
                    /*context.Grid1Meta[grid1MetaIndex++] = new VoxelMetaInfo1() {
                        VoxelIndex = voxel1Index,
                        StartIndex = (uint)startIndex,
                        EndIndex = (uint)triangleBufferIndex
                    };*/
                }

                if (voxel0Index >= context.Grid0Meta.Length) {
                    Debug.LogError($"voxel0Index {voxel0Index} >= context.Grid0Meta.Length {context.Grid0Meta.Length}");
                    continue;
                }

                context.Grid0Meta[voxel0Index] = new VoxelMetaInfo0() {
                    StartIndex = (uint)grid1MetaStartIndex,
                    EndIndex = (uint)grid1MetaIndex
                };

                context.Triangles = context.TrianglesList.ToArray();
                context.Grid1Meta = context.Grid1MetaList.ToArray();
            }
        }

        private void ProcessTriangle(RayTracingDataBuildingContext context, TriangleMetaInfo triangle) {
            Vector3 minLS = GetMin(triangle) - GridOriginWS;
            Vector3 maxLS = GetMax(triangle) - GridOriginWS;

            Vector3Int minVoxel1 = new Vector3Int(
                FloorToIntWithEpsilon(minLS.x / Grid1VoxelSize),
                FloorToIntWithEpsilon(minLS.y / Grid1VoxelSize),
                FloorToIntWithEpsilon(minLS.z / Grid1VoxelSize));

            Vector3Int maxVoxel1 = new Vector3Int(
                FloorToIntWithEpsilon(maxLS.x / Grid1VoxelSize),
                FloorToIntWithEpsilon(maxLS.y / Grid1VoxelSize),
                FloorToIntWithEpsilon(maxLS.z / Grid1VoxelSize));

            for (int y = minVoxel1.y; y <= maxVoxel1.y; y++)
            for (int z = minVoxel1.z; z <= maxVoxel1.z; z++)
            for (int x = minVoxel1.x; x <= maxVoxel1.x; x++) {
                Vector3 voxelMinWS = new Vector3(x * Grid1VoxelSize, y * Grid1VoxelSize, z * Grid1VoxelSize) +
                                     GridOriginWS;
                Vector3 voxelMaxWS = voxelMinWS + Vector3.one * Grid1VoxelSize;

                if (TriangleIntersectsAABB(triangle.V0, triangle.V1, triangle.V2, voxelMinWS, voxelMaxWS)) {
                    Vector3 worldPos = voxelMinWS + Vector3.one * (Grid1VoxelSize * 0.5f);

                    int voxel1Index = RayTracingUtility.GetVoxelIndex(Grid1Size, new Vector3Int(x, y, z));
                    
                    RayTracingUtility.WriteGridValue(context.Grid1, voxel1Index);

                    Vector3Int voxel0Coords =
                        RayTracingUtility.GetVoxelCoordsByWorldPos(GridOriginWS, Grid0VoxelSize, worldPos);
                    int voxel0Index = RayTracingUtility.GetVoxelIndex(Grid0Size, voxel0Coords);
                    //Debug.Log($"Triangle {triangle.V0} {triangle.V1} {triangle.V2} intersects voxel1 {voxel1Index} at {x} {y} {z} and voxel0 {voxel0Index} at {voxel0Coords}");
                    RayTracingUtility.WriteGridValue(context.Grid0, voxel0Index);

                    context.AddTriangle(voxel1Index, triangle);
                }
            }
        }
        
        private int FloorToIntWithEpsilon(float value) {
            return Mathf.FloorToInt(value + 1e-6f);
        }

        private Vector3 GetMin(TriangleMetaInfo triangle) {
            return new Vector3(
                Mathf.Min(triangle.V0.x, Mathf.Min(triangle.V1.x, triangle.V2.x)),
                Mathf.Min(triangle.V0.y, Mathf.Min(triangle.V1.y, triangle.V2.y)),
                Mathf.Min(triangle.V0.z, Mathf.Min(triangle.V1.z, triangle.V2.z)));
        }

        private Vector3 GetMax(TriangleMetaInfo triangle) {
            return new Vector3(
                Mathf.Max(triangle.V0.x, Mathf.Max(triangle.V1.x, triangle.V2.x)),
                Mathf.Max(triangle.V0.y, Mathf.Max(triangle.V1.y, triangle.V2.y)),
                Mathf.Max(triangle.V0.z, Mathf.Max(triangle.V1.z, triangle.V2.z)));
        }

        private bool TriangleIntersectsAABB(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 boxMin, Vector3 boxMax) {
            // 1. Быстрая проверка AABB vs AABB (BBox треугольника vs Box)
            Vector3 triMin = Vector3.Min(v0, Vector3.Min(v1, v2));
            Vector3 triMax = Vector3.Max(v0, Vector3.Max(v1, v2));
            if (triMax.x < boxMin.x || triMin.x > boxMax.x) return false;
            if (triMax.y < boxMin.y || triMin.y > boxMax.y) return false;
            if (triMax.z < boxMin.z || triMin.z > boxMax.z) return false;

            // 2. SAT тест
            // Центр и половина размеров бокса
            Vector3 boxCenter = (boxMin + boxMax) * 0.5f;
            Vector3 boxHalfSize = (boxMax - boxMin) * 0.5f;

            // Переносим треугольник в систему координат бокса
            v0 -= boxCenter;
            v1 -= boxCenter;
            v2 -= boxCenter;

            Vector3[] verts = { v0, v1, v2 };
            Vector3[] edges = { v1 - v0, v2 - v1, v0 - v2 };

            // Лямбда: проверка проекции на ось
            bool OverlapOnAxis(Vector3 axis) {
                if (axis == Vector3.zero) return true; // игнорируем нулевые оси

                float minTri = float.MaxValue, maxTri = float.MinValue;
                foreach (var v in verts) {
                    float proj = Vector3.Dot(v, axis);
                    minTri = Mathf.Min(minTri, proj);
                    maxTri = Mathf.Max(maxTri, proj);
                }

                // Проекция бокса
                float r = boxHalfSize.x * Mathf.Abs(axis.x) +
                          boxHalfSize.y * Mathf.Abs(axis.y) +
                          boxHalfSize.z * Mathf.Abs(axis.z);

                return !(minTri > r || maxTri < -r);
            }

            // Тестируем 13 осей
            // 3 оси AABB
            if (!OverlapOnAxis(Vector3.right)) return false;
            if (!OverlapOnAxis(Vector3.up)) return false;
            if (!OverlapOnAxis(Vector3.forward)) return false;

            // Нормаль треугольника
            Vector3 normal = Vector3.Cross(edges[0], edges[1]);
            if (!OverlapOnAxis(normal)) return false;

            // 9 перекрестных осей
            Vector3[] boxAxes = { Vector3.right, Vector3.up, Vector3.forward };
            foreach (var e in edges) {
                foreach (var a in boxAxes) {
                    Vector3 axis = Vector3.Cross(e, a);
                    if (!OverlapOnAxis(axis)) return false;
                }
            }

            return true;
        }
    }
}