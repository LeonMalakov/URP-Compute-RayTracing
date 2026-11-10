using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Boil.RenderPipeline.RayTracing {
    [Serializable]
    public class RayTracingSettings {
        [FormerlySerializedAs("computeShader")] public ComputeShader shadowsComputeShader;
        public ComputeShader reflectionsComputeShader;
        public ComputeShader globalIlluminationComputeShader;
        public bool debugOutputTexture = false;
    }
}