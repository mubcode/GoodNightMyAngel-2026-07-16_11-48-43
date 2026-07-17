// =============================================================================
// EnemySpawner.cs
// -----------------------------------------------------------------------------
// Dalga başladığında GameManager tarafından çağrılan, düşmanları orman
// kenarındaki spawn noktalarından çıkaran sistem.
//
// Inspector'dan:
//   - Spawn noktaları (Transform[])
//   - Normal ve boss prefab'ları
//   - Spawn aralığı (saniye)
//   - Hangi spawn noktalarının açık olduğu
// ayarlanabilir.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GoodNightMyAngel.Core;

namespace GoodNightMyAngel.Enemies
{
    /// <summary>
    /// Düşman spawn yöneticisi. Statik API'si sayesinde GameManager her
    /// dalga için SpawnWave(...) çağırır, sistem geri kalanını halleder.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // INSPECTOR
        // -------------------------------------------------------------------------
        [Header("Spawn Noktaları")]
        [Tooltip("Spawn noktaları. GameManager tarafından da override edilebilir.")]
        public Transform[] spawnPoints;

        [Tooltip("Spawn noktası tag'ı. Boşsa spawnPoints dizisi kullanılır.")]
        public string spawnPointTag = "EnemySpawn";

        [Header("Prefabs")]
        [Tooltip("Normal düşman prefab'ı (EnemyBase içermeli).")]
        public GameObject normalEnemyPrefab;

        [Tooltip("Boss prefab'ı (EnemyBase, isBoss=true içermeli).")]
        public GameObject bossPrefab;

        [Header("Zamanlama")]
        [Tooltip("Spawn'lar arası minimum bekleme (saniye).")]
        [Min(0.05f)] public float spawnInterval = 0.5f;

        [Tooltip("Spawn başlangıcı için rastgele gecikme (saniye).")]
        [Min(0f)] public float randomInitialDelay = 0.5f;

        [Header("Yönlendirme")]
        [Tooltip("Spawn noktasındaki yönlendirme (waypoint) — düşmanlar buraya yönelir.")]
        public Transform fallbackTarget;

        // -------------------------------------------------------------------------
        // STATİK DURUM
        // -------------------------------------------------------------------------
        public static int AliveCount { get; private set; } = 0;
        private static EnemySpawner _instance;

        // Aktif coroutine'leri takip et (instance yeniden doğmasın diye)
        private readonly List<EnemyBase> _aliveEnemies = new List<EnemyBase>();

