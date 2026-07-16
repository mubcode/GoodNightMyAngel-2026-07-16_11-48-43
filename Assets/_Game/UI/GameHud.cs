// =============================================================================
// GameHud.cs
// -----------------------------------------------------------------------------
// Ekrandaki ana oyun HUD'u. OnGUI kullanır (UGUI kurmadan çalışır).
// Inspector'dan tüm renkler, boyutlar, pozisyonlar ayarlanabilir.
//
// Yenilikler (eski versiyona göre):
//   - Yatak canı ekranın alt orta kısmında daha büyük
//   - Build phase geri sayımı belirgin renkte
//   - Çağrı (Call Mom) durumu sol altta kutu içinde
//   - Para sağ üstte
//   - PSX tarzı monospace font hissi için arka plan + çerçeve
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
        public int baseFontSize = 16;

        [Header("PSX Renk Paleti")]
        public Color hudBg = new Color(0, 0, 0, 0.55f);
        public Color hudBorder = new Color(0.7f, 0.6f, 0.4f, 0.6f);
        public Color textColor = new Color(0.95f, 0.92f, 0.85f);
        public Color accentColor = new Color(1f, 0.7f, 0.3f);

        [Header("Sağlık Çubuğu (Oyuncu)")]
        public Color playerHealthColor = new Color(0.4f, 0.85f, 0.4f);
        public Color playerHealthBack = new Color(0.15f, 0.15f, 0.15f, 0.7f);
        public Vector2 playerBarPos = new Vector2(20, 20);
        public Vector2 playerBarSize = new Vector2(240, 20);

        [Header("Yatak Can Çubuğu (Ekran Alt-Orta)")]
        public Color bedHealthColor = new Color(0.9f, 0.4f, 0.4f);
        public Color bedHealthBack = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        public Vector2 bedBarSize = new Vector2(360, 24);
        public float bedBarYOffset = 80f;

        [Header("Üst-Orta Bilgi")]
        public Color topInfoColor = new Color(0.95f, 0.92f, 0.85f);
        public Color topInfoBack = new Color(0, 0, 0, 0.5f);
        public int topInfoFontSize = 20;

        [Header("Sağ-Üst: Para")]
        public Color currencyColor = new Color(1f, 0.85f, 0.3f);
        public Vector2 currencyPos = new Vector2(0, 0);  // (0,0) = sağ üst köşe

        [Header("Sol-Alt: Call Mom")]
        public Color skillColor = new Color(1f, 0.7f, 0.9f);
        public Color skillDisabled = new Color(0.5f, 0.5f, 0.5f, 0.7f);
        public Vector2 skillPos = new Vector2(20, 100);
        public Vector2 skillSize = new Vector2(150, 40);

        [Header("İpucu (Ekranda Geçici Metin)")]
        public int tipFontSize = 16;

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

            // --- Oyuncu can çubuğu (sol üst) ---
            var ph = FindFirstObjectByType<Player.PlayerHealth>();
            if (ph != null && !ph.IsDead)
            {
                DrawBar(playerBarPos, playerBarSize, ph.CurrentHealth / ph.maxHealth,
                    $"Can: {ph.CurrentHealth:F0}/{ph.maxHealth:F0}",
                    playerHealthColor, playerHealthBack);
            }

            // --- Yatak can çubuğu (alt orta) ---
            var bed = GameManager.Instance.bed;
            if (bed != null && !bed.IsDead)
            {
                Vector2 bPos = new Vector2(
                    (Screen.width - bedBarSize.x) * 0.5f,
                    Screen.height - bedBarSize.y - bedBarYOffset);
                DrawBar(bPos, bedBarSize, bed.HealthPercent,
                    $"YATAK (Çocuğun Bedeni): {bed.CurrentHealth:F0}/{bed.maxHealth:F0}",
                    bedHealthColor, bedHealthBack);
            }

            // --- Üst-orta: Gün & Faz ---
            string topText = "";
            if (GameManager.Instance.Status == GameStatus.GameOver)
                topText = "OYUN BİTTİ — ESC ile başa dön";
            else if (GameManager.Instance.Status == GameStatus.Paused)
                topText = "⏸ DURAKLATILDI — ESC ile devam";
            else if (GameManager.Instance.Status == GameStatus.NightCleared)
                topText = "ŞAFAK SÖKÜYOR...";
            else
            {
                string tod = GameManager.Instance.TimeOfDay switch
                {
                    TimeOfDay.Day => "☀ GÜNDÜZ",
                    TimeOfDay.NightBuild => "🛡 GECE — BUILD PHASE",
                    TimeOfDay.NightDefense => "⚔ GECE — SAVUNMA",
                    TimeOfDay.NightBoss => "💀 GECE — BOSS",
                    TimeOfDay.Dawn => "🌅 ŞAFAK",
                    _ => ""
                };
                topText = $"Gün {GameManager.Instance.CurrentDay}   •   {tod}";
                if (GameManager.Instance.TimeOfDay == TimeOfDay.NightBuild)
                {
                    int t = Mathf.CeilToInt(GameManager.Instance.BuildPhaseTimeRemaining);
                    topText += $"   •   ⏱ {t}s";
                }
                if (GameManager.Instance.TimeOfDay == TimeOfDay.NightDefense ||
                    GameManager.Instance.TimeOfDay == TimeOfDay.NightBoss)
                {
                    int total = GameManager.Instance.wavesPerNight +
                                (GameManager.Instance.CurrentDay - 1) * GameManager.Instance.wavesIncreasePerNight;
                    topText += $"   •   Dalga {GameManager.Instance.CurrentWave}/{total}";
                }
            }
            DrawTopInfo(topText);

            // --- Sağ-üst: Para ---
            var bm = FindFirstObjectByType<Build.BuildManager>();
            if (bm != null)
            {
                var rect = new Rect(Screen.width - 160 + currencyPos.x, 20 + currencyPos.y, 140, 30);
                DrawBox(rect, $"💰 {bm.Currency} altın", currencyColor, hudBg, hudBorder);
            }

            // --- Sağ-üst (alt satır): Şarjör ---
            var weapon = FindFirstObjectByType<Player.Weapon>();
            if (weapon != null)
            {
                var rect = new Rect(Screen.width - 160 + currencyPos.x, 56 + currencyPos.y, 140, 30);
                string ammoText = weapon.IsReloading
                    ? $"🔄 Reload {weapon.ReloadProgress * 100:F0}%"
                    : $"🔫 {weapon.CurrentAmmo} / {weapon.MaxAmmo}";
                Color c = weapon.IsReloading ? new Color(1f, 0.6f, 0.3f) :
                          weapon.CurrentAmmo == 0 ? new Color(1f, 0.3f, 0.3f) :
                          new Color(0.7f, 0.95f, 0.4f);
                DrawBox(rect, ammoText, c, hudBg, hudBorder);
            }

            // --- Sol-alt: Call Mom göstergesi ---
            var callMom = FindFirstObjectByType<Skills.CallMomSkill>();
            if (callMom != null)
            {
                bool canUse = callMom.CanUse();
                Color c = canUse ? skillColor : skillDisabled;
                var rect = new Rect(skillPos.x, Screen.height - skillPos.y - skillSize.y,
                    skillSize.x, skillSize.y);
                string text = canUse
                    ? $"✨ Call Mom (Q)\n{((callMom.RemainingUses > 0) ? "×" + callMom.RemainingUses + " hak" : "yok")}"
                    : "Call Mom (Q)\nyok";
                DrawBox(rect, text, c, hudBg, hudBorder);
            }

            // --- İpucu (ekran ortası üst) ---
            if (_tipTimer > 0f)
            {
                var tipRect = new Rect(Screen.width / 2 - 220, 60, 440, 30);
                GUI.color = new Color(0, 0, 0, 0.6f);
                GUI.Box(tipRect, "");
                GUI.color = textColor;
                GUI.skin.label.fontSize = tipFontSize;
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
            // Arka plan
            GUI.color = bg;
            GUI.DrawTexture(new Rect(pos.x, pos.y, size.x, size.y), Texture2D.whiteTexture);
            // Ön plan
            GUI.color = fg;
            GUI.DrawTexture(new Rect(pos.x, pos.y, size.x * t, size.y), Texture2D.whiteTexture);
            // Çerçeve (basit)
            GUI.color = hudBorder;
            GUI.DrawTexture(new Rect(pos.x, pos.y, size.x, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(pos.x, pos.y + size.y - 1, size.x, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(pos.x, pos.y, 1, size.y), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(pos.x + size.x - 1, pos.y, 1, size.y), Texture2D.whiteTexture);
            // Etiket
            GUI.color = Color.black;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(pos.x, pos.y, size.x, size.y), label);
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
        }

        private void DrawBox(Rect rect, string text, Color fg, Color bg, Color border)
        {
            GUI.color = bg;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = border;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - 1, rect.width, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + rect.width - 1, rect.y, 1, rect.height), Texture2D.whiteTexture);
            GUI.color = fg;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.Label(rect, text);
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
        }

        private void DrawTopInfo(string text)
        {
            int prev = GUI.skin.label.fontSize;
            GUI.skin.label.fontSize = topInfoFontSize;
            var rect = new Rect(Screen.width / 2 - 280, 10, 560, 36);
            GUI.color = topInfoBack;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = topInfoColor;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.Label(rect, text);
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUI.skin.label.fontSize = prev;
        }
    }
}
