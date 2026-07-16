// =============================================================================
// PSXRenderFeature.cs
// -----------------------------------------------------------------------------
// URP Render Feature (Render Graph API, Unity 6+): düşük çözünürlüklü render
// texture'a render et, sonra dithering/snapping shader ile ekrana bas.
// PSX tarzı retro görünüm.
//
// Not: Unity 6'da ScriptableRenderPass.OnCameraSetup/Execute deprecated olmuş,
// bunun yerine Render Graph API'si (RecordRenderGraph) kullanılıyor.
// Eski yöntem de hala çalışıyor (uyarı verir), ama uyarıları önlemek ve
// geleceğe dönük olmak için yeni API'yi kullanıyoruz.
//
// Inspector'dan:
//   - Düşük çözünürlük (480x270 vb.)
//   - Renk derinliği (4-5 PSX)
//   - Dithering açık/kapalı ve şiddeti
// ayarlanabilir.
// =============================================================================

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

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

            [Tooltip("PSX post-process aktif mi? Kapalıyken passthrough davranır.")]
            public bool enabled = true;
        }

        public Settings settings = new Settings();

        private PSXRenderPass _pass;
        private Material _material;
        private Shader _shader;

        public override void Create()
        {
            _shader = Shader.Find("GoodNight/PSXSnapping");
            if (_shader == null)
            {
                Debug.LogWarning("[PSXRenderFeature] GoodNight/PSXSnapping shader bulunamadı. " +
                                 "Lütfen PSXSnapping.shader'ın projede olduğundan emin olun.");
                return;
            }
            _material = CoreUtils.CreateEngineMaterial(_shader);
            _pass = new PSXRenderPass(_material, settings)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || _material == null) return;
            if (!settings.enabled) return;
            // Sadece Game kamera için
            if (renderingData.cameraData.cameraType != CameraType.Game &&
                renderingData.cameraData.cameraType != CameraType.SceneView) return;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _pass?.Dispose();
        }

        // --------------------------------------------------------------------
        // PASS — Render Graph API
        // --------------------------------------------------------------------
        private class PSXRenderPass : ScriptableRenderPass
        {
            private Material _mat;
            private Settings _settings;
            private string _profilerTag = "PSXRenderPass";

            public PSXRenderPass(Material mat, Settings settings)
            {
                _mat = mat;
                _settings = settings;
                profilingSampler = new ProfilingSampler(_profilerTag);
            }

            // ----------------------------------------------------------------
            // Yeni Render Graph API'si
            // ----------------------------------------------------------------
            private class PassData
            {
                public Material material;
                public TextureHandle source;
                public TextureHandle lowRes;
                public Settings settings;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph,
                ContextContainer frameData)
            {
                if (_mat == null) return;

                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                if (resourceData.isActiveTargetBackBuffer) return;

                var source = resourceData.activeColorTexture;
                if (!source.IsValid()) return;

                // Low-res RT açıklaması
                var lowResDesc = new TextureDesc(
                    Mathf.Max(64, _settings.lowResWidth),
                    Mathf.Max(64, _settings.lowResHeight))
                {
                    colorFormat = GraphicsFormat.R8G8B8A8_UNorm,
                    depthBufferBits = DepthBits.None,
                    msaaSamples = MSAASamples.None,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "_PSXLowRes"
                };
                var lowRes = renderGraph.CreateTexture(lowResDesc);

                // Shader parametreleri
                _mat.SetFloat("_ColorBits", _settings.colorBits);
                _mat.SetFloat("_Dither", _settings.dithering ? _settings.ditherAmount : 0f);
                _mat.SetVector("_LowRes", new Vector4(_settings.lowResWidth, _settings.lowResHeight, 0, 0));

                // Pass 1: source -> lowRes
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                    "PSX_LowRes", out var passData, profilingSampler))
                {
                    passData.material = _mat;
                    passData.source = source;
                    passData.lowRes = lowRes;
                    passData.settings = _settings;

                    builder.UseTexture(source, AccessFlags.Read);
                    builder.SetRenderAttachment(lowRes, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                    });
                }

                // Pass 2: lowRes -> source (PSX shader ile)
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                    "PSX_Snap", out var passData, profilingSampler))
                {
                    passData.material = _mat;
                    passData.lowRes = lowRes;
                    passData.source = source;
                    passData.settings = _settings;

                    builder.UseTexture(lowRes, AccessFlags.Read);
                    builder.SetRenderAttachment(source, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        Blitter.BlitTexture(ctx.cmd, data.lowRes,
                            new Vector4(1, 1, 0, 0), data.material, 0);
                    });
                }
            }

            public void Dispose() { }
        }
    }
}
