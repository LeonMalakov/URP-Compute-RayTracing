using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace URPCustom {
    public class PortalMaskResourceData : UniversalResourceDataBase {
        // Маска порталов.
        public TextureHandle PortalMaskTexture;
        
        // Глубина маски порталов (глубина до портальных поверхностей).
        public TextureHandle PortalMaskDepthTexture;

        // Глубина всех портальных камер и основной камеры.
        public TextureHandle CameraDepthTexture;
        public TextureHandle CameraDepthAttachment;

        // Общая normal/gbuffer текстура для всех портальный и основной камер.
        public TextureHandle CameraNormalsTexture;

        // Временные текстуры для копирования.
        public TextureHandle DrawDepthTexture;
        public TextureHandle DrawDepthTempTexture;
        
        // For read purposes.
        // Can be null, can contain nullHandles.
        public TextureHandle[] GBufferTextures; 
        
        public override void Reset() {
            PortalMaskTexture = TextureHandle.nullHandle;
            PortalMaskDepthTexture = TextureHandle.nullHandle;
            CameraNormalsTexture = TextureHandle.nullHandle;
            CameraDepthTexture = TextureHandle.nullHandle;
            CameraDepthAttachment = TextureHandle.nullHandle;
            DrawDepthTexture = TextureHandle.nullHandle;
            DrawDepthTempTexture = TextureHandle.nullHandle;
            GBufferTextures = null;
        }
    }
}