        // -------------------------------------------------------------------------
        // YAŞAM DÖNGÜSÜ
        // -------------------------------------------------------------------------
        private void Awake()
        {
            _instance = this;
            // Eğer spawnPoints boşsa tag ile ara
            if ((spawnPoints == null || spawnPoints.Length == 0) && !string.IsNullOrEmpty(spawnPointTag))
            {
                var found = GameObject.FindGameObjectsWithTag(spawnPointTag);
                spawnPoints = new Transform[found.Length];
                for (int i = 0; i < found.Length; i++) spawnPoints[i] = found[i].transform;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // -------------------------------------------------------------------------
        // PUBLIC API
        // -------------------------------------------------------------------------

        /// <summary>GameManager tarafından her dalga başında çağrılır.</summary>
        public static void SpawnWave(int count, float hpMul)
        {
            if (_instance == null)
            {
                Debug.LogWarning("[EnemySpawner] Spawner instance yok. Sahneye EnemySpawner ekleyin.");
                return;
            }
            _instance.StartCoroutine(_instance.SpawnWaveRoutine(count, hpMul));
        }

        public static void OnEnemyDied(EnemyBase e)
        {
            if (e != null) AliveCount = Mathf.Max(0, AliveCount - 1);
            // Instance varsa kendi listesinden de çıkar
            if (_instance != null) _instance._aliveEnemies.Remove(e);
        }

        // -------------------------------------------------------------------------
        // COROUTINE
        // -------------------------------------------------------------------------
        private IEnumerator SpawnWaveRoutine(int count, float hpMul)
        {
            if (normalEnemyPrefab == null)
            {
                Debug.LogError("[EnemySpawner] normalEnemyPrefab atanmamış!");
                yield break;
            }

            // Spawn noktalarını PathManager'dan veya kendi dizisinden al
            // (PathManager varsa onun spawn noktaları tercih edilir)
            var spawnList = new List<Transform>();
            if (PathManager.Instance != null)
            {
                var pmSpawns = PathManager.Instance.GetAllSpawnPoints();
                if (pmSpawns.Count > 0) spawnList.AddRange(pmSpawns);
            }
            if (spawnList.Count == 0 && spawnPoints != null)
            {
                foreach (var sp in spawnPoints)
                    if (sp != null) spawnList.Add(sp);  // null olanları atla
            }
            if (spawnList.Count == 0)
            {
                Debug.LogError("[EnemySpawner] Hiç spawn noktası yok! (PathManager veya spawnPoints dizisi boş)");
                yield break;
            }

            // Başlangıç gecikmesi (PAUSE-aware)
            float initial = Random.Range(0f, randomInitialDelay);
            float waited = 0f;
            while (waited < initial)
            {
                while (GameManager.Instance != null && GameManager.Instance.Status == GameStatus.Paused) yield return null;
                waited += Time.deltaTime;
                yield return null;
            }

            for (int i = 0; i < count; i++)
            {
                while (GameManager.Instance != null && GameManager.Instance.Status == GameStatus.Paused) yield return null;

                Transform sp = spawnList[Random.Range(0, spawnList.Count)];
                if (sp == null) continue;

                GameObject go = Instantiate(normalEnemyPrefab, sp.position, sp.rotation);
                var enemy = go.GetComponent<EnemyBase>();
                if (enemy == null)
                {
                    Debug.LogError("[EnemySpawner] Prefab üzerinde EnemyBase yok!");
                    Destroy(go);
                    continue;
                }

                enemy.healthMultiplier = hpMul;
                if (GameManager.Instance != null)
                {
                    if (enemy.targetBed == null)
                        enemy.targetBed = GameManager.Instance.bed;
                }

                _aliveEnemies.Add(enemy);
                AliveCount = _aliveEnemies.Count;

                if (DebugOverlay.Instance != null)
                    DebugOverlay.Instance.Log(LogCategory.Enemy,
                        $"Spawn: {enemy.name} @ {sp.name} (HP x{hpMul:F2})", false);

                // Spawn aralığı (PAUSE-aware)
                float t = 0f;
                while (t < spawnInterval)
                {
                    while (GameManager.Instance != null && GameManager.Instance.Status == GameStatus.Paused) yield return null;
                    t += Time.deltaTime;
                    yield return null;
                }
            }
        }

        // -------------------------------------------------------------------------
        // DEBUG ÇİZİM
        // -------------------------------------------------------------------------
        private void OnDrawGizmos()
        {
            if (spawnPoints == null) return;
            Gizmos.color = Color.red;
            foreach (var sp in spawnPoints)
            {
                if (sp == null) continue;
                Gizmos.DrawWireSphere(sp.position, 0.5f);
                Gizmos.DrawLine(sp.position, sp.position + sp.forward * 1.5f);
            }
            // Eğer spawnPoints boşsa, tag ile arama yap ve gizmos çiz (debug için)
            if (spawnPoints.Length == 0 && !string.IsNullOrEmpty(spawnPointTag))
            {
                var found = GameObject.FindGameObjectsWithTag(spawnPointTag);
                Gizmos.color = new Color(1f, 0.5f, 0.5f, 0.7f);
                foreach (var f in found)
                {
                    if (f == null) continue;
                    Gizmos.DrawWireSphere(f.transform.position, 0.4f);
                }
            }
        }
    }
}
