// =============================================================================
// BuildManager.cs
// -----------------------------------------------------------------------------
// Gece build phase boyunca oyuncunun savunma elemanlarını yerleştirmesini
// yöneten ana sistem. Inspector'dan:
//   - Grid boyutu (hücre birim uzunluğu)
//   - Grid merkezi (yatak pozisyonu etrafında)
//   - Mevcut eşya kataloğu (BuildItemData listesi)
//   - Başlangıç parası
//   - Yol göstergesi (LineRenderer) için renk ve kalınlık
// ayarlanabilir.
//
// Akış:
//   1) Build phase başlar, oyuncu "1, 2, 3" ile kategori seçer.
//   2) Mouse ile grid üzerinde gezinir; uygun hücre yeşil/kırmızı boyanır.
//   3) Sol tık -> yerleştir (maliyet düşer).
//   4) Sağ tık -> var olan bir elemanı seç, tamir et veya kaldır.
//   5) Yaratık yolu (gece başında) tırtıklı çizgi ile gösterilir.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using GoodNightMyAngel.Core;

namespace GoodNightMyAngel.Build
{
    /// <summary>
    /// Build phase ana yöneticisi. Grid, yerleştirme, tamir ve yol
    /// göstergesini yönetir.
    /// </summary>
    public class BuildManager : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // INSPECTOR
        // -------------------------------------------------------------------------
        [Header("Grid")]
        [Tooltip("Bir hücrenin dünya uzayındaki birim uzunluğu.")]
        [Min(0.5f)] public float cellSize = 1f;

        [Tooltip("Grid merkezi. Boşsa yatak pozisyonu kullanılır.")]
        public Transform gridCenter;

        [Tooltip("Grid yarıçapı (hücre cinsinden, merkezden uzaklık).")]
        [Min(1)] public int gridRadiusCells = 12;

        [Tooltip("Yerleştirilebilir katman (LayerMask).")]
        public LayerMask placementMask = ~0;

        [Header("Katalog")]
        [Tooltip("Yerleştirilebilecek savunma elemanları. Sıra = hızlı seçim tuşu (1, 2, 3 ...).")]
        public List<BuildItemData> catalog = new List<BuildItemData>();

        [Header("Ekonomi")]
        [Tooltip("Oyuncunun başlangıç parası.")]
        [Min(0)] public int startingCurrency = 100;

        [Header("Yol Göstergesi")]
        [Tooltip("Yaratık yolunun gösterileceği LineRenderer. Boşsa runtime oluşturulur.")]
        public LineRenderer pathLine;

        [Tooltip("Yol çizgisi rengi.")]
        public Color pathColor = new Color(0.8f, 0.2f, 0.2f, 0.9f);

        [Tooltip("Yol çizgisi kalınlığı.")]
        [Min(0.01f)] public float pathWidth = 0.08f;

        [Tooltip("Yol çizgisi tırtık sayısı (yumuşaklık).")]
        [Range(2, 60)] public int pathSegments = 24;

        [Header("Girdi")]
        [Tooltip("Sol tık yerleştirir.")]
        public KeyCode placeKey = KeyCode.Mouse0;

        [Tooltip("Sağ tık seçer/etkileşir.")]
        public KeyCode interactKey = KeyCode.Mouse1;

        // -------------------------------------------------------------------------
        // DURUM
        // -------------------------------------------------------------------------
        public int Currency { get; private set; }

        // Grid: tüm hücreler dict'te tutulur (Vector2Int -> BuildItem)
        private readonly Dictionary<Vector2Int, BuildItem> _items = new Dictionary<Vector2Int, BuildItem>();

        private int _selectedIndex = 0;
        private Camera _cam;
        private Vector2Int? _hoverCell;
        private BuildItem _selectedItem;

        public bool IsBuildPhase => GameManager.Instance != null &&
            GameManager.Instance.TimeOfDay == TimeOfDay.NightBuild;

        // -------------------------------------------------------------------------
        // YAŞAM DÖNGÜSÜ
        // -------------------------------------------------------------------------
        private void Awake()
        {
            _cam = Camera.main;
            Currency = startingCurrency;
        }

