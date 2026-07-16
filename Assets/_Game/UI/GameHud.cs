// =============================================================================
// GameHud.cs
// -----------------------------------------------------------------------------
// Ekrandaki ana oyun HUD'u. OnGUI kullanır (UGUI kurmadan çalışır).
// Inspector'dan:
//   - Sağlık çubuğu can/renkleri
//   - Yatak can çubuğu
//   - Para
//   - Dalga bilgisi
//   - Call Mom göstergesi
//   - Açılan ipucu metinleri
//   - Font boyutu
// ayarlanabilir.
//
// Build phase sırasında ekranın üst orta kısmında geri sayım gösterilir.
// =============================================================================

using UnityEngine;
using GoodNightMyAngel.Core;

namespace GoodNightMyAngel.UI
{
    /// <summary>
    /// Oyun içi ana HUD. Geçici olarak OnGUI tabanlı, ileride UGUI'ye geçirilebilir.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        [Header("Genel")]
        public bool showHud = true;
        public int baseFontSize = 18;

        [Header("Sağlık Çubuğu (Oyuncu)")]
        public Color playerHealthColor = new Color(0.4f, 0.85f, 0.4f);
        public Color playerHealthBack = new Color(0.15f, 0.15f, 0.15f, 0.7f);
        public Vector2 playerBarPos = new Vector2(20, 20);
        public Vector2 playerBarSize = new Vector2(280, 22);

        [Header("Yatak Can Çubuğu")]
        public Color bedHealthColor = new Color(0.9f, 0.4f, 0.4f);
        public Color bedHealthBack = new Color(0.15f, 0.15f, 0.15f, 0.7f);
        public Vector2 bedBarPos = new Vector2(20, 50);
        public Vector2 bedBarSize = new Vector2(280, 22);

        [Header("Üst-Orta Bilgi")]
        public Color topInfoColor = Color.white;
        public Color topInfoBack = new Color(0, 0, 0, 0.5f);
        public int topInfoFontSize = 22;

        [Header("Sağ-Alt: Para")]
        public Color currencyColor = new Color(1f, 0.85f, 0.3f);

        [Header("Sol-Alt: Call Mom")]
        public Color skillColor = new Color(1f, 0.7f, 0.9f);
        public Color skillDisabled = new Color(0.5f, 0.5f, 0.5f, 0.7f);
        public Vector2 skillPos = new Vector2(20, Screen.height - 60);
        public Vector2 skillSize = new Vector2(160, 44);

        // Basit ipucu
        private string _tip = "";
        private float _tipTimer = 0f;

        public void ShowTip(string text, float seconds = 3f)
        {
            _tip = text;
            _tipTimer = seconds;
        }

        private void Update()
        {
            if (_tipTimer > 0f) _tipTimer -= Time.deltaTime;
        }

