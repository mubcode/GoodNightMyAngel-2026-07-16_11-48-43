// =============================================================================
// PathManager.cs
// -----------------------------------------------------------------------------
// Sahnedeki tüm PathWaypoint'leri yönetir. Fields Runner 2 tarzı:
// her spawn noktası bir waypoint zincirinin başlangıcıdır; düşmanlar
// sırayla waypoint'leri takip ederek yatağa ulaşır.
//
// Inspector'dan:
//   - Yatak referansı (yolun son hedefi)
//   - Başlangıç waypoint'leri listesi (boşsa sahnede otomatik bulur)
//   - Yolu çiz (devre dışı bırakılabilir)
//   - Çizgi rengi / kalınlığı / yüksekliği
//   - Spawn noktası -> en yakın başlangıç waypoint eşleştirmesi aktif mi
// ayarlanabilir.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using GoodNightMyAngel.Core;
using GoodNightMyAngel.World;

namespace GoodNightMyAngel.Enemies
{
    /// <summary>
    /// Yaratık yolu yöneticisi. Sahne üzerindeki tüm PathWaypoint'leri
    /// indeksler; yeni düşmanlar için yol ataması yapar.
    /// </summary>
    public class PathManager : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // INSPECTOR
        // -------------------------------------------------------------------------
        [Header("Yatak")]
        [Tooltip("Yolun son hedefi (yatak).")]
        public Bed bed;

        [Header("Yol Başlangıçları")]
        [Tooltip("Yolun başlangıç waypoint'leri. Boşsa sahnede 'PathStart' taglı objeleri arar.")]
        public List<PathWaypoint> startPoints = new List<PathWaypoint>();

        [Header("Görsel")]
        [Tooltip("Tüm yolları sahnede LineRenderer ile çiz.")]
        public bool drawPaths = true;

        [Tooltip("Yol çizgisi rengi (yarı transparent beyaz, klasik path göstergesi).")]
        public Color pathColor = new Color(1f, 1f, 1f, 0.45f);

        [Tooltip("Yol çizgisi kalınlığı.")]
        [Min(0.01f)] public float pathWidth = 0.08f;

        [Tooltip("Yol çizgisi yüksekliği.")]
        [Min(0f)] public float pathHeight = 0.05f;

        [Header("Hedef Eşleştirme")]
        [Tooltip("EnemySpawner'a otomatik kayıt ol.")]
        public bool registerToSpawner = true;

        // -------------------------------------------------------------------------
        // DURUM
        // -------------------------------------------------------------------------
        // Her yolun tam listesi (start + intermediate + bed)
        private List<List<Vector3>> _paths = new List<List<Vector3>>();
        private List<LineRenderer> _pathLines = new List<LineRenderer>();

        // Singleton
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
                bed = FindFirstObjectByType<Bed>();
            }

            // Eğer manuel liste boşsa sahnede ara
            if (startPoints.Count == 0)
            {
                var found = FindObjectsByType<PathWaypoint>(FindObjectsSortMode.None);
                // Sadece "next"i olmayanlar (yol başlangıçları)
                foreach (var wp in found)
                {
                    if (wp != null && wp.next == null) startPoints.Add(wp);
                }
                // Eğer hiç waypoint yoksa otomatik oluştur
                if (startPoints.Count == 0)
                {
                    if (DebugOverlay.Instance != null)
                        DebugOverlay.Instance.Log(LogCategory.Enemy,
                            "Hiç waypoint yok, otomatik oluşturuluyor.", true);
                    AutoCreatePathsFromSpawnPoints();
                }
            }

            BuildPaths();
            if (drawPaths) DrawAllPaths();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // -------------------------------------------------------------------------
        // OTOMATİK YOL OLUŞTURMA
        // -------------------------------------------------------------------------
        // Eğer sahnede waypoint yoksa, GameManager'ın spawn noktalarından
        // yatağa doğru tek-noktalı yol oluşturur.
        // -------------------------------------------------------------------------
        private void AutoCreatePathsFromSpawnPoints()
        {
            if (GameManager.Instance == null) return;
            var sps = GameManager.Instance.enemySpawnPoints;
            if (sps == null || sps.Length == 0) return;
            if (bed == null) return;

            for (int i = 0; i < sps.Length; i++)
            {
                var sp = sps[i];
                if (sp == null) continue;
                var go = new GameObject($"PathStart_Auto_{i}");
                go.transform.position = sp.position;
                var wp = go.AddComponent<PathWaypoint>();
                wp.gizmoColor = new Color(0.3f, 0.7f, 1f, 0.9f);
                startPoints.Add(wp);
            }

            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Enemy,
                    $"{startPoints.Count} otomatik yol başlangıcı oluşturuldu.", false);
        }

        // -------------------------------------------------------------------------
        // YOL İNŞASI
        // -------------------------------------------------------------------------
        private void BuildPaths()
        {
            _paths.Clear();
            foreach (var start in startPoints)
            {
                if (start == null) continue;
                var path = new List<Vector3>();
                var current = start;
                int safety = 100;     // sonsuz döngü koruması
                while (current != null && safety-- > 0)
                {
                    path.Add(current.transform.position);
                    if (current.next == null) break;
                    current = current.next;
                }
                if (bed != null) path.Add(bed.transform.position);
                _paths.Add(path);
            }
        }

        // -------------------------------------------------------------------------
        // GÖRSEL ÇİZİM
        // -------------------------------------------------------------------------
        private void DrawAllPaths()
        {
            // Eski line'ları temizle
            foreach (var ln in _pathLines) if (ln != null) Destroy(ln.gameObject);
            _pathLines.Clear();

            for (int i = 0; i < _paths.Count; i++)
            {
                var path = _paths[i];
                if (path.Count < 2) continue;

                var go = new GameObject($"PathLine_{i}");
                go.transform.SetParent(transform);
                var lr = go.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                lr.material.color = pathColor;
                lr.startColor = pathColor;
                lr.endColor = pathColor;
                lr.startWidth = pathWidth;
                lr.endWidth = pathWidth * 1.5f;
                lr.positionCount = path.Count;
                lr.useWorldSpace = true;
                lr.numCapVertices = 4;

                for (int p = 0; p < path.Count; p++)
                {
                    Vector3 v = path[p];
                    v.y = pathHeight;
                    lr.SetPosition(p, v);
                }
                _pathLines.Add(lr);
            }
        }

        // -------------------------------------------------------------------------
        // PUBLIC API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Verilen bir düşmana waypoint yol listesini kopyalar. Düşman Update'inde
        /// bu listeyi sırayla takip eder.
        /// </summary>
        public List<Vector3> GetRandomPath()
        {
            if (_paths.Count == 0) return null;
            return new List<Vector3>(_paths[Random.Range(0, _paths.Count)]);
        }

        /// <summary>Belirli bir indeksteki yolu döndürür.</summary>
        public List<Vector3> GetPath(int index)
        {
            if (index < 0 || index >= _paths.Count) return null;
            return new List<Vector3>(_paths[index]);
        }

        public int PathCount => _paths.Count;

        /// <summary>En yakın başlangıç waypoint'in indeksini bul.</summary>
        public int GetClosestPathIndex(Vector3 worldPos)
        {
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _paths.Count; i++)
            {
                if (_paths[i].Count == 0) continue;
                float d = Vector3.Distance(worldPos, _paths[i][0]);
                if (d < bestDist) { best = i; bestDist = d; }
            }
            return best;
        }
    }
}
