// =============================================================================
// DebugOverlay.cs
// -----------------------------------------------------------------------------
// GoodNight My Angel — Merkezi debug ve loglama sistemi.
//
// Bu script oyunun "gözü ve kulağı"dır. Görevleri:
//   1) Tüm sistemlerden gelen log mesajlarını Inspector'dan ayarlanabilir bir
//      kategorize yapı ile konsola yazdırmak.
//   2) Oyun içi (OnGUI) küçük bir HUD ile anlık durum göstermek: gündüz/gece,
//      gün sayısı, oyuncu canı, yatak canı, dalga bilgisi vb.
//   3) Geliştirici yardımcı komutları (god mode, günü atla, dalgayı tetikle)
//      tuş kısayolları ile çalıştırmak.
//
// Tüm ayarlar Inspector'dan değiştirilebilir; log kategorilerini açıp
// kapatabilir, renklerini ayarlayabilir, hangi mesajların yazılacağını
// seçebilirsin.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using GoodNightMyAngel.InputBridge;

namespace GoodNightMyAngel.Core
{
    /// <summary>
    /// Log kategorileri. Inspector'da her birinin açık/kapalı durumu ve
    /// konsol rengi ayarlanabilir. Yeni kategori eklemek için bu enum'a
    /// yeni bir değer eklemeniz ve Inspector'daki diziye karşılık gelen
    /// girdiyi doldurmanız yeterli.
    /// </summary>
    public enum LogCategory
    {
        System,        // GameManager, sahne yükleme vb.
        Player,        // Hareket, hasar, ölüm
        Enemy,         // Spawn, AI, ölüm
        Build,         // Build phase, barikat, tuzak
        Combat,        // Hasar hesapları, projectile
        Wave,          // Dalga yönetimi
        DayNight,      // Gündüz/gece geçişi
        Skill,         // Call Mom ve diğer yetenekler
        UI,            // Arayüz mesajları
        Input,         // Girdi olayları
        Camera,        // Kamera hareketi
        Story,         // Hikâye tetikleyicileri, anne/baba olayları
    }

    /// <summary>
    /// Inspector'da gösterilen tek bir log kategorisi ayarı.
    /// </summary>
    [Serializable]
    public class LogCategorySettings
    {
        [Tooltip("Log kategorisi")]
        public LogCategory category = LogCategory.System;

        [Tooltip("Bu kategorideki mesajlar konsola yazılsın mı?")]
        public bool enabled = true;

        [Tooltip("Bu kategorideki mesajlar için konsol rengi (Unity rich text).")]
        public string colorHex = "#ffffff";
    }

    /// <summary>
    /// Merkezi debug overlay. Sahneye tek bir instance olarak eklenir
    /// (DontDestroyOnLoad). Diğer tüm scriptler bu sınıfın statik
    /// Log(...) metodunu çağırarak mesaj bırakır.
    /// </summary>
    public class DebugOverlay : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // SINGLETON
        // -------------------------------------------------------------------------
        // Basit bir singleton deseni. Tüm sistemler DebugOverlay.Instance.Log(...)
        // şeklinde mesaj bırakabilir. Awake'de otomatik oluşturulur; eğer sahnede
        // manuel olarak eklerseniz aynı işi yapar.
        // -------------------------------------------------------------------------
        public static DebugOverlay Instance { get; private set; }

        [Header("Genel Ayarlar")]
        [Tooltip("Master switch. False yaparsanız hiçbir log yazılmaz ve HUD gizlenir.")]
        public bool debugEnabled = true;

        [Tooltip("OnGUI tabanlı ekran içi HUD gösterilsin mi?")]
        public bool showOnScreenHud = true;

        [Tooltip("Konsol loglarına zaman damgası (saniye) eklensin mi?")]
        public bool addTimeStamp = true;

        [Tooltip("Konsol loglarının başına kategori adı eklensin mi?")]
        public bool prefixCategory = true;

        [Header("Log Kategorileri")]
        [Tooltip("Her LogCategory için açık/kapalı ve renk ayarı. Inspector'dan düzenleyin.")]
        public List<LogCategorySettings> categorySettings = new List<LogCategorySettings>();

