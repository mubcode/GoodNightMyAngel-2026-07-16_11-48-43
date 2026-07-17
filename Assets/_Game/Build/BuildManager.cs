// =============================================================================
// BuildManager.cs
// -----------------------------------------------------------------------------
// Gece build phase boyunca oyuncunun savunma elemanlarını yerleştirmesini
// yöneten ana sistem.
//
// YERLEŞTIRME KURALLARI (Inspector'dan ayarlanabilir):
//   - Yatak hücrelerine ve çevresine yerleştirilemez (bedKeepOutCells)
//   - Yatak etrafı tamamen sarılamaz (her zaman en az 1 boş hücre kalmalı)
//   - Bu "son geçerli" hücre build phase boyunca "kilitli" gösterilir
//   - Eğer oyuncu yatak etrafındaki tüm hücreleri kapatmaya çalışırsa
//     son hücre otomatik olarak kırmızı (kilitli) gösterilir
//
// Yeni özellikler:
//   - Ghost (yarı saydam önizleme) hover sırasında gösterilir
//   - Yeşil/kırmızı zemin karesi ile hücre durumu görsel olarak anlaşılır
//   - Yol çizgisi artık gerçek düşman yolu (spawn -> bed) üzerinden çizilir
//
// Inspector'dan:
//   - Grid boyutu, yarıçapı
//   - Katalog
//   - Başlangıç parası
//   - Yatak keep-out hücre yarıçapı
//   - Yol göstergesi, ghost renkleri
// ayarlanabilir.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using GoodNightMyAngel.Core;
using GoodNightMyAngel.InputBridge;

namespace GoodNightMyAngel.Build
{
    /// <summary>
    /// Build phase ana yöneticisi. Grid, yerleştirme, tamir, yol göstergesi
    /// ve yatak koruma kurallarını yönetir.
    /// </summary>
    public class BuildManager : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // INSPECTOR
        // -------------------------------------------------------------------------
        [Header("Grid")]
        [Tooltip("Bir hücrenin dünya uzayındaki birim uzunluğu.")]
        [Min(0.5f)] public float cellSize = 1.2f;

        [Tooltip("Grid merkezi. Boşsa yatak pozisyonu kullanılır.")]
        public Transform gridCenter;

        [Tooltip("Grid yarıçapı (hücre cinsinden, merkezden uzaklık).")]
        [Min(1)] public int gridRadiusCells = 12;

        [Header("Yatak Koruma Kuralları")]
        [Tooltip("Yatağın etrafında keep-out hücre yarıçapı. Yatak dahil 1.5 = yatak + 1 hücre boşluk.")]
        [Min(0f)] public float bedKeepOutCells = 1.5f;

        [Tooltip("Yatak etrafındaki son geçerli hücre otomatik kilitlensin mi?")]
        public bool lockFinalApproachCell = true;

        [Header("Katalog")]
        public List<BuildItemData> catalog = new List<BuildItemData>();

        [Header("Ekonomi")]
        [Min(0)] public int startingCurrency = 100;

        [Header("Hover / Yerleştirme Önizleme")]
        public Color canPlaceColor = new Color(0.3f, 1f, 0.3f, 0.45f);
        public Color cannotPlaceColor = new Color(1f, 0.3f, 0.3f, 0.45f);
        public Color lockedCellColor = new Color(0.6f, 0.1f, 0.1f, 0.7f);   // son geçerli hücre kırmızı/parlak
        [Range(0f, 1f)] public float ghostOpacity = 0.45f;

        [Header("Girdi")]
        public KeyCode placeKey = KeyCode.Mouse0;
        public KeyCode interactKey = KeyCode.Mouse1;

        // -------------------------------------------------------------------------
        // DURUM
        // -------------------------------------------------------------------------
        public int Currency { get; private set; }

