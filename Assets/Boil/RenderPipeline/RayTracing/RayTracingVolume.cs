using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Boil.RenderPipeline.RayTracing {
    public class RayTracingVolume : MonoBehaviour {
        [SerializeField] private Transform root;
        [SerializeField] private Transform sunLight;
        
        public static RayTracingVolume Instance { get; private set; }
        
        public readonly RayTracingDataStorage DataStorage = new();

        private bool _renderEnabled;
        
        private void Start() {
            if (Instance != null) {
                Debug.LogError("Multiple RayTracingVolume instances detected. There should only be one instance in the scene.");
            }
            Instance = this;
            
            // BuildGeometry();
        }

        private void OnDestroy() {
            if (Instance == this) {
                Instance = null;
            }
            DataStorage.Dispose();
        }

        [ContextMenu("Build Geometry")]
        public void BuildGeometry() {
            Debug.Log("[RayTracingVolume] Start building geometry data.");
            
            DataStorage.StartBuilding();
            DataStorage.SunLightDirection = sunLight.forward;
            CreateStaticLightMaps();
            ProcessTriangles();
            DataStorage.CompleteBuilding();
            
            Debug.Log("[RayTracingVolume] Complete building geometry data.");
        }

        private void ProcessTriangles() {
            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(includeInactive: false);

            foreach (MeshFilter meshFilter in meshFilters) {
                MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                if (meshRenderer != null) {
                    ProcessObjectWithMesh(meshFilter, meshRenderer);
                }
            }
        }

        private void ProcessObjectWithMesh(MeshFilter meshFilter, MeshRenderer meshRenderer) {
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null) return;

            Matrix4x4 localToWorld = meshFilter.transform.localToWorldMatrix;

            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] lightmapUV = mesh.uv2;
            Vector4 lightmapScaleOffset = meshRenderer.lightmapScaleOffset;
            int lightmapIndex = meshRenderer.lightmapIndex;
            Vector2[] uv = mesh.uv;
            
            bool hasLightmapUV = lightmapUV != null && lightmapUV.Length > 0;
            /*if (lightmapUV == null || lightmapUV.Length == 0) {
                return;
            }
            
            if(uv == null || uv.Length == 0) {
                return;
            }*/

            if (lightmapIndex >= DataStorage.StaticLightmapSize.z || lightmapIndex < 0) {
                lightmapIndex = -1;
            }
            
            int materialIndex = meshRenderer.sharedMaterial != null
                ? DataStorage.AddMaterial(meshRenderer.sharedMaterial)
                : -1;
            
            for (int i = 0; i < triangles.Length; i += 3) {
                Vector3 v0 = localToWorld.MultiplyPoint(vertices[triangles[i]]);
                Vector3 v1 = localToWorld.MultiplyPoint(vertices[triangles[i + 1]]);
                Vector3 v2 = localToWorld.MultiplyPoint(vertices[triangles[i + 2]]);

                Vector2 lightmapUV0 = hasLightmapUV ? lightmapUV[triangles[i]] : Vector2.zero;
                Vector2 lightmapUV1 = hasLightmapUV ? lightmapUV[triangles[i + 1]] : Vector2.zero;
                Vector2 lightmapUV2 = hasLightmapUV ? lightmapUV[triangles[i + 2]] : Vector2.zero;
                
                lightmapUV0 = new Vector2(
                    lightmapUV0.x * lightmapScaleOffset.x + lightmapScaleOffset.z,
                    lightmapUV0.y * lightmapScaleOffset.y + lightmapScaleOffset.w);
                
                lightmapUV1 = new Vector2(
                    lightmapUV1.x * lightmapScaleOffset.x + lightmapScaleOffset.z,
                    lightmapUV1.y * lightmapScaleOffset.y + lightmapScaleOffset.w);
                
                lightmapUV2 = new Vector2(
                    lightmapUV2.x * lightmapScaleOffset.x + lightmapScaleOffset.z,
                    lightmapUV2.y * lightmapScaleOffset.y + lightmapScaleOffset.w);
                
                Vector2 firstUV0 = uv[triangles[i]];
                Vector2 firstUV1 = uv[triangles[i + 1]];
                Vector2 firstUV2 = uv[triangles[i + 2]];
                
                /*Vector3 n0 = localToWorld.MultiplyVector(normals[triangles[i]]).normalized;
                Vector3 n1 = localToWorld.MultiplyVector(normals[triangles[i + 1]]).normalized;
                Vector3 n2 = localToWorld.MultiplyVector(normals[triangles[i + 2]]).normalized;*/

                DataStorage.AddTriangle(new TriangleMetaInfo() {
                    V0 = v0,
                    V1 = v1,
                    V2 = v2,
                    
                    LightmapIndex = lightmapIndex,
                    LightmapUV0 = lightmapUV0,
                    LightmapUV1 = lightmapUV1,
                    LightmapUV2 = lightmapUV2,
                    
                    MaterialIndex = materialIndex,
                    UV0 = firstUV0,
                    UV1 = firstUV1,
                    UV2 = firstUV2,
                });
            }
        }

        private void CreateStaticLightMaps() {
            DataStorage.AddStaticLightMaps(LightmapSettings.lightmaps);
        }
    }
}