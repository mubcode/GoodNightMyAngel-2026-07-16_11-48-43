// =============================================================================
// DebugConsole.cs
// -----------------------------------------------------------------------------
// Geliştirici konsolu. F1 ile açılır, komutlar girilebilir. Komutlar:
//   god           -> oyuncu ölümsüz
//   day N         -> N. güne atla
//   night         -> hemen geceye geç
//   day           -> hemen güne dön
//   wave          -> build phase'i atla
//   addmoney N    -> N para ekle
//   killall       -> tüm düşmanları öldür
//   heal          -> tüm canları doldur
//   help          -> komut listesi
//
// Inspector'dan aktif/pasif ve tuş ataması yapılabilir.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using GoodNightMyAngel.Core;
using GoodNightMyAngel.InputBridge;

namespace GoodNightMyAngel.DebugTools
{
    /// <summary>
    /// Geliştirici komut konsolu. Backquote (`) tuşu ile açılır.
    /// </summary>
    public class DebugConsole : MonoBehaviour
    {
        [Header("Ayar")]
        public bool enabledConsole = true;
        public KeyCode toggleKey = KeyCode.BackQuote; // `

        private bool _open = false;
        private string _input = "";
        private readonly List<string> _history = new List<string>();
        private Vector2 _scroll;

        // God mode ve diğer state'ler
        public static bool GodMode { get; private set; } = false;

        private void Update()
        {
            if (!enabledConsole) return;
            if (LegacyInputBridge.GetKeyDown(toggleKey)) _open = !_open;
            if (!_open) return;
        }

        private void OnGUI()
        {
            if (!_open) return;

            var prevSize = GUI.skin.textField.fontSize;
            GUI.skin.textField.fontSize = 16;
            GUI.skin.label.fontSize = 16;
            GUI.skin.button.fontSize = 14;

            // Arka plan
            GUI.Box(new Rect(10, 10, 600, 280), "GoodNight Debug Console (` ile kapat)");

            // Geçmiş
            _scroll = GUI.BeginScrollView(new Rect(20, 40, 580, 180), _scroll,
                new Rect(0, 0, 560, _history.Count * 20 + 10));
            for (int i = 0; i < _history.Count; i++)
                GUI.Label(new Rect(10, i * 20, 560, 20), _history[i]);
            GUI.EndScrollView();

            // Giriş
            GUI.SetNextControlName("cmdField");
            _input = GUI.TextField(new Rect(20, 230, 500, 28), _input);
            if (GUI.Button(new Rect(525, 230, 60, 28), "Çalıştır"))
            {
                Execute(_input);
                _input = "";
            }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                if (GUI.GetNameOfFocusedControl() == "cmdField")
                {
                    Execute(_input);
                    _input = "";
                }
            }

            GUI.skin.textField.fontSize = prevSize;
            GUI.skin.label.fontSize = prevSize;
            GUI.skin.button.fontSize = prevSize;
        }

        private void Execute(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            string cmd = raw.Trim();
            _history.Add($"> {cmd}");
            if (_history.Count > 50) _history.RemoveAt(0);

            var parts = cmd.Split(' ');
            string op = parts[0].ToLowerInvariant();

            switch (op)
            {
                case "help":
                case "?":
                    _history.Add("Komutlar: god, day N, night, day, wave, addmoney N, killall, heal");
                    break;
                case "god":
                    GodMode = !GodMode;
                    _history.Add($"God mode: {(GodMode ? "AÇIK" : "KAPALI")}");
                    if (DebugOverlay.Instance != null)
                        DebugOverlay.Instance.Log(LogCategory.System,
                            $"God mode: {(GodMode ? "AÇIK" : "KAPALI")}", false);
                    break;
                case "day":
                    if (parts.Length > 1 && int.TryParse(parts[1], out int d))
                    {
                        if (GameManager.Instance != null) GameManager.Instance.DebugJumpToDay(d);
                        _history.Add($"{d}. güne atlandı.");
                    }
                    else if (GameManager.Instance != null)
                    {
                        GameManager.Instance.DebugToggleDayNight();
                        _history.Add("Gündüze dönüldü.");
                    }
                    break;
                case "night":
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.DebugToggleDayNight();
                        _history.Add("Geceye geçildi.");
                    }
                    break;
                case "wave":
                    if (GameManager.Instance != null) GameManager.Instance.DebugSkipToNextWave();
                    _history.Add("Build phase atlandı.");
                    break;
                case "addmoney":
                    if (parts.Length > 1 && int.TryParse(parts[1], out int m))
                    {
                        var bm = FindFirstObjectByType<Build.BuildManager>();
                        if (bm != null)
                        {
                            bm.AddCurrency(m);
                            _history.Add($"+{m} para eklendi. Toplam: {bm.Currency}");
                        }
                    }
                    break;
                case "killall":
                    var enemies = FindObjectsByType<Enemies.EnemyBase>(FindObjectsSortMode.None);
                    int n = 0;
                    foreach (var e in enemies) { if (e != null && !e.IsDead) { e.TakeDamage(99999f); n++; } }
                    _history.Add($"{n} düşman öldürüldü.");
                    break;
                case "heal":
                    var phs = FindObjectsByType<Player.PlayerHealth>(FindObjectsSortMode.None);
                    foreach (var p in phs) p.Heal(9999f);
                    var beds = FindObjectsByType<World.Bed>(FindObjectsSortMode.None);
                    foreach (var b in beds) b.Heal(9999f);
                    _history.Add("Tüm canlar dolduruldu.");
                    break;
                default:
                    _history.Add("Bilinmeyen komut. 'help' yaz.");
                    break;
            }
        }
    }
}