        private void OnGUI()
        {
            if (!showHud) return;
            if (GameManager.Instance == null) return;

            var prevSize = GUI.skin.label.fontSize;
            GUI.skin.label.fontSize = baseFontSize;

            // --- Oyuncu can çubuğu ---
            var ph = FindObjectOfType<Player.PlayerHealth>();
            if (ph != null && !ph.IsDead)
            {
                DrawBar(playerBarPos, playerBarSize, ph.CurrentHealth / ph.maxHealth,
                    $"Can: {ph.CurrentHealth:F0}/{ph.maxHealth:F0}",
                    playerHealthColor, playerHealthBack);
            }

            // --- Yatak can çubuğu ---
            var bed = GameManager.Instance.bed;
            if (bed != null && !bed.IsDead)
            {
                DrawBar(bedBarPos, bedBarSize, bed.HealthPercent,
                    $"Yatak: {bed.CurrentHealth:F0}/{bed.maxHealth:F0}",
                    bedHealthColor, bedHealthBack);
            }

            // --- Üst-orta: Gün & Faz ---
            string topText = "";
            if (GameManager.Instance.Status == GameStatus.GameOver)
                topText = "OYUN BİTTİ — R'ye bas veya konsoldan 'day 1' yaz";
            else if (GameManager.Instance.Status == GameStatus.NightCleared)
                topText = "Şafak söküyor...";
            else
            {
                string tod = GameManager.Instance.TimeOfDay switch
                {
                    TimeOfDay.Day => "GÜNDÜZ",
                    TimeOfDay.NightBuild => "GECE — BUILD PHASE",
                    TimeOfDay.NightDefense => "GECE — SAVUNMA",
                    TimeOfDay.NightBoss => "GECE — BOSS",
                    TimeOfDay.Dawn => "ŞAFAK",
                    _ => ""
                };
                topText = $"Gün {GameManager.Instance.CurrentDay}  •  {tod}";
                if (GameManager.Instance.TimeOfDay == TimeOfDay.NightBuild)
                {
                    topText += $"  •  {Mathf.CeilToInt(GameManager.Instance.BuildPhaseTimeRemaining)}s";
                }
                if (GameManager.Instance.TimeOfDay == TimeOfDay.NightDefense ||
                    GameManager.Instance.TimeOfDay == TimeOfDay.NightBoss)
                {
                    int total = GameManager.Instance.wavesPerNight +
                                (GameManager.Instance.CurrentDay - 1) * GameManager.Instance.wavesIncreasePerNight;
                    topText += $"  •  Dalga {GameManager.Instance.CurrentWave}/{total}";
                }
            }
            DrawTopInfo(topText);

            // --- Sağ-alt: Para ---
            var bm = FindObjectOfType<Build.BuildManager>();
            if (bm != null)
            {
                var rect = new Rect(Screen.width - 180, Screen.height - 50, 160, 30);
                GUI.color = new Color(0, 0, 0, 0.5f);
                GUI.Box(rect, "");
                GUI.color = currencyColor;
                GUI.Label(new Rect(rect.x + 8, rect.y + 4, rect.width, rect.height),
                    $"Para: {bm.Currency}");
            }

            // --- Sol-alt: Call Mom göstergesi ---
            var callMom = FindObjectOfType<Skills.CallMomSkill>();
            if (callMom != null)
            {
                bool canUse = callMom.CanUse();
                GUI.color = canUse ? skillColor : skillDisabled;
                var rect = new Rect(skillPos.x, skillPos.y, skillSize.x, skillSize.y);
                GUI.Box(rect, "");
                GUI.color = Color.black;
                GUI.Label(new Rect(rect.x + 8, rect.y + 4, rect.width, rect.height),
                    $"Call Mom (Q)\n{((callMom.RemainingUses > 0) ? "×" + callMom.RemainingUses : "yok")}");
            }

            // --- İpucu ---
            if (_tipTimer > 0f)
            {
                var tipRect = new Rect(Screen.width / 2 - 200, 80, 400, 30);
                GUI.color = new Color(0, 0, 0, 0.5f);
                GUI.Box(tipRect, "");
                GUI.color = Color.white;
                GUI.skin.label.fontSize = 16;
                GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                GUI.Label(tipRect, _tip);
                GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            }

            GUI.skin.label.fontSize = prevSize;
        }

        private void DrawBar(Vector2 pos, Vector2 size, float t, string label,
            Color fg, Color bg)
        {
            t = Mathf.Clamp01(t);
            GUI.color = bg;
            GUI.Box(new Rect(pos.x, pos.y, size.x, size.y), "");
            GUI.color = fg;
            GUI.Box(new Rect(pos.x, pos.y, size.x * t, size.y), "");
            GUI.color = Color.black;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(pos.x, pos.y, size.x, size.y), label);
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
        }

        private void DrawTopInfo(string text)
        {
            int prev = GUI.skin.label.fontSize;
            GUI.skin.label.fontSize = topInfoFontSize;
            var rect = new Rect(Screen.width / 2 - 250, 10, 500, 36);
            GUI.color = topInfoBack;
            GUI.Box(rect, "");
            GUI.color = topInfoColor;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.Label(rect, text);
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUI.skin.label.fontSize = prev;
        }
    }
}
