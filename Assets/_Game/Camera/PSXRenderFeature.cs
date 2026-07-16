// =============================================================================
// PSXRenderFeature.cs
// -----------------------------------------------------------------------------
// URP Render Feature (Unity 6 / URP 17+): PSX tarzı renk azaltma + dithering.
//
// NOT: Bu feature iki ayrı Blit pass kullanır. Birinde sahnenin kendisi
// değil, bir "swap" texture'ı kullanılır. İkincisinde swap camera color'a
// geri kopyalanır. Bu sayede source/destination aynı olmaz ve read/write
// çakışması yaşanmaz.
//
// Vertex snap (düşük çözünürlüklü polygon kenarları) Blitter tarafından
// zaten doğru yapılır; shader sadece fragment tarafında renk değiştirir.
//
// Inspector'dan:
//   - Düşük çözünürlük (genişlik, yükseklik)
//   - Renk derinliği (4-5 PSX, 6-7 retro, 8 modern)
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
            [Tooltip("PSX post-process aktif mi? Kapalıyken oyun normal görünür.")]
            public bool enabled = true;

            [Tooltip("Posterize için her kanalda kaç bit kullanılsın. 4-5 PSX, 8 modern.")]
            [Range(2, 8)] public int colorBits = 5;

            [Tooltip("Bayer dithering uygulansın mı?")]
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
                                 "PSXSnapping.shader'ın projede olduğundan emin olun.");
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
        // PASS
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

            private class PassData
            {
                public Material material;
                public TextureHandle source;
                public TextureHandle dest;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph,
                ContextContainer frameData)
            {
                if (_mat == null) return;

                var resourceData = frameData.Get<UniversalResourceData>();
                var source = resourceData.activeColorTexture;
                if (!source.IsValid()) return;

                // Hedef: camera color'la aynı boyutta ama farklı bir texture
                var desc = renderGraph.GetTextureDesc(source);
                desc.name = "_PSXSwap";
                desc.depthBufferBits = DepthBits.None;
                desc.clearBuffer = false;
                desc.filterMode = FilterMode.Point;
                var dest = renderGraph.CreateTexture(desc);

                // Shader parametrelerini güncelle
                _mat.SetFloat("_ColorBits", _settings.colorBits);
                _mat.SetFloat("_Dither", _settings.dithering ? _settings.ditherAmount : 0f);

                // 1) source (camera) -> dest (swap) — PSX shader uygula
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                    "PSX_Apply", out var passData, profilingSampler))
                {
                    passData.material = _mat;
                    passData.source = source;
                    passData.dest = dest;

                    builder.UseTexture(source, AccessFlags.Read);
                    builder.SetRenderAttachment(dest, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        // CoreUtils.DrawFullScreen ile sahnenin düzgün çizilmesini sağla
                        // yerine Blitter kullanıyoruz (Blitter'ın vertex'leri tüm ekranı kaplar)
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
