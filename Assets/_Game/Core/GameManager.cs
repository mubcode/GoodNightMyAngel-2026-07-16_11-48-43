// =============================================================================
// GameManager.cs
// -----------------------------------------------------------------------------
// Oyunun beyni. Sahip olduğu sorumluluklar:
//   1) Gündüz / gece döngüsünü yönetmek (TimeOfDay enum'u ile).
//   2) Build phase süresini geri saymak ve bittiğinde savunma fazına geçmek.
//   3) Dalga (wave) sayısını, boss'u ve düşman spawn'ını koordine etmek.
//   4) Kazanma / kaybetme durumlarını ele almak (GameStatus).
//   5) Geliştirici kısayolları için DebugToggleDayNight gibi metodlar sunmak.
//
// Tüm zamanlama değerleri Inspector'dan ayarlanabilir; her geçişte hem
// event hem konsol logu atılır.
// =============================================================================

using System;
using System.Collections;
using UnityEngine;
using GoodNightMyAngel.Enemies;

namespace GoodNightMyAngel.Core
{
    /// <summary>
    /// Oyunun ana yöneticisi. Sahnede bir tane bulunur (DontDestroyOnLoad).
    /// Diğer tüm sistemler olaylara buradan abone olur.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // SINGLETON
        // -------------------------------------------------------------------------
        public static GameManager Instance { get; private set; }

        // -------------------------------------------------------------------------
        // INSPECTOR — ZAMANLAMA
        // -------------------------------------------------------------------------
        [Header("Başlangıç Durumu")]
        [Tooltip("Oyun ilk açıldığında hangi günle başlıyoruz?")]
        public int startDay = 1;

        [Tooltip("Oyun başladığında gündüz mü başlıyoruz?")]
        public bool startInDay = true;

        [Header("Build Phase Süreleri")]
        [Tooltip("Her gece build phase için kaç saniye süre veriliyor.")]
        [Min(1f)] public float baseBuildPhaseSeconds = 60f;

        [Tooltip("Her yeni gecede build phase süresine eklenen saniye (giderek daha kısa olabilir).")]
        public float buildPhaseIncreasePerNight = -5f;

        [Tooltip("Build phase süresinin alt sınırı (saniye).")]
        [Min(5f)] public float minBuildPhaseSeconds = 20f;

        [Header("Dalgalar")]
        [Tooltip("Her gecedeki dalga sayısı (boss hariç).")]
        [Min(1)] public int wavesPerNight = 3;

        [Tooltip("Her yeni gecede dalga sayısına eklenen miktar.")]
        public int wavesIncreasePerNight = 1;

        [Tooltip("İlk gecede kaç düşman spawn olacak.")]
        [Min(1)] public int baseEnemiesPerWave = 5;

        [Tooltip("Her yeni dalgada düşman sayısına eklenen miktar.")]
        public int enemiesIncreasePerWave = 2;

        [Tooltip("Her yeni gecede, düşmanlara eklenen can çarpanı (1.0 = aynı).")]
        public float enemyHealthMultiplierPerNight = 0.1f;

        [Header("Dalga Zamanlaması")]
        [Tooltip("Dalgalar arasındaki bekleme süresi (saniye).")]
        [Min(0f)] public float timeBetweenWaves = 4f;

        [Tooltip("Son dalgadan sonra boss spawn olmadan önceki gecikme.")]
        [Min(0f)] public float timeBeforeBoss = 3f;

        [Header("Boss")]
        [Tooltip("Boss için düşman prefab'ı. Boş bırakılırsa boss spawn olmaz.")]
        public GameObject bossPrefab;

        [Tooltip("Boss spawn noktaları (orman kenarı). Boşsa rastgele bir nokta seçilir.")]
        public Transform[] bossSpawnPoints;

        [Header("Spawn Noktaları")]
        [Tooltip("Gece düşmanlarının spawn olacağı orman kenarı noktaları.")]
        public Transform[] enemySpawnPoints;

        [Tooltip("Hangi noktaların kullanılabilir olduğunu belirleyen bayrak dizisi. " +
                 "Eğer boşsa, tüm noktalar kullanılabilir.")]
        public bool[] spawnPointEnabled;

        [Header("Yatak (Rüya gören beden)")]
        [Tooltip("Oyundaki yatak referansı. Yatağın canı sıfıra düşerse oyun biter.")]
        public World.Bed bed;

        [Header("Sahne Geçiş Süreleri")]
        [Tooltip("Gündüz -> gece geçişi için ekran karartma süresi (saniye).")]
        [Min(0f)] public float dayToNightFade = 1.5f;

        [Tooltip("Gece -> sabah geçişi için ekran aydınlanma süresi (saniye).")]
        [Min(0f)] public float nightToDayFade = 2f;

