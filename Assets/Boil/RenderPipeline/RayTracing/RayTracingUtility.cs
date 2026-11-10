using UnityEngine;

namespace Boil.RenderPipeline.RayTracing {
    public static class RayTracingUtility {
        public static int GetVoxelIndex(Vector3Int gridSize, Vector3Int voxelCoords) {
            return voxelCoords.x + voxelCoords.z * gridSize.x + voxelCoords.y * gridSize.x * gridSize.z;
        }
        
        public static Vector3Int GetVoxelCoordsByWorldPos(Vector3 gridOriginWS, float voxelSize, Vector3 worldPos) {
            Vector3 localPos = worldPos - gridOriginWS;
            Vector3Int coords = new Vector3Int(
                Mathf.FloorToInt(localPos.x / voxelSize),
                Mathf.FloorToInt(localPos.y / voxelSize),
                Mathf.FloorToInt(localPos.z / voxelSize));
            return coords;
        }

        public static void WriteGridValue(uint[] grid, int voxelIndex) {
            int wordIndex = voxelIndex / 32;
            int bitIndex = voxelIndex % 32;
            
            /*if(wordIndex < 0 || wordIndex >= grid.Length) {
                Debug.LogError($"[RayTracingUtility] WriteGridValue: wordIndex {wordIndex} for voxelIndex {voxelIndex} out of bounds (grid length: {grid.Length}).");
                return;
            }*/
            
            grid[wordIndex] |= (uint)(1 << bitIndex);
        }
    }
}