        public void AddCurrency(int amount)
        {
            Currency = Mathf.Max(0, Currency + amount);
            UpdateHud();
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Build,
                    $"Para: {Currency} (+{amount})", false);
        }

        private readonly Dictionary<Vector2Int, BuildItem> _items = new Dictionary<Vector2Int, BuildItem>();

        private int _selectedIndex = -1;   // -1 = inşa modunda değil
        private Camera _cam;
        private Vector2Int? _hoverCell;
        private BuildItem _selectedItem;

        // Ghost için
        private GameObject _ghostObj;
        private GameObject _hoverSquareObj;
        private Material _ghostMaterial;
        private Material _hoverSquareMaterial;
        private Renderer _ghostRenderer;
        private Renderer _hoverSquareRenderer;

        // Yatak koruması
        private Vector2Int _bedCell = Vector2Int.zero;
        private HashSet<Vector2Int> _keepOutCells = new HashSet<Vector2Int>();
        private Vector2Int? _lastValidCell;          // yatağa ulaşmak için son geçerli boş hücre

        public bool IsBuildPhase => GameManager.Instance != null &&
            GameManager.Instance.TimeOfDay == TimeOfDay.NightBuild;

        // -------------------------------------------------------------------------
        // YAŞAM DÖNGÜSÜ
        // -------------------------------------------------------------------------
        private void Awake()
        {
            _cam = Camera.main;
            Currency = startingCurrency;
            CreatePreviewObjects();
        }

        private void Start()
        {
            BuildKeepOut();
            UpdateHud();
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Build,
                    $"BuildManager hazır. Para: {Currency}, Katalog: {catalog.Count} eşya.", false);
        }

        private void OnDestroy()
        {
            if (_ghostObj != null) Destroy(_ghostObj);
            if (_hoverSquareObj != null) Destroy(_hoverSquareObj);
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
            if (_ghostObj != null) _ghostObj.SetActive(false);
            if (_hoverSquareObj != null) _hoverSquareObj.SetActive(false);
        }

        private void HandleBuildStarted(float t)
        {
            _selectedItem = null;
            RecomputeLastValidCell();
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Build,
                    $"Build phase başladı. {t:F0}s süren var.", false);
        }

        private void HandleBuildEnded()
        {
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Build, "Build phase bitti.", false);
            if (_ghostObj != null) _ghostObj.SetActive(false);
            if (_hoverSquareObj != null) _hoverSquareObj.SetActive(false);
        }

        private void Update()
        {
            if (!IsBuildPhase)
            {
                if (_ghostObj != null && _ghostObj.activeSelf) _ghostObj.SetActive(false);
                if (_hoverSquareObj != null && _hoverSquareObj.activeSelf) _hoverSquareObj.SetActive(false);
                return;
            }

            // PAUSE sırasında ghost'u yerinde dondur (mouse takip etmesin)
            if (GameManager.Instance != null && GameManager.Instance.Status == GameStatus.Paused)
            {
                return;
            }

            for (int i = 0; i < 9; i++)
            {
                if (LegacyInputBridge.GetKeyDown(KeyCode.Alpha1 + i) && i < catalog.Count)
                {
                    _selectedIndex = i;
                    if (DebugOverlay.Instance != null)
                        DebugOverlay.Instance.Log(LogCategory.Build,
                            $"Seçili eşya: {catalog[i].displayName} (maliyet {catalog[i].cost})", false);
                }
            }

            UpdateHoverCell();
            UpdateHoverPreview();

            // Sol tık -> sadece inşa modundayken (_selectedIndex >= 0) yerleştir
            // Aksi halde sol tık oyuncu ateşi için serbest
            if (_hoverCell.HasValue && _selectedIndex >= 0 && LegacyInputBridge.GetKeyDown(placeKey))
            {
                TryPlaceAt(_hoverCell.Value);
                RecomputeLastValidCell();
            }

            // Sağ tık -> etkileşim (eşya seç / inşa modundan çık)
            if (LegacyInputBridge.GetKeyDown(interactKey))
            {
                TryInteractAt(_hoverCell);
            }

            // R tuşu: seçili eşyayı tamir et
            if (LegacyInputBridge.GetKeyDown(KeyCode.R) && _selectedItem != null)
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

            Ray ray = _cam.ScreenPointToRay(LegacyInputBridge.mousePosition);
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
        // YATAK KORUMA KURALLARI
        // -------------------------------------------------------------------------
        private void BuildKeepOut()
        {
            _keepOutCells.Clear();
            // Yatak pozisyonunu grid koordinatına çevir
            Vector3 bedPos = GameManager.Instance != null && GameManager.Instance.bed != null
                ? GameManager.Instance.bed.transform.position
                : GridOrigin;
            _bedCell = WorldToCell(bedPos);

            // Yatak etrafında bedKeepOutCells yarıçapında hücreleri "no build" yap
            int r = Mathf.CeilToInt(bedKeepOutCells);
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d <= bedKeepOutCells)
                    {
                        _keepOutCells.Add(new Vector2Int(_bedCell.x + dx, _bedCell.y + dz));
                    }
                }
            }
            // Yatak hücresinin kendisi de keep-out'ta
            _keepOutCells.Add(_bedCell);
        }

        /// <summary>
        /// Yatağa ulaşmak için kalan "son geçerli" hücreyi bul.
        /// Yatak hücrelerine komşu olan boş hücrelerden bir tanesini
        /// "kritik hücre" olarak seç. Eğer o hücre de kapatılırsa
        /// yatak tamamen erişilemez olur (oyun hata durumu).
        /// </summary>
        private void RecomputeLastValidCell()
        {
            _lastValidCell = null;
            // Yatak hücresine komşu olan 4 hücreyi kontrol et
            Vector2Int[] neighbors = new Vector2Int[]
            {
                new Vector2Int(_bedCell.x + 1, _bedCell.y),
                new Vector2Int(_bedCell.x - 1, _bedCell.y),
                new Vector2Int(_bedCell.x, _bedCell.y + 1),
                new Vector2Int(_bedCell.x, _bedCell.y - 1),
            };
            // İlk boş (eşya konulmamış) komşuyu seç
            foreach (var n in neighbors)
            {
                if (!_items.ContainsKey(n) && IsCellInBounds(n))
                {
                    _lastValidCell = n;
                    break;
                }
            }
        }

        // -------------------------------------------------------------------------
        // ÖNİZLEME OBJELERİ
        // -------------------------------------------------------------------------
        private void CreatePreviewObjects()
        {
            _ghostObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ghostObj.name = "BuildGhost";
            var col = _ghostObj.GetComponent<Collider>();
            if (col != null) Destroy(col);
            _ghostObj.SetActive(false);
            var gr = _ghostObj.GetComponent<Renderer>();
            _ghostMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _ghostMaterial.SetFloat("_Surface", 1);
            _ghostMaterial.SetFloat("_Blend", 0);
            _ghostMaterial.SetOverrideTag("RenderType", "Transparent");
            _ghostMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _ghostMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _ghostMaterial.SetInt("_ZWrite", 0);
            _ghostMaterial.renderQueue = 3000;
            gr.sharedMaterial = _ghostMaterial;

            _hoverSquareObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _hoverSquareObj.name = "HoverSquare";
            var col2 = _hoverSquareObj.GetComponent<Collider>();
            if (col2 != null) Destroy(col2);
            _hoverSquareObj.transform.rotation = Quaternion.Euler(90, 0, 0);
            _hoverSquareObj.SetActive(false);
            _hoverSquareRenderer = _hoverSquareObj.GetComponent<Renderer>();
            _hoverSquareMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _hoverSquareMaterial.SetFloat("_Surface", 1);
            _hoverSquareMaterial.SetFloat("_Blend", 0);
            _hoverSquareMaterial.SetOverrideTag("RenderType", "Transparent");
            _hoverSquareMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _hoverSquareMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _hoverSquareMaterial.SetInt("_ZWrite", 0);
            _hoverSquareMaterial.renderQueue = 3000;
            _hoverSquareRenderer.sharedMaterial = _hoverSquareMaterial;
        }

        private void UpdateHoverPreview()
        {
            if (!_hoverCell.HasValue)
            {
                if (_ghostObj.activeSelf) _ghostObj.SetActive(false);
                if (_hoverSquareObj.activeSelf) _hoverSquareObj.SetActive(false);
                return;
            }

            Vector2Int cell = _hoverCell.Value;
            bool canPlace = CanPlaceAt(cell);
            bool isKeepOut = _keepOutCells.Contains(cell);
            bool isLastValid = lockFinalApproachCell && _lastValidCell.HasValue &&
                               _lastValidCell.Value == cell;

            // Zemin karesi
            Vector3 cellPos = CellToWorld(cell);
            _hoverSquareObj.transform.position = cellPos + Vector3.up * 0.02f;
            _hoverSquareObj.transform.localScale = new Vector3(cellSize * 0.95f, cellSize * 0.95f, 1f);

            // Renk seçimi: locked -> kırmızı/parlak, canPlace -> yeşil, !canPlace -> kırmızı
            Color sqColor;
            if (isLastValid) sqColor = lockedCellColor;
            else if (canPlace) sqColor = canPlaceColor;
            else sqColor = cannotPlaceColor;
            _hoverSquareMaterial.color = sqColor;
            if (!_hoverSquareObj.activeSelf) _hoverSquareObj.SetActive(true);

            // Ghost
            if (!canPlace || catalog.Count == 0 || _selectedIndex < 0 || _selectedIndex >= catalog.Count ||
                catalog[_selectedIndex] == null)
            {
                if (_ghostObj.activeSelf) _ghostObj.SetActive(false);
                return;
            }
            var data = catalog[_selectedIndex];
            Color ghostCol = data.category switch
            {
                BuildItemCategory.Barricade => new Color(1f, 0.6f, 0.2f, ghostOpacity),
                BuildItemCategory.Trap => new Color(1f, 0.3f, 0.3f, ghostOpacity),
                BuildItemCategory.Turret => new Color(0.3f, 0.7f, 1f, ghostOpacity),
                BuildItemCategory.Slow => new Color(0.3f, 1f, 1f, ghostOpacity),
                BuildItemCategory.Special => new Color(0.8f, 0.4f, 1f, ghostOpacity),
                _ => new Color(1f, 1f, 1f, ghostOpacity)
            };
            if (isLastValid) ghostCol = new Color(0.8f, 0.1f, 0.1f, ghostOpacity);

            _ghostObj.transform.position = cellPos + Vector3.up * 0.5f;
            _ghostObj.transform.localScale = new Vector3(cellSize * 0.85f, 1f, cellSize * 0.85f);
            _ghostMaterial.color = ghostCol;
            if (!_ghostObj.activeSelf) _ghostObj.SetActive(true);
        }

        // -------------------------------------------------------------------------
        // YERLEŞTİRME
        // -------------------------------------------------------------------------
        public bool CanPlaceAt(Vector2Int cell)
        {
            if (!IsBuildPhase) return false;
            if (!IsCellInBounds(cell)) return false;
            if (_items.ContainsKey(cell)) return false;
            if (_keepOutCells.Contains(cell)) return false;     // yatak koruma alanı
            // Son geçerli hücre koruması
            if (lockFinalApproachCell && _lastValidCell.HasValue && _lastValidCell.Value == cell)
                return false;
            if (catalog.Count == 0 || _selectedIndex < 0 || _selectedIndex >= catalog.Count) return false;
            var data = catalog[_selectedIndex];
            if (data == null) return false;
            if (Currency < data.cost) return false;
            return true;
        }

        public bool TryPlaceAt(Vector2Int cell)
        {
            if (!CanPlaceAt(cell)) return false;
            var data = catalog[_selectedIndex];

            GameObject prefab = data.prefab;
            Vector3 pos = CellToWorld(cell);
            GameObject go;
            if (prefab != null)
                go = Instantiate(prefab, pos, Quaternion.identity);
            else
            {
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
                // Bir eşya seçildi
                _selectedItem = item;
                if (DebugOverlay.Instance != null)
                    DebugOverlay.Instance.Log(LogCategory.Build,
                        $"Seçildi: {item.name} (HP {item.CurrentHealth:F0}/{item.MaxHealth:F0})", false);
            }
            else
            {
                _selectedItem = null;
            }

            // Sağ tık: inşa modundan çık (katalog seçimini temizle) + ghost gizle
            if (_selectedIndex >= 0)
            {
                _selectedIndex = -1;
                if (_ghostObj != null) _ghostObj.SetActive(false);
                if (_hoverSquareObj != null) _hoverSquareObj.SetActive(false);
                if (DebugOverlay.Instance != null)
                    DebugOverlay.Instance.Log(LogCategory.Build, "İnşa modundan çıkıldı.", false);
            }

            UpdateHud();
        }

        public void ExitBuildMode()
        {
            _selectedIndex = -1;
            _selectedItem = null;
            if (_ghostObj != null) _ghostObj.SetActive(false);
            if (_hoverSquareObj != null) _hoverSquareObj.SetActive(false);
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
            RecomputeLastValidCell();
        }

        // -------------------------------------------------------------------------
        // YOL GÖSTERGESİ KALDIRILDI
        // -------------------------------------------------------------------------
        // Yol çizimi sadece PathManager tarafından yapılır. Burada
        // kırmızı çizgi / __BuildPathLines oluşturulmaz.

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

            // Yatak keep-out
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.3f);
            foreach (var c in _keepOutCells)
            {
                Vector3 p = CellToWorld(c);
                Gizmos.DrawWireCube(p + Vector3.up * 0.05f, new Vector3(cellSize, 0.1f, cellSize));
            }

            // Son geçerli hücre
            if (_lastValidCell.HasValue)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.7f);
                Vector3 p = CellToWorld(_lastValidCell.Value);
                Gizmos.DrawWireCube(p + Vector3.up * 0.06f, new Vector3(cellSize * 0.95f, 0.12f, cellSize * 0.95f));
            }
        }
    }
}
