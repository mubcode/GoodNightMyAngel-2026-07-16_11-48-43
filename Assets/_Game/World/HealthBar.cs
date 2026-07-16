// =============================================================================
// HealthBar.cs
// -----------------------------------------------------------------------------
// World-space can barı. Herhangi bir GameObject'in üstünde 2D bir bar olarak
// görünür. Kullanımı:
//   - GetComponent<HealthBar>() veya AddComponent<HealthBar>() ile ekle.
//   - Bind(healthSource) ile bir can kaynağına bağla (PlayerHealth, Bed vb.)
//   - Veya manuel Set(t) ile dışarıdan yüzde (0-1) ver.
//
// Inspector'dan:
//   - Bar boyutu (width, height)
//   - Bar rengi (ön plan, arka plan)
//   - Yükseklik ofseti (objenin ne kadar üstünde)
//   - Kamera bakma (her zaman kameraya dönsün)
//   - Sadece hasar alındığında göster (psx tarzı)
// ayarlanabilir.
// =============================================================================

using System;
using UnityEngine;
using GoodNightMyAngel.Core;

namespace GoodNightMyAngel.World
{
    /// <summary>
    /// World-space can barı. Sahneye eklenen bir billboard quad olarak çizilir.
    /// </summary>
    public class HealthBar : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // INSPECTOR
        // -------------------------------------------------------------------------
        [Header("Bağlama")]
        [Tooltip("Can kaynağı (PlayerHealth, Bed vb.). Inspector'dan atanabilir veya kod ile Bind() yapılabilir.")]
        public MonoBehaviour healthSource;          // generic olarak tutulur

        [Tooltip("Otomatik olarak healthSource'dan okuma yapılsın mı? (Start'ta tip çözülür)")]
        public bool autoBind = true;

        [Header("Görsel")]
        [Tooltip("Bar dünya biriminde genişlik.")]
        [Min(0.1f)] public float width = 1.2f;

        [Tooltip("Bar dünya biriminde yükseklik.")]
        [Min(0.05f)] public float height = 0.15f;

        [Tooltip("Barın obje merkezinden ne kadar yukarıda.")]
        public float heightOffset = 1.4f;

        [Tooltip("Ön plan (dolu) renk.")]
        public Color foregroundColor = new Color(0.4f, 0.95f, 0.4f);

