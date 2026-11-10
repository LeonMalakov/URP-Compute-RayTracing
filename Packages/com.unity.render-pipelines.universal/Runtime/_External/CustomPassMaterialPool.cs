using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPCustom {
    public class CustomPassMaterialPool : ScriptableRenderPass {
        private readonly List<Material> _materials;
        
        private readonly Shader _shader;
        private readonly int _maxMaterials;
        private bool _disposed;
        private int _materialIndex;

        public CustomPassMaterialPool(Shader shader, int maxMaterials, int warmUpMaterials) {
            _shader = shader;
            _maxMaterials = maxMaterials;
            _materials = new List<Material>(maxMaterials);
            
            for (int i = 0; i < warmUpMaterials; i++) {
                CreateNewMaterial();
            }
        }
        
        public void Dispose() {
            _disposed = true;
            
            foreach (Material material in _materials) {
                CoreUtils.Destroy(material);
            }
            _materials.Clear();
        }

        public void ResetUsage() {
            _materialIndex = 0;
        }

        public Material GetNextMaterial() {
            int index = _materialIndex++;

            if (index >= _maxMaterials) {
                LogErrorMaxMaterials();
                return null;
            }
            
            return GetPooledMaterial(index);
        }
        
        private Material GetPooledMaterial(int index) {
            if (index >= _materials.Count) {
                if (!CreateNewMaterial()) {
                    return null;
                }
            }

            return _materials[index];
        }

        private bool CreateNewMaterial() {
            if (_disposed) {
                return false;
            }
            
            if (_shader == null) {
                return false;
            }
            
            Material material = CoreUtils.CreateEngineMaterial(_shader);
            _materials.Add(material);
            return true;
        }
        
        private static void LogErrorMaxMaterials() {
            Debug.LogError("Max materials used");
        }
    }
}