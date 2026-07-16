// =============================================================================
// BuildManager.cs
// -----------------------------------------------------------------------------
// Gece build phase boyunca oyuncunun savunma elemanlarını yerleştirmesini
// yöneten ana sistem.
//
// Yeni özellikler:
//   - Ghost (yarı saydam önizleme) hover sırasında gösterilir
//   - Yeşil/kırmızı zemin karesi ile hücre durumu görsel olarak anlaşılır
//   - Yol çizgisi artık gerçek düşman yolu (spawn -> bed) üzerinden çizilir
//   - Düşman yolu göstergesinin rengi/yüksekliği Inspector'dan ayarlanabilir
//
// Inspector'dan:
//   - Grid boyutu (hücre birim uzunluğu)
//   - Grid merkezi (yatak pozisyonu etrafında)
//   - Mevcut eşya kataloğu (BuildItemData listesi)
//   - Başlangıç parası
//   - Yol göstergesi (LineRenderer) için renk ve kalınlık
//   - Ghost (preview) ve zemin işareti renkleri
// ayarlanabilir.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using GoodNightMyAngel.Core;
using GoodNightMyAngel.InputBridge;

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
        [Min(0.5f)] public float cellSize = 1.2f;

        [Tooltip("Grid merkezi. Boşsa yatak pozisyonu kullanılır.")]
        public Transform gridCenter;

        [Tooltip("Grid yarıçapı (hücre cinsinden, merkezden uzaklık).")]
        [Min(1)] public int gridRadiusCells = 12;

        [Header("Katalog")]
        [Tooltip("Yerleştirilebilecek savunma elemanları. Sıra = hızlı seçim tuşu (1, 2, 3 ...).")]
        public List<BuildItemData> catalog = new List<BuildItemData>();

        [Header("Ekonomi")]
        [Tooltip("Oyuncunun başlangıç parası.")]
        [Min(0)] public int startingCurrency = 100;

        [Header("Yol Göstergesi (Düşman yolu)")]
        [Tooltip("Yaratık yolunun gösterileceği LineRenderer. Boşsa runtime oluşturulur.")]
        public LineRenderer pathLine;

        [Tooltip("Yol çizgisi rengi (kırmızımsı).")]
        public Color pathColor = new Color(1f, 0.3f, 0.3f, 0.85f);

        [Tooltip("Yol çizgisi kalınlığı.")]
        [Min(0.01f)] public float pathWidth = 0.12f;

        [Tooltip("Yol çizgisi yüksekliği (zeminden kaç birim yukarı).")]
        [Min(0f)] public float pathHeight = 0.08f;

        [Header("Hover / Yerleştirme Önizleme")]
        [Tooltip("Yeşil (yerleştirilebilir) zemin karesi rengi.")]
        public Color canPlaceColor = new Color(0.3f, 1f, 0.3f, 0.45f);

        [Tooltip("Kırmızı (yerleştirilemez) zemin karesi rengi.")]
        public Color cannotPlaceColor = new Color(1f, 0.3f, 0.3f, 0.45f);

        [Tooltip("Hover ghost (yarı saydam eşya) göstergesinin opaklığı.")]
        [Range(0f, 1f)] public float ghostOpacity = 0.45f;

        [Header("Girdi")]
        [Tooltip("Sol tık yerleştirir.")]
        public KeyCode placeKey = KeyCode.Mouse0;

        [Tooltip("Sağ tık seçer/etkileşir.")]
        public KeyCode interactKey = KeyCode.Mouse1;

        // -------------------------------------------------------------------------
        // DURUM
        // -------------------------------------------------------------------------
        public int Currency { get; private set; }

        private readonly Dictionary<Vector2Int, BuildItem> _items = new Dictionary<Vector2Int, BuildItem>();

        private int _selectedIndex = 0;
        private Camera _cam;
        private Vector2Int? _hoverCell;
        private BuildItem _selectedItem;

        // Ghost için prefab instance (placeholder, runtime'da oluşturulur)
        private GameObject _ghostObj;
        private GameObject _hoverSquareObj;       // zemine çizilen kare
        private Material _ghostMaterial;
        private Material _hoverSquareMaterial;
        private Renderer _ghostRenderer;
        private Renderer _hoverSquareRenderer;

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
            // Yolu çiz
            DrawEnemyPath();
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
            // Build phase bittiğinde preview gizle
            if (_ghostObj != null) _ghostObj.SetActive(false);
            if (_hoverSquareObj != null) _hoverSquareObj.SetActive(false);
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

            // Kısayol tuşları: 1..9 ile katalog seç
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

            // Sol tık -> yerleştir
            if (_hoverCell.HasValue && LegacyInputBridge.GetKeyDown(placeKey))
            {
                TryPlaceAt(_hoverCell.Value);
            }

            // Sağ tık -> seç / tamir / kaldır
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
        // ÖNİZLEME OBJELERİ
        // -------------------------------------------------------------------------
        private void CreatePreviewObjects()
        {
            // Ghost (eşya önizleme) — yarı saydam küp
            _ghostObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ghostObj.name = "BuildGhost";
            // Collider lazım değil
            var col = _ghostObj.GetComponent<Collider>();
            if (col != null) Destroy(col);
            _ghostObj.SetActive(false);
            _ghostRenderer = _ghostObj.GetComponent<Renderer>();
            _ghostMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _ghostMaterial.SetFloat("_Surface", 1);   // Transparent
            _ghostMaterial.SetFloat("_Blend", 0);
            _ghostMaterial.SetOverrideTag("RenderType", "Transparent");
            _ghostMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _ghostMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _ghostMaterial.SetInt("_ZWrite", 0);
            _ghostMaterial.renderQueue = 3000;
            _ghostRenderer.sharedMaterial = _ghostMaterial;

            // Zemin karesi (hover)
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
            bool inBounds = IsCellInBounds(cell);
            bool free = !_items.ContainsKey(cell);
            bool canAfford = catalog.Count > 0 && _selectedIndex < catalog.Count &&
                             Currency >= catalog[_selectedIndex].cost;
            bool canPlace = inBounds && free && canAfford;

            // Zemin karesini güncelle
            Vector3 cellPos = CellToWorld(cell);
            _hoverSquareObj.transform.position = cellPos + Vector3.up * 0.02f;
            _hoverSquareObj.transform.localScale = new Vector3(cellSize * 0.95f, cellSize * 0.95f, 1f);
            _hoverSquareMaterial.color = canPlace ? canPlaceColor : cannotPlaceColor;
            if (!_hoverSquareObj.activeSelf) _hoverSquareObj.SetActive(true);

            // Ghost
            if (catalog.Count == 0 || _selectedIndex >= catalog.Count ||
                catalog[_selectedIndex] == null || catalog[_selectedIndex].prefab == null)
            {
                if (_ghostObj.activeSelf) _ghostObj.SetActive(false);
                return;
            }
            var data = catalog[_selectedIndex];

            // Ghost rengini kategoriye göre değiştirelim:
            // Barricade -> turuncu, Trap -> kırmızı, Turret -> mavi, Slow -> cyan, Special -> mor
            Color ghostCol = data.category switch
            {
                BuildItemCategory.Barricade => new Color(1f, 0.6f, 0.2f, ghostOpacity),
                BuildItemCategory.Trap => new Color(1f, 0.3f, 0.3f, ghostOpacity),
                BuildItemCategory.Turret => new Color(0.3f, 0.7f, 1f, ghostOpacity),
                BuildItemCategory.Slow => new Color(0.3f, 1f, 1f, ghostOpacity),
                BuildItemCategory.Special => new Color(0.8f, 0.4f, 1f, ghostOpacity),
                _ => new Color(1f, 1f, 1f, ghostOpacity)
            };
            if (!canPlace) ghostCol = new Color(0.5f, 0.2f, 0.2f, ghostOpacity);

            _ghostObj.transform.position = cellPos + Vector3.up * 0.5f;
            _ghostObj.transform.localScale = new Vector3(cellSize * 0.85f, 1f, cellSize * 0.85f);
            _ghostMaterial.color = ghostCol;
            if (!_ghostObj.activeSelf) _ghostObj.SetActive(true);
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
        // Spawn noktalarından yatağa doğru **her spawn noktası için ayrı bir çizgi**
        // çizer. Çizgi yüksekliği pathHeight kadardır ve yere yatmaz; böylece
        // düşman yolu net olarak görünür. Çizgi ayrıca yataktan spawn noktasına
        // doğru ok ucu gibi görünecek şekilde (renk geçişiyle) çizilir.
        // -------------------------------------------------------------------------
        private void DrawEnemyPath()
        {
            if (GameManager.Instance == null) return;
            var gmsp = GameManager.Instance.enemySpawnPoints;
            Vector3 bedPos = GameManager.Instance.bed != null
                ? GameManager.Instance.bed.transform.position
                : GridOrigin;
            if (gmsp == null || gmsp.Length == 0) return;

            // Mevcut line'ları temizle
            if (pathLine != null) Destroy(pathLine.gameObject);

            // Her spawn noktası için bir LineRenderer oluştur
            for (int s = 0; s < gmsp.Length; s++)
            {
                var sp = gmsp[s];
                if (sp == null) continue;

                var go = new GameObject($"EnemyPath_{s}");
                go.transform.SetParent(transform);
                var lr = go.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                lr.material.color = pathColor;
                lr.startColor = pathColor;
                lr.endColor = pathColor;
                lr.startWidth = pathWidth;
                lr.endWidth = pathWidth * 1.5f;     // Bed'e doğru kalınlaşsın (vurgu)
                lr.positionCount = 2;
                lr.useWorldSpace = true;

                Vector3 a = sp.position;
                Vector3 b = bedPos;
                // Zeminin hemen üstünde
                a.y = pathHeight;
                b.y = pathHeight;
                lr.SetPosition(0, a);
                lr.SetPosition(1, b);
                lr.numCapVertices = 4;             // Uçlarda yumuşak
            }

            // Sahnede sadece ilk line'ı referans al (Inspector için)
            var first = transform.Find("EnemyPath_0");
            if (first != null) pathLine = first.GetComponent<LineRenderer>();
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
        }
    }
}
