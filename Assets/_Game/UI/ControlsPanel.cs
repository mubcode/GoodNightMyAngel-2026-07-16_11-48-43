// =============================================================================
// ControlsPanel.cs
// -----------------------------------------------------------------------------
// Ekranda sol orta kısımda bir UI panel. Oyunun tüm kontrollerini ve debug
// konsolu komutlarını gösterir. Kodla aynı yerde tanımlı olduğu için
// Inspector'dan veya koddan otomatik güncellenir — yeni eklenen/kaldırılan
// özellikler burada listelenir.
//
// Inspector'dan:
//   - Panel pozisyonu (sol üst/orta/sağ)
//   - Arka plan rengi
//   - Metin rengi
//   - Font boyutu
//   - Panel genişliği
//   - Açılır/kapanır (F1 veya Inspector switch)
//   - Kontrol listesi (otomatik veya manuel)
// ayarlanabilir.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using GoodNightMyAngel.Core;

namespace GoodNightMyAngel.UI
{
    /// <summary>
    /// Oyun kontrolleri ve debug komutları için UI panel.
    /// </summary>
    public class ControlsPanel : MonoBehaviour
    {
        [Header("Görünüm")]
        [Tooltip("Paneli göster.")]
        public bool showPanel = true;

        [Tooltip("F1 ile aç/kapa (DebugOverlay'dan bağımsız).")]
        public bool toggleWithF1 = true;

        [Tooltip("Panel X pozisyonu (piksel, sol = 0).")]
        public float panelX = 20f;

        [Tooltip("Panel Y pozisyonu (piksel, üst = 0).")]
        public float panelY = 80f;

        [Tooltip("Panel genişliği (piksel).")]
        [Min(200f)] public float panelWidth = 320f;

        [Tooltip("Başlık.")]
        public string title = "KONTROLLER & DEBUG";

        [Tooltip("Başlık font boyutu.")]
        [Min(10)] public int titleFontSize = 16;

        [Tooltip("İçerik font boyutu.")]
        [Min(9)] public int bodyFontSize = 12;

        [Tooltip("Satır aralığı (piksel).")]
        [Min(10f)] public float lineHeight = 16f;

        [Header("Renkler")]
        public Color backgroundColor = new Color(0, 0, 0, 0.7f);
        public Color borderColor = new Color(0.7f, 0.6f, 0.4f, 0.8f);
        public Color titleColor = new Color(1f, 0.85f, 0.3f);
        public Color sectionColor = new Color(0.5f, 0.9f, 0.9f);
        public Color textColor = new Color(0.95f, 0.92f, 0.85f);
        public Color dimTextColor = new Color(0.7f, 0.7f, 0.65f);

        [Header("Özel")]
        [Tooltip("Mouse aim ray'i göster (karakter -> crosshair).")]
        public bool enableRayToggle = true;

        // Kontrol listesi
        private struct ControlEntry
        {
            public string key;
            public string desc;
            public Color color;
        }

        private List<ControlEntry> _controls = new List<ControlEntry>();
        private List<ControlEntry> _debugCmds = new List<ControlEntry>();
        private Vector2 _scroll;
        private bool _rayVisible = true;
        private Player.PlayerController _pc;

        private void Start()
        {
            BuildControlList();
            _pc = FindFirstObjectByType<Player.PlayerController>();
        }

        private void Update()
        {
            if (toggleWithF1 && GoodNightMyAngel.InputBridge.LegacyInputBridge.GetKeyDown(KeyCode.F1))
                showPanel = !showPanel;

            if (enableRayToggle && GoodNightMyAngel.InputBridge.LegacyInputBridge.GetKeyDown(KeyCode.F4))
            {
                _rayVisible = !_rayVisible;
                if (_pc != null)
                {
                    // Ray'i kontrol etmek için basit yol: rengi alpha 0 yap
                    var lr = _pc.GetComponentInChildren<LineRenderer>(true);
                    if (lr != null)
                    {
                        var c = lr.startColor;
                        c.a = _rayVisible ? 0.6f : 0f;
                        lr.startColor = c;
                        lr.endColor = c;
                        lr.enabled = _rayVisible;
                    }
                }
            }
        }