        // -------------------------------------------------------------------------
        // OLAYLAR (EVENTS)
        // -------------------------------------------------------------------------
        // Diğer sistemler (UI, ses, düşman spawner vb.) bu olaylara abone olarak
        // tepki verir. Doğrudan metod çağrısı yerine event kullanmak sistemler
        // arası bağımlılığı azaltır.
        // -------------------------------------------------------------------------

        /// <summary>Gündüz başladığında tetiklenir.</summary>
        public event Action<int> OnDayStarted;

        /// <summary>Gece başladığında (yatmadan önceki karanlık geçiş) tetiklenir.</summary>
        public event Action<int> OnNightStarted;

        /// <summary>Build phase başladığında tetiklenir.</summary>
        public event Action<float> OnBuildPhaseStarted;

        /// <summary>Build phase bittiğinde tetiklenir.</summary>
        public event Action OnBuildPhaseEnded;

        /// <summary>Yeni bir dalga başladığında tetiklenir (waveNumber 1-indexed).</summary>
        public event Action<int, int> OnWaveStarted;     // (waveIndex, enemyCount)

        /// <summary>Bir dalga bittiğinde tetiklenir.</summary>
        public event Action<int> OnWaveCleared;

        /// <summary>Boss spawn olduğunda tetiklenir.</summary>
        public event Action OnBossSpawned;

        /// <summary>Boss öldüğünde tetiklenir.</summary>
        public event Action OnBossDefeated;

        /// <summary>Sabah olduğunda tetiklenir (gece başarıyla bitti).</summary>
        public event Action<int> OnDawnArrived;

        /// <summary>Oyun kaybedildiğinde tetiklenir.</summary>
        public event Action OnGameOver;

        /// <summary>Oyun duraklatıldığında / devam ettirildiğinde tetiklenir.</summary>
        public event Action<bool> OnPauseChanged;

        // -------------------------------------------------------------------------
        // ÇALIŞMA ZAMANI DURUMU
        // -------------------------------------------------------------------------
        public TimeOfDay TimeOfDay { get; private set; }
        public GameStatus Status { get; private set; } = GameStatus.Playing;
        public int CurrentDay { get; private set; } = 1;
        public int CurrentWave { get; private set; } = 0;
        public float BuildPhaseTimeRemaining { get; private set; }

        private Coroutine _nightLoopRoutine;

        // -------------------------------------------------------------------------
        // YAŞAM DÖNGÜSÜ
        // -------------------------------------------------------------------------
        private void Awake()
        {
            // Singleton kontrolü
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            CurrentDay = Mathf.Max(1, startDay);
            TimeOfDay = startInDay ? TimeOfDay.Day : TimeOfDay.NightBuild;
        }

