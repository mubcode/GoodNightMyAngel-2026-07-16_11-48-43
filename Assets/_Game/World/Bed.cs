// =============================================================================
// Bed.cs
// -----------------------------------------------------------------------------
// Oyundaki "yatak" nesnesi. Hem gündüz evindeki gerçek yatak hem de rüya
// sahnesinde uyuyan bedenin temsilidir.
//
// Sorumlulukları:
//   1) Yatak canını tutmak ve Inspector'dan ayarlanabilir kılmak.
//   2) Düşmanlar yatağa ulaştığında hasar almak.
//   3) Can sıfıra düştüğünde GameManager.GameOver() çağırmak.
//   4) Görsel geri bildirim: kırmızıya boyanma, titreme vb.
// =============================================================================

using UnityEngine;
using GoodNightMyAngel.Core;

namespace GoodNightMyAngel.World
{
    /// <summary>
    /// Oyundaki ana yatak. Sahnede bir GameObject'e eklenir. Düşmanlar
    /// belirli bir mesafeye geldiğinde bu objeye hasar verir.
    /// </summary>
    [DisallowMultipleComponent]
    public class Bed : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // INSPECTOR
        // -------------------------------------------------------------------------
        [Header("Can")]
        [Tooltip("Yatağın başlangıç ve maksimum canı.")]
        [Min(1f)] public float maxHealth = 100f;

        [Tooltip("Şu anki can. Inspector'da da görünür, debug için.")]
        [SerializeField] private float _currentHealth = 100f;

        [Header("Hasar Ayarları")]
        [Tooltip("Düşman yatağa kaç saniyede bir vuruyor (saniyede hasar).")]
        [Min(0.1f)] public float damageInterval = 0.5f;

        [Tooltip("Düşmanın yatağa vurabilmesi için gereken mesafe.")]
        [Min(0.1f)] public float attackRadius = 1.5f;

        [Tooltip("Bir vuruşta ne kadar hasar.")]
        [Min(1f)] public float damagePerHit = 5f;

        [Header("Görsel Geri Bildirim")]
        [Tooltip("Yatak mesh'inin Renderer'ı. Varsa hasar alınca kırmızıya boyarız.")]
        public Renderer bedRenderer;

        [Tooltip("Hasar alındığında varsayılan rengin ne kadarına karışacağı (0-1). " +
                 "0 = renk değişmez, 1 = tamamen kırmızı olur.")]
        [Range(0f, 1f)] public float damageFlashAmount = 0.6f;

        [Tooltip("Renk geri sönme hızı.")]
        [Min(0.1f)] public float colorFadeSpeed = 3f;

        [Tooltip("Hasar alındığında titreme (punch) şiddeti.")]
        public float shakeAmount = 0.05f;

        [Header("Ölüm")]
        [Tooltip("Yatak yok olduğunda (canı 0 olunca) çocuk için ölüm efekti.")]
        public GameObject deathVfxPrefab;

        // -------------------------------------------------------------------------
        // DURUM
        // -------------------------------------------------------------------------
        public float CurrentHealth => _currentHealth;
        public bool IsDead => _currentHealth <= 0f;
        public float HealthPercent => Mathf.Clamp01(_currentHealth / maxHealth);

        private float _damageTimer = 0f;
        private Color _baseColor = Color.white;
        private Vector3 _baseLocalPos;
        private float _shakeTimer = 0f;

        // -------------------------------------------------------------------------
        // YAŞAM DÖNGÜSÜ
        // -------------------------------------------------------------------------
        private void Awake()
        {
            _currentHealth = maxHealth;
            if (bedRenderer == null) bedRenderer = GetComponentInChildren<Renderer>();
            if (bedRenderer != null)
            {
                // MaterialPropertyBlock kullanıyoruz ki material instance leak olmasın.
                var mpb = new MaterialPropertyBlock();
                bedRenderer.GetPropertyBlock(mpb);
                _baseColor = bedRenderer.sharedMaterial != null
                    ? bedRenderer.sharedMaterial.color
                    : Color.white;
            }
            _baseLocalPos = transform.localPosition;
        }

        private void Start()
        {
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.SetHudValue("Yatak Canı", $"{_currentHealth:F0}/{maxHealth:F0}");
        }

        private void Update()
        {
            if (IsDead) return;

            // Gece sırasında yakınımızdaki düşmanlardan hasar al
            if (GameManager.Instance != null &&
                (GameManager.Instance.TimeOfDay == TimeOfDay.NightDefense ||
                 GameManager.Instance.TimeOfDay == TimeOfDay.NightBoss))
            {
                _damageTimer -= Time.deltaTime;
                if (_damageTimer <= 0f)
                {
                    // En yakın düşmanı bul
                    var nearest = FindNearestEnemyInRange();
                    if (nearest != null)
                    {
                        TakeDamage(damagePerHit, nearest);
                        _damageTimer = damageInterval;
                    }
                }
            }

            // Hasar titremesi azalır
            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(_shakeTimer / 0.2f);
                transform.localPosition = _baseLocalPos +
                    (Vector3)(Random.insideUnitCircle * shakeAmount * t);
                if (_shakeTimer <= 0f) transform.localPosition = _baseLocalPos;
            }

            // Renk geri sönümü
            if (bedRenderer != null)
            {
                var mpb = new MaterialPropertyBlock();
                bedRenderer.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", _baseColor);
                bedRenderer.SetPropertyBlock(mpb);
            }
        }

        // -------------------------------------------------------------------------
        // HASAR
        // -------------------------------------------------------------------------

        public void TakeDamage(float amount, GameObject source = null)
        {
            if (IsDead) return;
            _currentHealth = Mathf.Max(0f, _currentHealth - amount);

            // Görsel: titreme + renk
            _shakeTimer = 0.2f;
            if (bedRenderer != null)
            {
                var mpb = new MaterialPropertyBlock();
                bedRenderer.GetPropertyBlock(mpb);
                Color dmg = Color.Lerp(_baseColor, Color.red, damageFlashAmount);
                mpb.SetColor("_BaseColor", dmg);
                bedRenderer.SetPropertyBlock(mpb);
            }

            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Combat,
                    $"Yatak hasar aldı: -{amount:F1} (kalan {_currentHealth:F1}/{maxHealth:F1})", false);
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.SetHudValue("Yatak Canı", $"{_currentHealth:F0}/{maxHealth:F0}");

            if (_currentHealth <= 0f) Die();
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            _currentHealth = Mathf.Min(maxHealth, _currentHealth + amount);
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Combat, $"Yatak iyileşti: +{amount:F1}", false);
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.SetHudValue("Yatak Canı", $"{_currentHealth:F0}/{maxHealth:F0}");
        }

        private void Die()
        {
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Combat, "YATAK YOK OLDU.", false);

            if (deathVfxPrefab != null)
                Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);

            // GameManager'a bildir
            if (GameManager.Instance != null) GameManager.Instance.GameOver();
        }

        // -------------------------------------------------------------------------
        // YARDIMCI
        // -------------------------------------------------------------------------
        private GameObject FindNearestEnemyInRange()
        {
            // Etiket veya layer ile filtrelemek daha hızlı olurdu, ama basit
            // mesafe kontrolü yeterli. Çok sayıda düşman olursa OverlapSphere
            // kullanılabilir.
            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            GameObject best = null;
            float bestDist = attackRadius;
            Vector3 p = transform.position;
            foreach (var e in enemies)
            {
                if (e == null || !e.activeInHierarchy) continue;
                float d = Vector3.Distance(e.transform.position, p);
                if (d < bestDist) { best = e; bestDist = d; }
            }
            return best;
        }
    }
}
