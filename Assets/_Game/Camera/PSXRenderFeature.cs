// =============================================================================
// PSXRenderFeature.cs
// -----------------------------------------------------------------------------
// URP Render Feature: düşük çözünürlüklü render texture'a render et, sonra
// dithering/snapping shader ile ekrana bas. PSX tarzı retro görünüm.
//
// Nasıl çalışır:
//   1) URP renderer'a bu feature eklenir.
//   2) Render request sırasında Blit() ile kaynak -> low-res RT.
//   3) Sonra low-res RT -> ekran (snapping shader ile).
//
// Inspector'dan (feature settings):
//   - Düşük çözünürlük (320x240, 480x360, 640x360 vb.)
//   - Snap step (vertex/uv quantization)
//   - Color depth (sınırlı palet)
//   - Dithering açık/kapalı
// ayarlanabilir.
// =============================================================================

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GoodNightMyAngel.CameraSys
{
    public class PSXRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("Render texture'ın çözünürlüğü (X).")]
            public int lowResWidth = 480;

            [Tooltip("Render texture'ın çözünürlüğü (Y).")]
            public int lowResHeight = 270;

            [Tooltip("Renk derinliği (her kanal için bit). 4-5 PSX tarzı.")]
            [Range(2, 8)] public int colorBits = 5;

            [Tooltip("Dithering uygulansın mı?")]
            public bool dithering = true;

            [Tooltip("Dithering şiddeti (0-1).")]
            [Range(0f, 1f)] public float ditherAmount = 0.06f;
        }

        public Settings settings = new Settings();

        private PSXRenderPass _pass;
        private Material _material;
        private Shader _shader;

        public override void Create()
        {
            // Shader'ı runtime'da yükle (RenderFeature/PSXSnapping.shader)
            _shader = Shader.Find("GoodNight/PSXSnapping");
            if (_shader == null)
            {
                Debug.LogWarning("[PSXRenderFeature] GoodNight/PSXSnapping shader bulunamadı. " +
                                 "Render feature pasif.");
                return;
            }
            _material = CoreUtils.CreateEngineMaterial(_shader);
            _pass = new PSXRenderPass(_material, settings);
            _pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || _material == null) return;
            _pass.Setup(renderer.cameraColorTargetHandle);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
        }

        // --------------------------------------------------------------------
        // PASS
        // --------------------------------------------------------------------
        private class PSXRenderPass : ScriptableRenderPass
        {
            private Material _mat;
            private Settings _settings;
            private RTHandle _source;
            private RTHandle _lowRes;
            private string _profilerTag = "PSXRenderPass";

            public PSXRenderPass(Material mat, Settings settings)
            {
                _mat = mat;
                _settings = settings;
            }

            public void Setup(RTHandle source)
            {
                _source = source;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.width = Mathf.Max(64, _settings.lowResWidth);
                desc.height = Mathf.Max(64, _settings.lowResHeight);
                desc.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref _lowRes, desc, FilterMode.Point,
                    TextureWrapMode.Clamp, name: "_PSXLowRes");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_mat == null || _source == null || _lowRes == null) return;

                CommandBuffer cmd = CommandBufferPool.Get(_profilerTag);

                // 1) Ekranı -> low-res RT
                Blitter.BlitCameraTexture(cmd, _source, _lowRes);

                // 2) Shader parametrelerini güncelle
                _mat.SetFloat("_ColorBits", _settings.colorBits);
                _mat.SetFloat("_Dither", _settings.dithering ? _settings.ditherAmount : 0f);
                _mat.SetVector("_LowRes", new Vector2(_settings.lowResWidth, _settings.lowResHeight));

                // 3) Low-res RT -> ekran
                Blitter.BlitCameraTexture(cmd, _lowRes, _source, _mat, 0);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Cleanup()
            {
                _lowRes?.Release();
            }
        }
    }
}