        [Header("HUD Ayarları")]
        [Tooltip("HUD'un sol üst köşeden pixel uzaklığı.")]
        public Vector2 hudOffset = new Vector2(12f, 12f);

        [Tooltip("HUD genişliği (piksel).")]
        public float hudWidth = 320f;

        [Tooltip("HUD arka plan rengi (RGBA).")]
        public Color hudBackground = new Color(0f, 0f, 0f, 0.55f);

        [Tooltip("HUD yazı rengi.")]
        public Color hudTextColor = Color.white;

        [Tooltip("HUD yazı boyutu.")]
        public int hudFontSize = 14;

        // -------------------------------------------------------------------------
        // ÇALIŞMA ZAMANI DURUMU
        // -------------------------------------------------------------------------
        // HUD'da gösterilecek anlık değerler. Bunlar dışarıdan SetHudValue ile
        // güncellenebilir; böylece her sistem kendi bilgisini paylaşabilir.
        // -------------------------------------------------------------------------
        private readonly Dictionary<string, string> _hudValues = new Dictionary<string, string>();

        // Geliştirici tuş kısayolları aktif mi?
        public bool enableHotkeys = true;

        // -------------------------------------------------------------------------
        // YAŞAM DÖNGÜSÜ
        // -------------------------------------------------------------------------

        private void Awake()
        {
            // Singleton kontrolü: birden fazla olmasın.
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[DebugOverlay] Sahnede birden fazla DebugOverlay var, yenisi yok ediliyor.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // DontDestroyOnLoad sadece root GameObject'lerde çalışır. Eğer bu
            // obje bir parent'ın altındaysa, parent'ı koparıp root yap ki
            // DontDestroyOnLoad çalışsın.
            if (transform.parent != null)
            {
                Debug.LogWarning("[DebugOverlay] Parent tespit edildi, koparılıyor (DontDestroyOnLoad için).");
                transform.SetParent(null, true);
            }
            DontDestroyOnLoad(gameObject);

            // Eğer Inspector'da kategori listesi boşsa, tüm kategoriler için
            // varsayılan ayarları oluştur. Bu sayede kod tarafında Log(...) çağrısı
            // yapıldığında KeyNotFoundException almazsın.
            EnsureCategorySettings();
        }