        private void Start()
        {
            // Başlangıç olaylarını tetikle
            if (TimeOfDay == TimeOfDay.Day)
            {
                StartDay(CurrentDay);
            }
            else
            {
                StartNight(CurrentDay);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // ESC veya P ile duraklatma
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
            {
                TogglePause();
            }
        }

        // -------------------------------------------------------------------------
        // GÜNDÜZ AKIŞI
        // -------------------------------------------------------------------------

        /// <summary>
        /// Yeni bir gün başlat. Gündüz olaylarını tetikler.
        /// </summary>
        public void StartDay(int day)
        {
            CurrentDay = day;
            TimeOfDay = TimeOfDay.Day;
            Status = GameStatus.Playing;

            LogDay("Yeni gün başladı. Karakter yatağından uyandı.");

            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.SetHudValue("Zaman", "Gündüz");
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.SetHudValue("Gün", CurrentDay.ToString());

            OnDayStarted?.Invoke(CurrentDay);
        }

        /// <summary>
        /// Gündüz bitti — gece başlıyor. Build phase tetiklenir.
        /// </summary>
        public void StartNight(int day)
        {
            CurrentDay = day;
            TimeOfDay = TimeOfDay.NightBuild;
            Status = GameStatus.Playing;

            float buildSec = GetBuildPhaseSecondsForDay(day);
            BuildPhaseTimeRemaining = buildSec;

            LogDay($"Gece başladı. Build phase süresi: {buildSec:F1}s " +
                   $"({wavesPerNight + (day - 1) * wavesIncreasePerNight} dalga bekleniyor).");

            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.SetHudValue("Zaman", "Gece — Build");
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.SetHudValue("Build Süresi", $"{BuildPhaseTimeRemaining:F1}s");

            OnNightStarted?.Invoke(CurrentDay);
            OnBuildPhaseStarted?.Invoke(buildSec);

            // Gece döngüsünü başlat (build -> savunma -> boss -> sabah)
            if (_nightLoopRoutine != null) StopCoroutine(_nightLoopRoutine);
            _nightLoopRoutine = StartCoroutine(NightLoop());
        }

        // -------------------------------------------------------------------------
        // GECE DÖNGÜSÜ
        // -------------------------------------------------------------------------

        private IEnumerator NightLoop()
        {
            // 1) Build phase
            while (BuildPhaseTimeRemaining > 0f && Status == GameStatus.Playing)
            {
                BuildPhaseTimeRemaining -= Time.deltaTime;
                if (DebugOverlay.Instance != null)
                    DebugOverlay.Instance.SetHudValue("Build Süresi", $"{Mathf.Max(0, BuildPhaseTimeRemaining):F1}s");
                yield return null;
            }

            if (Status != GameStatus.Playing)
            {
                LogDay("Build phase yarıda kesildi (oyun durumu değişti).");
                yield break;
            }

            OnBuildPhaseEnded?.Invoke();
            LogDay("Build phase bitti. Savunma fazına geçiliyor.");

            // 2) Dalgalar
            int totalWaves = wavesPerNight + (CurrentDay - 1) * wavesIncreasePerNight;
            for (int w = 1; w <= totalWaves; w++)
            {
                if (Status != GameStatus.Playing) yield break;
                yield return StartCoroutine(RunWave(w, totalWaves));
                yield return new WaitForSeconds(timeBetweenWaves);
            }

            // 3) Boss
            if (bossPrefab != null && Status == GameStatus.Playing)
            {
                TimeOfDay = TimeOfDay.NightBoss;
                LogDay("Tüm dalgalar temizlendi. Boss yaklaşıyor!");
                if (DebugOverlay.Instance != null)
                    DebugOverlay.Instance.SetHudValue("Zaman", "Gece — BOSS");

                yield return new WaitForSeconds(timeBeforeBoss);
                SpawnBoss();
                // Boss ölünceye kadar bekle. Bed veya boss ölümü tarafından yönetilir.
                while (Status == GameStatus.Playing && !_bossDefeated) yield return null;
            }

            // 4) Sabah
            if (Status == GameStatus.Playing)
            {
                StartDawn();
            }
        }

        private IEnumerator RunWave(int waveIndex, int totalWaves)
        {
            CurrentWave = waveIndex;
            TimeOfDay = TimeOfDay.NightDefense;

            int enemyCount = baseEnemiesPerWave + (waveIndex - 1) * enemiesIncreasePerWave;
            float hpMul = 1f + (CurrentDay - 1) * enemyHealthMultiplierPerNight;

            LogDay($"Dalga {waveIndex}/{totalWaves} başlıyor: {enemyCount} düşman, " +
                   $"HP çarpanı x{hpMul:F2}");

            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.SetHudValue("Dalga", $"{waveIndex}/{totalWaves}");
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.SetHudValue("Kalan Düşman", enemyCount.ToString());

            OnWaveStarted?.Invoke(waveIndex, enemyCount);

            // Spawner'a düşmanları çağırt. Spawner kendi içinde süreye yayar.
            Enemies.EnemySpawner.SpawnWave(enemyCount, hpMul);

            // Spawner'ın tüm düşmanlar ölünceye kadar bekle.
            while (Enemies.EnemySpawner.AliveCount > 0 && Status == GameStatus.Playing)
            {
                if (DebugOverlay.Instance != null)
                    DebugOverlay.Instance.SetHudValue("Kalan Düşman", Enemies.EnemySpawner.AliveCount.ToString());
                yield return null;
            }

            if (Status != GameStatus.Playing) yield break;

            LogDay($"Dalga {waveIndex} temizlendi.");
            OnWaveCleared?.Invoke(waveIndex);
        }

        private bool _bossDefeated = false;
        public void NotifyBossDefeated()
        {
            _bossDefeated = true;
            LogDay("Boss yenildi!");
            OnBossDefeated?.Invoke();
        }

        private void SpawnBoss()
        {
            _bossDefeated = false;
            Transform spawn = null;
            if (bossSpawnPoints != null && bossSpawnPoints.Length > 0)
            {
                spawn = bossSpawnPoints[UnityEngine.Random.Range(0, bossSpawnPoints.Length)];
            }
            if (spawn == null) spawn = (enemySpawnPoints != null && enemySpawnPoints.Length > 0)
                ? enemySpawnPoints[0] : null;

            if (spawn == null)
            {
                Debug.LogWarning("[GameManager] Boss için spawn noktası bulunamadı!");
                return;
            }

            Instantiate(bossPrefab, spawn.position, spawn.rotation);
            LogDay("Boss spawn oldu!");
            OnBossSpawned?.Invoke();
        }

        private void StartDawn()
        {
            TimeOfDay = TimeOfDay.Dawn;
            Status = GameStatus.NightCleared;
            LogDay("Sabah oldu. Çocuk sağlıklı bir şekilde uyandı!");

            // Checkpoint'i güncelle (gece başarıyla tamamlandı)
            if (Checkpoint.LastClearedDay < CurrentDay)
                Checkpoint.LastClearedDay = CurrentDay;

            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.SetHudValue("Zaman", "Şafak ☀");

            OnDawnArrived?.Invoke(CurrentDay);

            // Kısa geçişten sonra yeni güne başla
            StartCoroutine(WaitAndStartNewDay());
        }

        private IEnumerator WaitAndStartNewDay()
        {
            yield return new WaitForSeconds(nightToDayFade);
            StartDay(CurrentDay + 1);
        }

        // -------------------------------------------------------------------------
        // KAYBETME
        // -------------------------------------------------------------------------

        /// <summary>
        /// Yatak canı sıfıra düştü — oyunu kaybettik. GameManager tarafından çağrılır.
        /// </summary>
        public void GameOver()
        {
            if (Status == GameStatus.GameOver) return;
            Status = GameStatus.GameOver;
            TimeOfDay = TimeOfDay.NightDefense; // gece sırasında öldü
            LogDay("OYUN BİTTİ. Çocuk rüyasından uyanamadı.");
            OnGameOver?.Invoke();
        }

        // -------------------------------------------------------------------------
        // DURAKLATMA
        // -------------------------------------------------------------------------

        public void TogglePause()
        {
            if (Status == GameStatus.GameOver) return;
            if (Status == GameStatus.Paused) Resume();
            else Pause();
        }

        public void Pause()
        {
            Status = GameStatus.Paused;
            Time.timeScale = 0f;
            LogDay("Oyun duraklatıldı.");
            OnPauseChanged?.Invoke(true);
        }

        public void Resume()
        {
            Status = GameStatus.Playing;
            Time.timeScale = 1f;
            LogDay("Oyun devam ediyor.");
            OnPauseChanged?.Invoke(false);
        }

        // -------------------------------------------------------------------------
        // YARDIMCI METODLAR
        // -------------------------------------------------------------------------

        private float GetBuildPhaseSecondsForDay(int day)
        {
            float s = baseBuildPhaseSeconds + (day - 1) * buildPhaseIncreasePerNight;
            return Mathf.Max(minBuildPhaseSeconds, s);
        }

        private void LogDay(string msg)
        {
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.System,
                    $"[Gün {CurrentDay}] {msg}", false);
        }