        // -------------------------------------------------------------------------
        // KONTROL LİSTESİ
        // -------------------------------------------------------------------------
        private void BuildControlList()
        {
            _controls.Clear();
            _debugCmds.Clear();

            // === TEMEL HAREKET ===
            _controls.Add(Make("WASD / Sol Analog", "Hareket"));
            _controls.Add(Make("Sol Shift", "Sprint (koşma)"));
            _controls.Add(Make("SPACE", "Zıplama"));
            _controls.Add(Make("Mouse", "Nişan al (karakter döner)"));

            // === KAMERA ===
            _controls.Add(Make("Mouse Wheel", "Kamera zoom"));

            // === BUILD PHASE ===
            _controls.Add(Make("1 / 2 / 3", "Eşya seç (Barikat / Tuzak / Kule)"));
            _controls.Add(Make("Sol Tık", "Eşya yerleştir (gece build)"));
            _controls.Add(Make("Sağ Tık", "Eşya seç (tamir/kaldır)"));
            _controls.Add(Make("R", "Seçili eşyayı tamir et"));

            // === BECERİ ===
            _controls.Add(Make("Q", "Call Mom (ultimate, 1x/gece)"));

            // === DEBUG ===
            _controls.Add(Make("F1", "Bu paneli aç/kapa"));
            _controls.Add(Make("F2", "Debug log sistemi aç/kapa"));
            _controls.Add(Make("F3", "Gün/Gece hızlı geçiş (test)"));
            _controls.Add(Make("F4", "Mouse aim ray aç/kapa"));
            _controls.Add(Make("ESC", "Oyunu duraklat / devam et"));
            _controls.Add(Make("` (backtick)", "Debug konsolu aç"));

            // === DEBUG KOMUTLARI ===
            _debugCmds.Add(Make("help", "Komut listesi"));
            _debugCmds.Add(Make("god", "Ölümsüz mod aç/kapa"));
            _debugCmds.Add(Make("day N", "N. güne atla (ör: day 5)"));
            _debugCmds.Add(Make("night", "Hemen geceye geç"));
            _debugCmds.Add(Make("wave", "Build phase'i atla"));
            _debugCmds.Add(Make("killall", "Tüm düşmanları öldür"));
            _debugCmds.Add(Make("heal", "Tüm canları doldur"));
        }

        private ControlEntry Make(string k, string d)
        {
            return new ControlEntry { key = k, desc = d, color = textColor };
        }

        // -------------------------------------------------------------------------
        // OnGUI
        // -------------------------------------------------------------------------
        private void OnGUI()
        {
            if (!showPanel) return;

            // İçerik satır sayısı (kontroller + debug + başlık + boşluklar)
            int totalLines = 2 + _controls.Count + 3 + _debugCmds.Count + 2;
            float padY = 12f;
            float padX = 12f;
            float titleH = titleFontSize + 6;
            float h = titleH + 4 + totalLines * lineHeight + padY * 2;

            var rect = new Rect(panelX, panelY, panelWidth, h);

            // Arka plan
            GUI.color = backgroundColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            // Çerçeve
            GUI.color = borderColor;
            DrawBorder(rect);

            float x = rect.x + padX;
            float y = rect.y + padY;
            float w = rect.width - padX * 2;

            // Başlık
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = titleFontSize;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleLeft;
            var titleRect = new Rect(x, y, w, titleH);
            GUI.color = titleColor;
            GUI.Label(titleRect, "⌨ " + title, titleStyle);
            y += titleH + 2;

            // Ayırıcı
            GUI.color = borderColor;
            GUI.DrawTexture(new Rect(x, y, w, 1), Texture2D.whiteTexture);
            y += 4;

            // Kontroller
            DrawSection("🎮 KONTROLLER", ref y, x, w);
            foreach (var e in _controls) DrawLine(e, ref y, x, w);

            // Boşluk
            y += 4;
            GUI.color = borderColor;
            GUI.DrawTexture(new Rect(x, y, w, 1), Texture2D.whiteTexture);
            y += 4;

            // Debug komutları
            DrawSection("⌨ DEBUG KOMUTLARI (` ile aç)", ref y, x, w);
            foreach (var e in _debugCmds) DrawLine(e, ref y, x, w);

            // Footer
            y += 4;
            var footerStyle = new GUIStyle(GUI.skin.label);
            footerStyle.fontSize = bodyFontSize - 1;
            footerStyle.fontStyle = FontStyle.Italic;
            footerStyle.alignment = TextAnchor.MiddleLeft;
            GUI.color = dimTextColor;
            GUI.Label(new Rect(x, y, w, lineHeight),
                "F1: panel aç/kapa • F3: gün/gece • Q: Call Mom", footerStyle);
        }

        private void DrawSection(string text, ref float y, float x, float w)
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontSize = bodyFontSize + 1;
            style.fontStyle = FontStyle.Bold;
            GUI.color = sectionColor;
            GUI.Label(new Rect(x, y, w, lineHeight), text, style);
            y += lineHeight;
        }

        private void DrawLine(ControlEntry e, ref float y, float x, float w)
        {
            var keyStyle = new GUIStyle(GUI.skin.label);
            keyStyle.fontSize = bodyFontSize;
            keyStyle.fontStyle = FontStyle.Bold;
            keyStyle.alignment = TextAnchor.MiddleLeft;
            keyStyle.normal.textColor = new Color(1f, 0.95f, 0.7f);

            var descStyle = new GUIStyle(GUI.skin.label);
            descStyle.fontSize = bodyFontSize;
            descStyle.alignment = TextAnchor.MiddleLeft;
            descStyle.normal.textColor = textColor;

            float keyW = w * 0.45f;
            // Tuş
            GUI.color = new Color(1f, 0.95f, 0.7f);
            GUI.Label(new Rect(x, y, keyW, lineHeight), e.key, keyStyle);
            // Açıklama
            GUI.color = e.color;
            GUI.Label(new Rect(x + keyW + 6, y, w - keyW - 6, lineHeight), e.desc, descStyle);
            y += lineHeight;
        }

        private void DrawBorder(Rect r)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y + r.height - 1, r.width, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, 1, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x + r.width - 1, r.y, 1, r.height), Texture2D.whiteTexture);
        }
    }
}
