using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Boil.RenderPipeline.RayTracing {
    public class RayTracingGlobalIlluminationRenderPass : ScriptableRenderPass, IDisposable {
        [StructLayout(LayoutKind.Sequential)]
        public struct RayInfo {
            public const int SIZE = sizeof(float) * 3 * 2 + sizeof(float);

            public Vector3 Origin;
            public Vector3 Direction;
            public float Energy;
        }

        private static class ShaderParams {
            public static readonly string KernelName = "CSMain";
            public static readonly Vector2Int NumThreads = new Vector2Int(8, 1);

            public static readonly int Result = Shader.PropertyToID("_Result");
            public static readonly int ResultTextureSize = Shader.PropertyToID("_ResultTextureSize");
            public static readonly int Rays = Shader.PropertyToID("_Rays");
            public static readonly int RayCount = Shader.PropertyToID("_RayCount");
        }

        private class PassData {
            public UniversalCameraData CameraData;
            public ComputeShader ComputeShader;
            public int KernelIndex;
            public RayTracingPassData RayTracingData;
            public TextureHandle StaticLightmapArray;
            public Vector3Int StaticLightmapSize;
            public BufferHandle Rays;
            public int RayCount;
        }

        private const int MAX_RAYS = 8192;

        private readonly RayTracingSettings _settings;

        private GraphicsBuffer _raysBuffer;
        private readonly List<RayInfo> _rays = new();


        public RayTracingGlobalIlluminationRenderPass(RayTracingSettings settings) {
            _settings = settings;

            _raysBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MAX_RAYS, RayInfo.SIZE);
        }

        public void Dispose() {
            _raysBuffer?.Release();
        }

        private void FillSunRaysForSphere(Vector3 spherePos, float sphereRadius, Vector3 lightDirection,
            List<RayInfo> rays, int maxRays, float sunAngularRadius = 0.53f) {
            rays.Clear();
            lightDirection = lightDirection.normalized;
            float sunAngleRad = sunAngularRadius * Mathf.Deg2Rad;

            float offset = 1f / maxRays;
            float increment = Mathf.PI * (3f - Mathf.Sqrt(5f));

            for (int i = 0; i < maxRays; i++) {
                // Точка на поверхности сферы
                float y = (i * offset) + (offset / 2); // от 0 до 1
                float r = Mathf.Sqrt(Mathf.Max(0f, 1 - y * y));
                float phi = i * increment;

                float x = Mathf.Cos(phi) * r;
                float z = Mathf.Sin(phi) * r;
                Vector3 pointOnSphere = spherePos + new Vector3(x, y, z) * sphereRadius;

                // Направление луча внутри конуса вокруг lightDirection
                float t = (i + 0.5f) / maxRays;
                float theta = sunAngleRad * Mathf.Sqrt(t); // угол от центра конуса
                float psi = 2 * Mathf.PI * t; // азимут

                // Ортонормированный базис для lightDirection
                Vector3 axis = Vector3.Cross(lightDirection, Vector3.up);
                if (axis.sqrMagnitude < 1e-4f) axis = Vector3.Cross(lightDirection, Vector3.right);
                axis.Normalize();
                Vector3 binormal = Vector3.Cross(axis, lightDirection);

                // Вектор направления в конусе
                Vector3 rayDir =
                    Quaternion.AngleAxis(theta * Mathf.Rad2Deg, axis) *
                    Quaternion.AngleAxis(psi * Mathf.Rad2Deg, lightDirection) *
                    lightDirection;

                rays.Add(new RayInfo {
                    Origin = pointOnSphere,
                    Direction = rayDir.normalized,
                    Energy = 1.0f
                });
            }
        }

        private void MoveRaysToGridBounds() {
            RayTracingDataStorage dataStorage = RayTracingVolume.Instance.DataStorage;
            
            Vector3 gridMin = dataStorage.GridOriginWS;
            Vector3 gridMax = dataStorage.GridOriginWS + new Vector3(
                dataStorage.Grid0Size.x * dataStorage.Grid0VoxelSize,
                dataStorage.Grid0Size.y * dataStorage.Grid0VoxelSize,
                dataStorage.Grid0Size.z * dataStorage.Grid0VoxelSize);
            
            for(int i = 0; i < _rays.Count; i++) {
                RayInfo ray = _rays[i];
                float tMin = float.NegativeInfinity;
                float tMax = float.PositiveInfinity;
                for (int j = 0; j < 3; j++) {
                    if (Mathf.Abs(ray.Direction[j]) < 1e-6f) {
                        // Луч параллелен плоскости
                        if (ray.Origin[j] < gridMin[j] || ray.Origin[j] > gridMax[j]) {
                            tMin = float.PositiveInfinity;
                            tMax = float.NegativeInfinity;
                            break;
                        }
                    } else {
                        float t1 = (gridMin[j] - ray.Origin[j]) / ray.Direction[j];
                        float t2 = (gridMax[j] - ray.Origin[j]) / ray.Direction[j];
                        if (t1 > t2) {
                            (t1, t2) = (t2, t1);
                        }
                        tMin = Mathf.Max(tMin, t1);
                        tMax = Mathf.Min(tMax, t2);
                        if (tMin > tMax) {
                            break;
                        }
                    }
                }
                if (tMin <= tMax && tMax >= 0) {
                    float t = tMin >= 0 ? tMin : tMax;
                    ray.Origin += ray.Direction * (t + 0.01f); // Смещаем луч чуть внутрь объема
                    _rays[i] = ray;
                } else {
                    // Луч не пересекает объем, деактивируем его
                    ray.Energy = 0f;
                    _rays[i] = ray;
                }
            }
        }

        private void DrawRayGizmos(Color color) {
            for(int i = 0; i < _rays.Count; i++) {
                RayInfo ray = _rays[i];
                Debug.DrawRay(ray.Origin, ray.Direction * 2, color, 60f);
            }
        }


        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
            if (RayTracingVolume.Instance == null || RayTracingVolume.Instance.DataStorage == null) {
                return;
            }

            RayTracingDataStorage dataStorage = RayTracingVolume.Instance.DataStorage;

            if (!dataStorage.Ready) {
                return;
            }

            if (_rays.Count == 0) {
                FillSunRaysForSphere(new Vector3(4.75f, 1.5f, 4.5f), 25f,
                    RayTracingVolume.Instance.DataStorage.SunLightDirection, _rays, MAX_RAYS);

                //DrawRayGizmos(Color.red);
                
                MoveRaysToGridBounds();
                
                DrawRayGizmos(Color.blue);
            }

            const string passTitle = "RayTracing_GlobalIllumination";

            using (IComputeRenderGraphBuilder builder = renderGraph.AddComputePass(passTitle, out PassData passData)) {
                //UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                /*UniversalRenderingData universalRenderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();*/
                //URPCustom.RayTracingUniversalData rayTracingData = frameData.GetOrCreate<URPCustom.RayTracingUniversalData>();

                passData.CameraData = cameraData;
                passData.ComputeShader = _settings.globalIlluminationComputeShader;
                passData.KernelIndex = passData.ComputeShader.FindKernel(ShaderParams.KernelName);

                passData.RayTracingData = RayTracingPassCommon.ImportAndUseRayTracingData(dataStorage, renderGraph, builder);
                
                passData.StaticLightmapArray = renderGraph.ImportTexture(dataStorage.StaticLightmapArray);
                passData.StaticLightmapSize = dataStorage.StaticLightmapSize;

                builder.UseTexture(passData.StaticLightmapArray, AccessFlags.Write);

                _raysBuffer.SetData(_rays);
                passData.Rays = renderGraph.ImportBuffer(_raysBuffer);
                passData.RayCount = _rays.Count;


                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, ComputeGraphContext context) => ExecutePass(data, context));
            }
        }

        private static void ExecutePass(PassData data, ComputeGraphContext context) {
            RayTracingPassCommon.SetupRayTracingData(context, data.ComputeShader, data.KernelIndex,
                ref data.RayTracingData);
            SetupData(data, context);

            int threadGroupX = Mathf.CeilToInt(data.RayCount / (float)ShaderParams.NumThreads.x);
            context.cmd.DispatchCompute(data.ComputeShader, data.KernelIndex, threadGroupX, 1, 1);
        }

        private static void SetupData(PassData data, ComputeGraphContext context) {
            ComputeCommandBuffer cmd = context.cmd;
            cmd.SetComputeTextureParam(data.ComputeShader, data.KernelIndex, ShaderParams.Result,
                data.StaticLightmapArray);
            cmd.SetComputeIntParams(data.ComputeShader, ShaderParams.ResultTextureSize, data.StaticLightmapSize.x,
                data.StaticLightmapSize.y, data.StaticLightmapSize.z);
            cmd.SetComputeBufferParam(data.ComputeShader, data.KernelIndex, ShaderParams.Rays, data.Rays);
            cmd.SetComputeIntParam(data.ComputeShader, ShaderParams.RayCount, data.RayCount);
        }
    }
}