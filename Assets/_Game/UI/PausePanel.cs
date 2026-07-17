// =============================================================================
// PausePanel.cs
// -----------------------------------------------------------------------------
// Oyun duraklatıldığında ekranda büyük bir "DURAKLATILDI" paneli gösterir.
// ESC ile duraklatma açılır/kapanır (P artık çalışmaz).
//
// Inspector'dan:
//   - Panel arka plan rengi
//   - Yazı rengi, font boyutu
//   - Animasyon (fade-in, scale)
//   - "Devam Etmek için ESC" ipucu
// ayarlanabilir.
// =============================================================================

using UnityEngine;
using GoodNightMyAngel.Core;
using GoodNightMyAngel.InputBridge;

namespace GoodNightMyAngel.UI
{
    /// <summary>
    /// Oyun duraklatma paneli. ESC ile açılır/kapanır.
    /// </summary>
    public class PausePanel : MonoBehaviour
    {
        [Header("Görünüm")]
        public bool showPanel = true;
        public Color backgroundColor = new Color(0, 0, 0, 0.7f);
        public Color titleColor = new Color(1f, 0.85f, 0.3f);
        public Color subtitleColor = new Color(0.95f, 0.92f, 0.85f);
        public Color hintColor = new Color(0.7f, 0.7f, 0.7f);
        public Color borderColor = new Color(0.8f, 0.6f, 0.3f, 0.9f);

        [Header("Boyut")]
        [Min(300f)] public float panelWidth = 540f;
        [Min(200f)] public float panelHeight = 280f;
        [Min(20)] public int titleFontSize = 56;
        [Min(14)] public int subtitleFontSize = 22;
        [Min(12)] public int hintFontSize = 16;

        [Header("Animasyon")]
        [Tooltip("Panel açılırken fade-in süresi (saniye).")]
        [Min(0f)] public float fadeInTime = 0.18f;

        [Tooltip("Panel açılırken pop (scale) animasyonu.")]
        public bool popAnimation = true;

        [Header("Mesajlar")]
        public string titleText = "DURAKLATILDI";
        public string subtitleText = "Rüya bir süreliğine durdu...";
        public string hintText = "ESC'ye tekrar bas — rüyaya devam et";

        // Durum
        private float _animTime = 0f;
        private bool _wasPaused = false;

        private void Update()
        {
            if (GameManager.Instance == null) return;

            bool paused = GameManager.Instance.Status == GameStatus.Paused;
            if (paused != _wasPaused)
            {
                _wasPaused = paused;
                _animTime = 0f;     // animasyonu sıfırla
            }
            if (paused && fadeInTime > 0f && _animTime < fadeInTime)
            {
                _animTime += Time.unscaledDeltaTime;
                if (_animTime > fadeInTime) _animTime = fadeInTime;
            }
        }

        private void OnGUI()
        {
            if (!showPanel) return;
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.Status != GameStatus.Paused) return;

            // Tüm ekranı yarı saydam siyah yap (oyun görünsün ama soluk)
            GUI.color = backgroundColor;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            // Panel ortada
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float w = panelWidth;
            float h = panelHeight;
            var panelRect = new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h);

            // Animasyon: scale-up (pop)
            float t = fadeInTime > 0f ? Mathf.Clamp01(_animTime / fadeInTime) : 1f;
            float ease = 1f - Mathf.Pow(1f - t, 3f);   // ease-out cubic
            if (popAnimation)
            {
                float scale = 0.85f + 0.15f * ease;
                panelRect = new Rect(
                    cx - w * 0.5f * scale,
                    cy - h * 0.5f * scale,
                    w * scale, h * scale);
            }

            // Panel arka planı
            GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);

            // Çerçeve
            GUI.color = borderColor;
            DrawBorder(panelRect, 3);

            // Başlık
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = Mathf.RoundToInt(titleFontSize * (popAnimation ? ease : 1f));
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.normal.textColor = titleColor;
            float titleY = panelRect.y + 30;
            GUI.Label(new Rect(panelRect.x, titleY, panelRect.width, titleFontSize + 20),
                titleText, titleStyle);

            // Alt başlık
            var subStyle = new GUIStyle(GUI.skin.label);
            subStyle.fontSize = subtitleFontSize;
            subStyle.fontStyle = FontStyle.Italic;
            subStyle.alignment = TextAnchor.MiddleCenter;
            subStyle.normal.textColor = subtitleColor;
            float subY = titleY + titleFontSize + 20;
            GUI.Label(new Rect(panelRect.x, subY, panelRect.width, subtitleFontSize + 10),
                subtitleText, subStyle);

            // Ayırıcı
            GUI.color = borderColor;
            GUI.DrawTexture(new Rect(panelRect.x + 60, subY + subtitleFontSize + 25,
                panelRect.width - 120, 1), Texture2D.whiteTexture);

            // İpucu
            var hintStyle = new GUIStyle(GUI.skin.label);
            hintStyle.fontSize = hintFontSize;
            hintStyle.alignment = TextAnchor.MiddleCenter;
            hintStyle.normal.textColor = hintColor;
            float hintY = subY + subtitleFontSize + 50;
            GUI.Label(new Rect(panelRect.x, hintY, panelRect.width, hintFontSize + 10),
                hintText, hintStyle);

            // ESC tuşu ipucu (basılı tutulan tuşu gösteren küçük bir kutu)
            float escSize = 36;
            var escRect = new Rect(cx - escSize * 0.5f, hintY + hintFontSize + 20,
                escSize, escSize);
            GUI.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);
            GUI.DrawTexture(escRect, Texture2D.whiteTexture);
            GUI.color = titleColor;
            DrawBorder(escRect, 2);
            var escStyle = new GUIStyle(GUI.skin.label);
            escStyle.fontSize = 18;
            escStyle.fontStyle = FontStyle.Bold;
            escStyle.alignment = TextAnchor.MiddleCenter;
            escStyle.normal.textColor = titleColor;
            GUI.Label(escRect, "ESC", escStyle);
        }

        private void DrawBorder(Rect r, int thickness)
        {
            for (int i = 0; i < thickness; i++)
            {
                GUI.DrawTexture(new Rect(r.x + i, r.y + i, r.width - i * 2, 1), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.x + i, r.y + r.height - 1 - i, r.width - i * 2, 1), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.x + i, r.y + i, 1, r.height - i * 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.x + r.width - 1 - i, r.y + i, 1, r.height - i * 2), Texture2D.whiteTexture);
            }
        }
    }
}
