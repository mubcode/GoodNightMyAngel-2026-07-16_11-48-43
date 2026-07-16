// =============================================================================
// GameBootstrap.cs
// -----------------------------------------------------------------------------
// Oyunun çalışması için gereken temel sistemleri otomatik olarak sahnede
// oluşturan bootstrap script'i. Sahneye boş bir GameObject ekleyip bu
// script'i verdiğinde:
//   - DebugOverlay
//   - GameManager
//   - EventSystem
// oluşturulur.
//
// Bu sayede geliştirme aşamasında sahneyi elle doldurmak zorunda kalmazsın.
// Production için bu kapatılabilir (createEssentials=false).
// =============================================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using GoodNightMyAngel.Core;

namespace GoodNightMyAngel.Core
{
    /// <summary>
    /// Oyunu sıfırdan başlatmak için bootstrap. Sahneye boş bir objeye
    /// eklenir; gerekli temel sistemleri oluşturur.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Otomatik Kurulum")]
        [Tooltip("Başlangıçta temel sistemler otomatik oluşturulsun mu?")]
        public bool createEssentials = true;

        [Tooltip("Oyunu otomatik başlat (gündüz).")]
        public bool autoStartGame = true;

        [Header("Referanslar (otomatik doldurulur)")]
        public GameManager gameManager;
        public DebugOverlay debugOverlay;

        private void Awake()
        {
            if (!createEssentials) return;

            // DebugOverlay yoksa oluştur
            // ÖNEMLİ: DontDestroyOnLoad sadece root GameObject'lerde çalışır,
            // bu yüzden bu objeleri __Bootstrap'a child yapmıyoruz; sahne
            // kökünde bağımsız olarak oluşturuluyorlar.
            if (debugOverlay == null)
            {
                var go = new GameObject("DebugOverlay");
                debugOverlay = go.AddComponent<DebugOverlay>();
            }

            // GameManager yoksa oluştur
            if (gameManager == null)
            {
                var go = new GameObject("GameManager");
                gameManager = go.AddComponent<GameManager>();
            }

            // EventSystem (UI için) — Input System paketiyle uyumlu
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                // StandaloneInputModule eski Input API'sini kullanır ve
                // yeni Input System modunda hata verir. Onun yerine
                // InputSystemUIInputModule kullanıyoruz.
                es.AddComponent<InputSystemUIInputModule>();
            }
        }

        private void Start()
        {
            if (autoStartGame && gameManager != null)
            {
                if (DebugOverlay.Instance != null)
                    DebugOverlay.Instance.Log(LogCategory.System,
                        "GoodNight My Angel — Bootstrap tamamlandı, oyun başlıyor.", false);
            }
        }
    }
}
