using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace URPCustom {
    public class RayTracingUniversalData : ContextItem {
        public TextureHandle ShadowMap;
        public TextureHandle ReflectionsMap;

        public override void Reset() {
            ShadowMap = TextureHandle.nullHandle;
            ReflectionsMap = TextureHandle.nullHandle;
        }
    }
}