        private void Start()
        {
            Log(LogCategory.System,
                "DebugOverlay başlatıldı. Konsol logları aktif.", false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!debugEnabled || !enableHotkeys) return;

            // F1 -> HUD aç/kapa
            if (LegacyInputBridge.GetKeyDown(KeyCode.F1))
            {
                showOnScreenHud = !showOnScreenHud;
                Log(LogCategory.System, $"On-screen HUD: {(showOnScreenHud ? "AÇIK" : "KAPALI")}", false);
            }

            // F2 -> Tüm logları aç/kapa
            if (LegacyInputBridge.GetKeyDown(KeyCode.F2))
            {
                debugEnabled = !debugEnabled;
                Log(LogCategory.System, $"Debug sistemi: {(debugEnabled ? "AÇIK" : "KAPALI")}", false);
            }

            // F3 -> Gündüz/gece arası geçiş (hızlı test)
            if (LegacyInputBridge.GetKeyDown(KeyCode.F3))
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.DebugToggleDayNight();
                }
            }
        }

        // -------------------------------------------------------------------------
        // KATEGORİ YÖNETİMİ
        // -------------------------------------------------------------------------

        private void EnsureCategorySettings()
        {
            // Enum'daki tüm kategoriler için bir ayar kaydı olduğundan emin ol.
            foreach (LogCategory cat in Enum.GetValues(typeof(LogCategory)))
            {
                bool exists = false;
                for (int i = 0; i < categorySettings.Count; i++)
                {
                    if (categorySettings[i].category == cat) { exists = true; break; }
                }
                if (!exists)
                {
                    categorySettings.Add(new LogCategorySettings
                    {
                        category = cat,
                        enabled = true,
                        // Her kategori için makul bir varsayılan renk ata.
                        colorHex = cat switch
                        {
                            LogCategory.System => "#88c0d0",
                            LogCategory.Player => "#a3be8c",
                            LogCategory.Enemy => "#bf616a",
                            LogCategory.Build => "#ebcb8b",
                            LogCategory.Combat => "#d08770",
                            LogCategory.Wave => "#b48ead",
                            LogCategory.DayNight => "#f0a070",
                            LogCategory.Skill => "#ffd166",
                            LogCategory.UI => "#8fbcbb",
                            LogCategory.Input => "#5e81ac",
                            LogCategory.Camera => "#7aa2f7",
                            LogCategory.Story => "#ff77aa",
                            _ => "#ffffff"
                        }
                    });
                }
            }
        }

        private bool IsCategoryEnabled(LogCategory cat)
        {
            for (int i = 0; i < categorySettings.Count; i++)
            {
                if (categorySettings[i].category == cat) return categorySettings[i].enabled;
            }
            return true;
        }

        private string GetCategoryColor(LogCategory cat)
        {
            for (int i = 0; i < categorySettings.Count; i++)
            {
                if (categorySettings[i].category == cat) return categorySettings[i].colorHex;
            }
            return "#ffffff";
        }

        // -------------------------------------------------------------------------
        // PUBLIC API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Bir log mesajı yaz. Diğer tüm sistemler bunu çağırır.
        /// </summary>
        /// <param name="cat">Log kategorisi.</param>
        /// <param name="message">Mesaj.</param>
        /// <param name="verbose">True ise yalnızca Inspector'da 'verbose' açıksa yazar.
        /// (Şu an tüm mesajlar yazılır, bu parametre ileride sıkıştırma için.)</param>
        public void Log(LogCategory cat, string message, bool verbose = false)
        {
            if (!debugEnabled) return;
            if (!IsCategoryEnabled(cat)) return;

            string prefix = "";
            if (addTimeStamp)
                prefix += $"[{Time.time:F2}s] ";
            if (prefixCategory)
                prefix += $"[{cat}] ";

            string colored = $"<color={GetCategoryColor(cat)}>{prefix}{message}</color>";
            Debug.Log(colored);
        }

        /// <summary>
        /// HUD üzerinde gösterilecek bir anahtar/değer çifti ayarla.
        /// Örn: SetHudValue("Gün", "3"), SetHudValue("Gece Aşaması", "Build").
        /// </summary>
        public void SetHudValue(string key, string value)
        {
            _hudValues[key] = value;
        }

        public void RemoveHudValue(string key)
        {
            if (_hudValues.ContainsKey(key)) _hudValues.Remove(key);
        }

        public void ClearHudValues()
        {
            _hudValues.Clear();
        }

        // -------------------------------------------------------------------------
        // OnGUI — EKRAN İÇİ HUD
        // -------------------------------------------------------------------------
        // Burada Unity'nin eski OnGUI sistemi kullanıyoruz. Performans kritik
        // değil (sadece geliştirici HUD'u), ayrıca UGUI kurmadan çalışır.
        // İleride ayrı bir UGUI HUD isterseniz bu kaldırılabilir.
        // -------------------------------------------------------------------------
        private void OnGUI()
        {
            if (!debugEnabled || !showOnScreenHud) return;

            // Arka plan kutusu
            var rect = new Rect(hudOffset.x, hudOffset.y, hudWidth, 0);
            GUI.color = hudBackground;
            GUI.Box(new Rect(rect.x, rect.y, rect.width, 24 + _hudValues.Count * (hudFontSize + 4)), "");

            // Başlık
            var prevColor = GUI.color;
            var prevSize = GUI.skin.label.fontSize;
            GUI.color = hudTextColor;
            GUI.skin.label.fontSize = hudFontSize;

            float y = rect.y + 6;
            GUI.Label(new Rect(rect.x + 8, y, rect.width, hudFontSize + 2),
                $"<b>GoodNight My Angel — Debug HUD</b>  (F1 gizle)");
            y += hudFontSize + 6;

            foreach (var kvp in _hudValues)
            {
                GUI.Label(new Rect(rect.x + 8, y, rect.width, hudFontSize + 2),
                    $"<b>{kvp.Key}:</b> {kvp.Value}");
                y += hudFontSize + 4;
            }

            // Geri al
            GUI.color = prevColor;
            GUI.skin.label.fontSize = prevSize;
        }
    }
}