        // -------------------------------------------------------------------------
        // GELİŞTİRİCİ KISAYOLLARI
        // -------------------------------------------------------------------------
        // Inspector'dan veya tuşla tetiklenebilen hızlı test metodları.
        // -------------------------------------------------------------------------

        /// <summary>F3 tuşu ile gündüz/gece arası hızlı geçiş (test için).</summary>
        public void DebugToggleDayNight()
        {
            if (TimeOfDay == TimeOfDay.Day)
            {
                StartNight(CurrentDay);
            }
            else
            {
                if (_nightLoopRoutine != null) StopCoroutine(_nightLoopRoutine);
                StartDay(CurrentDay + 1);
            }
        }

        /// <summary>Belirli bir gün sayısına atla.</summary>
        public void DebugJumpToDay(int day)
        {
            if (day < 1) day = 1;
            if (_nightLoopRoutine != null) StopCoroutine(_nightLoopRoutine);
            StartDay(day);
        }

        /// <summary>Bir sonraki dalgayı hemen tetikle (build phase'i atla).</summary>
        public void DebugSkipToNextWave()
        {
            if (TimeOfDay != TimeOfDay.NightBuild) return;
            BuildPhaseTimeRemaining = 0f;
            LogDay("DEBUG: Build phase atlandı.");
        }

        // -------------------------------------------------------------------------
        // EDITOR
        // -------------------------------------------------------------------------
        // Inspector'da spawn noktası dizisi değiştiğinde enabled dizisini senkronla.
        // -------------------------------------------------------------------------
        private void OnValidate()
        {
            if (enemySpawnPoints != null)
            {
                if (spawnPointEnabled == null || spawnPointEnabled.Length != enemySpawnPoints.Length)
                {
                    Array.Resize(ref spawnPointEnabled, enemySpawnPoints.Length);
                    for (int i = 0; i < spawnPointEnabled.Length; i++)
                        if (!spawnPointEnabled[i]) spawnPointEnabled[i] = true;
                }
            }
        }
    }

    /// <summary>
    /// Statik checkpoint deposu. Şimdilik sadece son başarılı günü tutuyoruz.
    /// Oyun kaybedildiğinde buradan geri yüklenir.
    /// </summary>
    public static class Checkpoint
    {
        public static int LastClearedDay { get; set; } = 0;
    }
}
