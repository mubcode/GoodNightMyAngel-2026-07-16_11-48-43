// =============================================================================
// PSXRenderFeature.cs
// -----------------------------------------------------------------------------
// URP Render Feature (Render Graph API, Unity 6 / URP 17+): düşük çözünürlüklü
// bir ara texture'a render et, sonra PSX shader ile (snap, posterize,
// dithering) geri kopyala.
//
// ÖNEMLİ: Kaynak ve hedef aynı olamaz (Blitter aynı anda okuyup yazamaz).
// Bu yüzden "camera color" yerine "cameraColor + swap texture" kullanıyoruz.
//
// Inspector'dan:
//   - Düşük çözünürlük (480x270 vb.)
//   - Renk derinliği (4-5 PSX)
//   - Dithering açık/kapalı ve şiddeti
//   - Aktif/Pasif
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
            [Tooltip("PSX post-process aktif mi?")]
            public bool enabled = true;

            [Tooltip("Düşük çözünürlük X (genişlik).")]
            [Range(160, 1920)] public int lowResWidth = 480;

            [Tooltip("Düşük çözünürlük Y (yükseklik).")]
            [Range(120, 1080)] public int lowResHeight = 270;

            [Tooltip("Renk derinliği (her kanal için bit). 4-5 PSX, 6-7 retro, 8 modern.")]
            [Range(2, 8)] public int colorBits = 5;

            [Tooltip("Dithering uygulansın mı?")]
            public bool dithering = true;

            [Tooltip("Dithering şiddeti (0-0.2).")]
            [Range(0f, 0.2f)] public float ditherAmount = 0.06f;
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
                                 "PSXSnapping.shader dosyasının projede olduğundan emin olun.");
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
            // Sadece Game ve Scene view kameraları
            if (renderingData.cameraData.cameraType != CameraType.Game &&
                renderingData.cameraData.cameraType != CameraType.SceneView) return;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _pass = null;
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
            // Pass veri sınıfı
            // ----------------------------------------------------------------
            private class PassData
            {
                public Material material;
                public TextureHandle source;
                public TextureHandle dest;
                public Settings settings;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph,
                ContextContainer frameData)
            {
                if (_mat == null) return;

                var resourceData = frameData.Get<UniversalResourceData>();
                var source = resourceData.activeColorTexture;
                if (!source.IsValid()) return;

                // Hedef: Aynı boyutta yeni bir texture (kendi üstüne yazma yok)
                var destDesc = renderGraph.GetTextureDesc(source);
                destDesc.name = "_PSXSwap";
                destDesc.depthBufferBits = DepthBits.None;
                destDesc.clearBuffer = false;
                destDesc.filterMode = FilterMode.Point;
                var dest = renderGraph.CreateTexture(destDesc);

                // Shader parametrelerini güncelle
                _mat.SetFloat("_ColorBits", _settings.colorBits);
                _mat.SetFloat("_Dither", _settings.dithering ? _settings.ditherAmount : 0f);
                _mat.SetVector("_LowRes", new Vector4(_settings.lowResWidth, _settings.lowResHeight, 0, 0));

                // 1) source -> dest (PSX shader uygula)
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                    "PSX_Apply", out var passData, profilingSampler))
                {
                    passData.material = _mat;
                    passData.source = source;
                    passData.dest = dest;
                    passData.settings = _settings;

                    builder.UseTexture(source, AccessFlags.Read);
                    builder.SetRenderAttachment(dest, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        Blitter.BlitTexture(ctx.cmd, data.source,
                            new Vector4(1, 1, 0, 0), data.material, 0);
                    });
                }

                // 2) dest -> source (kopyala, shader yok)
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                    "PSX_CopyBack", out var passData, profilingSampler))
                {
                    passData.source = dest;
                    passData.dest = source;

                    builder.UseTexture(dest, AccessFlags.Read);
                    builder.SetRenderAttachment(source, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        Blitter.BlitTexture(ctx.cmd, data.source,
                            new Vector4(1, 1, 0, 0), 0, false);
                    });
                }
            }
        }
    }
}
