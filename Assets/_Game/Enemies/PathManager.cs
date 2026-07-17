// =============================================================================
// PathManager.cs
// -----------------------------------------------------------------------------
// Yeni CS-tarzı rota sistemi: EnemyRoute kullanır.
//
// Her EnemyRoute bir spawn noktası + sıralı point'ler içerir. Yaratıklar
// spawn'da doğar, point'leri takip eder, yatağa yeterince yaklaşınca saldırır.
//
// Inspector'dan:
//   - Yatak referansı (yolun son hedefi)
//   - Routes listesi (EnemyRoute component'i olan objeler)
//   - Yolu çiz (devre dışı bırakılabilir)
//   - Çizgi rengi / kalınlığı / yüksekliği
// ayarlanabilir.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using GoodNightMyAngel.Core;
using GoodNightMyAngel.World;

namespace GoodNightMyAngel.Enemies
{
    /// <summary>
    /// Yaratık rota yöneticisi. Sahne üzerindeki tüm EnemyRoute'ları
    /// indeksler; yeni düşmanlar için rota ataması yapar.
    /// </summary>
    public class PathManager : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // INSPECTOR
        // -------------------------------------------------------------------------
        [Header("Yatak")]
        [Tooltip("Yolun son hedefi (yatak).")]
        public Bed bed;

        [Header("Rotalar")]
        [Tooltip("Yaratıkların takip edeceği EnemyRoute'lar. Boşsa sahnede otomatik aranır.")]
        public List<EnemyRoute> routes = new List<EnemyRoute>();

        [Header("Görsel")]
        [Tooltip("Tüm yolları sahnede LineRenderer ile çiz.")]
        public bool drawPaths = true;

        [Tooltip("Yol çizgisi rengi (yarı transparent beyaz).")]
        public Color pathColor = new Color(1f, 1f, 1f, 0.45f);

        [Tooltip("Yol çizgisi kalınlığı.")]
        [Min(0.01f)] public float pathWidth = 0.08f;

        [Tooltip("Yol çizgisi yüksekliği (zeminden).")]
        [Min(0f)] public float pathHeight = 0.05f;

        // -------------------------------------------------------------------------
        // DURUM
        // -------------------------------------------------------------------------
        // Her rota için: spawn noktası + path (pointler) + bed
        public class RouteData
        {
            public Transform spawnPoint;
            public List<Vector3> points = new List<Vector3>();  // bed hariç
        }

        private List<RouteData> _routes = new List<RouteData>();
        private List<LineRenderer> _pathLines = new List<LineRenderer>();

        public static PathManager Instance { get; private set; }

        // -------------------------------------------------------------------------
        // YAŞAM DÖNGÜSÜ
        // -------------------------------------------------------------------------
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void Start()
        {
            // Bed referansını otomatik bul
            if (bed == null && GameManager.Instance != null)
                bed = GameManager.Instance.bed;
            if (bed == null)
            {
                bed = Object.FindFirstObjectByType<Bed>();
            }

            // Routes boşsa sahnede EnemyRoute'ları ara
            if (routes.Count == 0)
            {
                var found = Object.FindObjectsByType<EnemyRoute>(FindObjectsSortMode.None);
                routes.AddRange(found);
            }

            // Eğer hiç route yoksa kullanıcıya bildir
            if (routes.Count == 0 && DebugOverlay.Instance != null)
            {
                DebugOverlay.Instance.Log(LogCategory.Enemy,
                    "Hiç EnemyRoute yok. Sahneye 'EnemyRoute' component'li obje ekleyin.", true);
            }

            BuildRoutes();
            if (drawPaths) DrawAllPaths();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // -------------------------------------------------------------------------
        // ROTA İNŞASI
        // -------------------------------------------------------------------------
        private void BuildRoutes()
        {
            _routes.Clear();
            foreach (var route in routes)
            {
                if (route == null) continue;
                var data = new RouteData();
                data.spawnPoint = route.SpawnPoint;
                if (data.spawnPoint == null)
                {
                    if (DebugOverlay.Instance != null)
                        DebugOverlay.Instance.Log(LogCategory.Enemy,
                            $"Route '{route.name}': spawn point yok (child gerekli).", true);
                    continue;
                }
                // Spawn noktası + tüm point'ler (sırayla)
                data.points.Add(data.spawnPoint.position);
                foreach (var p in route.PathPoints) data.points.Add(p.position);
                // Yatak ekleme — yatak ayrı bir obje, yaratıklar saldırmak için
                // bedAttackRange içine girmeli. Yolu bitirmeleri gerekmiyor.
                _routes.Add(data);
            }
        }

        // -------------------------------------------------------------------------
        // GÖRSEL ÇİZİM
        // -------------------------------------------------------------------------
        private void DrawAllPaths()
        {
            foreach (var ln in _pathLines) if (ln != null) Destroy(ln.gameObject);
            _pathLines.Clear();

            for (int i = 0; i < _routes.Count; i++)
            {
                var data = _routes[i];
                if (data.points.Count < 2) continue;

                var go = new GameObject($"PathLine_{i}");
                go.transform.SetParent(transform);
                var lr = go.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                lr.material.color = pathColor;
                lr.startColor = pathColor;
                lr.endColor = pathColor;
                lr.startWidth = pathWidth;
                lr.endWidth = pathWidth;
                lr.positionCount = data.points.Count;
                lr.useWorldSpace = true;
                lr.numCapVertices = 4;

                for (int p = 0; p < data.points.Count; p++)
                {
                    Vector3 v = data.points[p];
                    v.y = pathHeight;
                    lr.SetPosition(p, v);
                }
                _pathLines.Add(lr);
            }
        }

        // -------------------------------------------------------------------------
        // PUBLIC API
        // -------------------------------------------------------------------------

        /// <summary>Rastgele bir rota döndürür (spawn + points).</summary>
        public RouteData GetRandomRoute()
        {
            if (_routes.Count == 0) return null;
            return _routes[Random.Range(0, _routes.Count)];
        }

        /// <summary>Belirli bir indeksteki rota.</summary>
        public RouteData GetRoute(int index)
        {
            if (index < 0 || index >= _routes.Count) return null;
            return _routes[index];
        }

        public int RouteCount => _routes.Count;

        /// <summary>Sahnedeki toplam spawn noktası sayısı (tüm route'lar).</summary>
        public List<Transform> GetAllSpawnPoints()
        {
            var list = new List<Transform>();
            foreach (var data in _routes)
            {
                if (data.spawnPoint != null) list.Add(data.spawnPoint);
            }
            return list;
        }
    }
}
