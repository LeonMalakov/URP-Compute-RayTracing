using UnityEngine.Device;
using UnityEngine.Rendering;

namespace Boil.RenderPipeline.Config {
    public static class DeferredConfigLocal {
        internal static bool IsOpenGL { get; set; }
        
        // DX10 uses SM 4.0. However URP shaders requires SM 4.5 or will use fallback to SM 2.0 shaders otherwise.
        // We will consider deferred renderer is not available when SM 2.0 shaders run.
        internal static bool IsDX10 { get; set; }

        static DeferredConfigLocal() {
            IsOpenGL = SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3;

            // Cachre result for DX10 platform too. Same reasons as above.
            IsDX10 = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 && SystemInfo.graphicsShaderLevel <= 40;
        }
    }
}