        [Tooltip("Arka plan (boş) renk.")]
        public Color backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.7f);

        [Tooltip("Bar her zaman kameraya baksın mı? (Billboard)")]
        public bool billboard = true;

        [Header("PSX Tarzı")]
        [Tooltip("Bar sadece tam can değilken gösterilsin mi? (Sadece hasar alınca görünür)")]
        public bool showOnlyWhenDamaged = true;

        [Tooltip("Yaralandıktan kaç saniye sonra kaybolmaya başlasın.")]
        [Min(0f)] public float hideAfter = 4f;

        [Tooltip("Bar doldurulduğunda hızlıca bir pop animasyonu (görsel feed-back).")]
        public bool showFlashOnDamage = true;

        // -------------------------------------------------------------------------
        // DURUM
        // -------------------------------------------------------------------------
        private float _currentPct = 1f;
        private float _lastDamageTime = -999f;
        private float _flashTimer = 0f;
        private GameObject _barRoot;
        private GameObject _bgQuad;
        private GameObject _fgQuad;
        private Material _bgMat;
        private Material _fgMat;

        private System.Func<float> _getPercentFn;     // can yüzdesi sorgu fonksiyonu
        private System.Action<float> _setOnDamageFn;  // hasar flash tetikleyici

        // -------------------------------------------------------------------------
        // YAŞAM DÖNGÜSÜ
        // -------------------------------------------------------------------------
        private void Awake()
        {
            CreateVisuals();
            if (autoBind) TryBindFromInspector();
        }

        private void Start()
        {
            if (_getPercentFn != null) _currentPct = Mathf.Clamp01(_getPercentFn());
        }

        private void Update()
        {
            if (_getPercentFn != null)
            {
                float newPct = Mathf.Clamp01(_getPercentFn());
                if (newPct < _currentPct) _lastDamageTime = Time.time;
                _currentPct = newPct;
            }

            // Pozisyon
            if (_barRoot != null)
            {
                _barRoot.transform.position = transform.position + Vector3.up * heightOffset;
                if (billboard && Camera.main != null)
                    _barRoot.transform.rotation = Quaternion.LookRotation(
                        _barRoot.transform.position - Camera.main.transform.position);
            }

            // Foreground quad'ı oranla
            if (_fgQuad != null)
            {
                _fgQuad.transform.localScale = new Vector3(width * _currentPct, height, 1f);
                // Sol kenardan büyüsün
                _fgQuad.transform.localPosition = new Vector3(-width * 0.5f + (width * _currentPct) * 0.5f, 0, 0);
            }

            // Renk (can yüzdesine göre: yeşil -> sarı -> kırmızı)
            if (_fgMat != null)
            {
                Color c = _currentPct > 0.6f ? foregroundColor :
                          _currentPct > 0.3f ? Color.Lerp(Color.yellow, foregroundColor, 0.5f) :
                                               Color.red;
                if (_flashTimer > 0f)
                {
                    c = Color.Lerp(c, Color.white, _flashTimer * 2f);
                    _flashTimer -= Time.deltaTime * 4f;
                }
                _fgMat.color = c;
            }

            // Görünürlük
            bool shouldShow = true;
            if (showOnlyWhenDamaged && _currentPct >= 0.999f)
                shouldShow = false;
            if (Time.time - _lastDamageTime > hideAfter)
                shouldShow = false;

            if (_barRoot != null && _barRoot.activeSelf != shouldShow)
                _barRoot.SetActive(shouldShow);
        }

        // -------------------------------------------------------------------------
        // BAĞLAMA
        // -------------------------------------------------------------------------
        private void TryBindFromInspector()
        {
            if (healthSource == null) return;

            // PlayerHealth ise
            if (healthSource is Player.PlayerHealth ph)
            {
                BindFromPlayerHealth(ph);
                return;
            }
            // Bed ise
            if (healthSource is World.Bed bed)
            {
                BindFromBed(bed);
                return;
            }
        }

        public void BindFromPlayerHealth(Player.PlayerHealth ph)
        {
            _getPercentFn = () => ph.maxHealth > 0 ? ph.CurrentHealth / ph.maxHealth : 0f;
            ph.OnHealthChanged.AddListener(_ => _lastDamageTime = Time.time);
            healthSource = ph;
        }

        public void BindFromBed(World.Bed bed)
        {
            _getPercentFn = () => bed.HealthPercent;
            healthSource = bed;
        }

        public void BindFromBuildItem(Build.BuildItem item)
        {
            _getPercentFn = () => item.MaxHealth > 0 ? item.CurrentHealth / item.MaxHealth : 0f;
            healthSource = item;
            // Barikatlar için biraz farklı görsel
            if (item.Category == Build.BuildItemCategory.Barricade)
            {
                foregroundColor = new Color(0.4f, 0.85f, 0.4f);
                width = 1.0f;
                heightOffset = 1.6f;
            }
        }

        public void Bind(Func<float> percentFn)
        {
            _getPercentFn = percentFn;
        }

        public void Flash(float duration = 0.2f)
        {
            _flashTimer = duration;
        }

        // -------------------------------------------------------------------------
        // GÖRSEL OLUŞTURMA
        // -------------------------------------------------------------------------
        private void CreateVisuals()
        {
            _barRoot = new GameObject("HealthBarRoot");
            _barRoot.transform.SetParent(transform, false);

            // Arka plan
            _bgQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _bgQuad.name = "BG";
            var bgCol = _bgQuad.GetComponent<Collider>();
            if (bgCol != null) Destroy(bgCol);
            _bgQuad.transform.SetParent(_barRoot.transform, false);
            _bgQuad.transform.localScale = new Vector3(width, height, 1f);
            _bgMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _bgMat.color = backgroundColor;
            _bgQuad.GetComponent<Renderer>().sharedMaterial = _bgMat;

            // Ön plan
            _fgQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _fgQuad.name = "FG";
            var fgCol = _fgQuad.GetComponent<Collider>();
            if (fgCol != null) Destroy(fgCol);
            _fgQuad.transform.SetParent(_barRoot.transform, false);
            _fgQuad.transform.localScale = new Vector3(width, height, 1f);
            _fgMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _fgMat.color = foregroundColor;
            _fgQuad.GetComponent<Renderer>().sharedMaterial = _fgMat;
            // FG, BG'nin çok az önünde
            _fgQuad.transform.localPosition = new Vector3(0, 0, -0.01f);
        }

        private void OnDestroy()
        {
            if (_barRoot != null) Destroy(_barRoot);
        }
    }
}