        private void Start()
        {
            // Yolu çiz
            DrawEnemyPath();
            UpdateHud();
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Build,
                    $"BuildManager hazır. Para: {Currency}, Katalog: {catalog.Count} eşya.", false);
        }

        private void OnEnable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnBuildPhaseStarted += HandleBuildStarted;
                GameManager.Instance.OnBuildPhaseEnded += HandleBuildEnded;
            }
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnBuildPhaseStarted -= HandleBuildStarted;
                GameManager.Instance.OnBuildPhaseEnded -= HandleBuildEnded;
            }
        }

        private void HandleBuildStarted(float t)
        {
            _selectedItem = null;
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Build,
                    $"Build phase başladı. {t:F0}s süren var.", false);
        }

        private void HandleBuildEnded()
        {
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Build, "Build phase bitti.", false);
        }

        private void Update()
        {
            if (!IsBuildPhase) return;

            // Kısayol tuşları: 1..9 ile katalog seç
            for (int i = 0; i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) && i < catalog.Count)
                {
                    _selectedIndex = i;
                    if (DebugOverlay.Instance != null)
                        DebugOverlay.Instance.Log(LogCategory.Build,
                            $"Seçili eşya: {catalog[i].displayName} (maliyet {catalog[i].cost})", false);
                }
            }

            // Mouse hover -> grid hücresi hesapla
            UpdateHoverCell();

            // Sol tık -> yerleştir
            if (_hoverCell.HasValue && Input.GetKeyDown(placeKey))
            {
                TryPlaceAt(_hoverCell.Value);
            }

            // Sağ tık -> seç / tamir / kaldır
            if (Input.GetKeyDown(interactKey))
            {
                TryInteractAt(_hoverCell);
            }

            // R tuşu: seçili eşyayı tamir et
            if (Input.GetKeyDown(KeyCode.R) && _selectedItem != null)
            {
                TryRepair(_selectedItem);
            }
        }

        // -------------------------------------------------------------------------
        // GRID HESAPLAMA
        // -------------------------------------------------------------------------
        private Vector3 GridOrigin =>
            gridCenter != null ? gridCenter.position : Vector3.zero;

        private Vector2Int WorldToCell(Vector3 world)
        {
            Vector3 local = world - GridOrigin;
            int x = Mathf.RoundToInt(local.x / cellSize);
            int z = Mathf.RoundToInt(local.z / cellSize);
            return new Vector2Int(x, z);
        }

        private Vector3 CellToWorld(Vector2Int cell)
        {
            return GridOrigin + new Vector3(cell.x * cellSize, 0, cell.y * cellSize);
        }

        private bool IsCellInBounds(Vector2Int cell)
        {
            return Mathf.Abs(cell.x) <= gridRadiusCells && Mathf.Abs(cell.y) <= gridRadiusCells;
        }

        private void UpdateHoverCell()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) { _hoverCell = null; return; }

            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            // Yatay düzlem (XZ) ile kesişim noktasını bul. Y=0 düzlem.
            Plane ground = new Plane(Vector3.up, GridOrigin);
            if (ground.Raycast(ray, out float enter))
            {
                Vector3 hit = ray.GetPoint(enter);
                _hoverCell = WorldToCell(hit);
            }
            else
            {
                _hoverCell = null;
            }
        }

        // -------------------------------------------------------------------------
        // YERLEŞTİRME
        // -------------------------------------------------------------------------
        public bool TryPlaceAt(Vector2Int cell)
        {
            if (!IsBuildPhase) return false;
            if (!IsCellInBounds(cell)) return false;
            if (_items.ContainsKey(cell)) return false;
            if (catalog.Count == 0) return false;
            if (_selectedIndex < 0 || _selectedIndex >= catalog.Count) return false;

            var data = catalog[_selectedIndex];
            if (data == null) return false;
            if (Currency < data.cost)
            {
                if (DebugOverlay.Instance != null)
                    DebugOverlay.Instance.Log(LogCategory.Build, "Yetersiz para.", false);
                return false;
            }

            GameObject prefab = data.prefab;
            Vector3 pos = CellToWorld(cell);
            GameObject go;
            if (prefab != null)
                go = Instantiate(prefab, pos, Quaternion.identity);
            else
            {
                // Geçici görsel: küp
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.position = pos + Vector3.up * 0.5f;
                go.transform.localScale = new Vector3(cellSize * 0.9f, 1f, cellSize * 0.9f);
            }
            go.name = $"BuildItem_{data.displayName}_{cell.x}_{cell.y}";

            var item = go.GetComponent<BuildItem>();
            if (item == null) item = go.AddComponent<BuildItem>();
            item.data = data;

            _items[cell] = item;
            Currency -= data.cost;
            UpdateHud();

            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Build,
                    $"Yerleştirildi: {data.displayName} @ {cell} (-{data.cost} para)", false);
            return true;
        }

        // -------------------------------------------------------------------------
        // ETKİLEŞİM
        // -------------------------------------------------------------------------
        public void TryInteractAt(Vector2Int? cellNullable)
        {
            if (!cellNullable.HasValue) return;
            var cell = cellNullable.Value;
            if (_items.TryGetValue(cell, out var item))
            {
                _selectedItem = item;
                if (DebugOverlay.Instance != null)
                    DebugOverlay.Instance.Log(LogCategory.Build,
                        $"Seçildi: {item.name} (HP {item.CurrentHealth:F0}/{item.MaxHealth:F0})", false);
            }
            else
            {
                _selectedItem = null;
            }
            UpdateHud();
        }

        public bool TryRepair(BuildItem item)
        {
            if (item == null || item.IsBroken) return false;
            float missing = item.MaxHealth - item.CurrentHealth;
            if (missing <= 0f) return false;

            float cost = missing * item.RepairCostPerHp;
            int costI = Mathf.CeilToInt(cost);
            if (Currency < costI)
            {
                if (DebugOverlay.Instance != null)
                    DebugOverlay.Instance.Log(LogCategory.Build, "Tamir için yetersiz para.", false);
                return false;
            }
            Currency -= costI;
            item.Repair(missing);
            UpdateHud();
            return true;
        }

        public void RemoveItem(BuildItem item)
        {
            foreach (var kv in _items)
            {
                if (kv.Value == item)
                {
                    _items.Remove(kv.Key);
                    break;
                }
            }
            if (item != null) Destroy(item.gameObject);
            if (_selectedItem == item) _selectedItem = null;
        }

        // -------------------------------------------------------------------------
        // YOL GÖSTERGESİ
        // -------------------------------------------------------------------------
        private void DrawEnemyPath()
        {
            // Spawn noktalarından yatağa doğru örnek bir yol çiz. Bu sadece
            // görsel rehber; düşmanlar NavMesh/transform ile gider.
            if (GameManager.Instance == null) return;
            var gmsp = GameManager.Instance.enemySpawnPoints;
            if (gmsp == null || gmsp.Length == 0) return;

            // LineRenderer yoksa oluştur
            if (pathLine == null)
            {
                var go = new GameObject("EnemyPathLine");
                go.transform.SetParent(transform);
                pathLine = go.AddComponent<LineRenderer>();
                pathLine.material = new Material(Shader.Find("Sprites/Default"));
            }
            pathLine.startColor = pathColor;
            pathLine.endColor = pathColor;
            pathLine.startWidth = pathWidth;
            pathLine.endWidth = pathWidth;
            pathLine.positionCount = pathSegments + 1;

            Vector3 bedPos = GameManager.Instance.bed != null
                ? GameManager.Instance.bed.transform.position
                : GridOrigin;

            for (int i = 0; i <= pathSegments; i++)
            {
                float t = i / (float)pathSegments;
                Transform sp = gmsp[Mathf.Min(gmsp.Length - 1, Mathf.FloorToInt(t * gmsp.Length))];
                Vector3 a = sp != null ? sp.position : bedPos;
                Vector3 b = bedPos;
                // Tırtıklı (zigzag) interpolasyon: anahtar noktalara küçük ofsetler
                Vector3 mid = Vector3.Lerp(a, b, t);
                float wave = Mathf.Sin(t * Mathf.PI * 6f) * 0.4f;
                Vector3 perp = Vector3.Cross((b - a).normalized, Vector3.up) * wave;
                pathLine.SetPosition(i, mid + perp + Vector3.up * 0.05f);
            }
        }

        // -------------------------------------------------------------------------
        // HUD
        // -------------------------------------------------------------------------
        private void UpdateHud()
        {
            if (DebugOverlay.Instance == null) return;
            DebugOverlay.Instance.SetHudValue("Para", Currency.ToString());
            if (_selectedItem != null)
            {
                DebugOverlay.Instance.SetHudValue("Seçili Eşya",
                    $"{_selectedItem.name} HP {_selectedItem.CurrentHealth:F0}/{_selectedItem.MaxHealth:F0}");
            }
            else
            {
                DebugOverlay.Instance.RemoveHudValue("Seçili Eşya");
            }
        }

        // -------------------------------------------------------------------------
        // DEBUG ÇİZİM
        // -------------------------------------------------------------------------
        private void OnDrawGizmos()
        {
            // Grid'i sahnede göster
            Vector3 origin = gridCenter != null ? gridCenter.position : Vector3.zero;
            Gizmos.color = new Color(1, 1, 1, 0.15f);
            int r = gridRadiusCells;
            for (int x = -r; x <= r; x++)
            {
                Vector3 a = origin + new Vector3(x * cellSize, 0, -r * cellSize);
                Vector3 b = origin + new Vector3(x * cellSize, 0, r * cellSize);
                Gizmos.DrawLine(a, b);
            }
            for (int z = -r; z <= r; z++)
            {
                Vector3 a = origin + new Vector3(-r * cellSize, 0, z * cellSize);
                Vector3 b = origin + new Vector3(r * cellSize, 0, z * cellSize);
                Gizmos.DrawLine(a, b);
            }

            // Hover hücresini vurgula
            if (_hoverCell.HasValue)
            {
                bool canPlace = IsBuildPhase && IsCellInBounds(_hoverCell.Value) &&
                                !_items.ContainsKey(_hoverCell.Value);
                Gizmos.color = canPlace ? Color.green : Color.red;
                Vector3 p = CellToWorld(_hoverCell.Value);
                Gizmos.DrawWireCube(p + Vector3.up * 0.05f, new Vector3(cellSize, 0.1f, cellSize));
            }
        }
    }
}
