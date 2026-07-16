// =============================================================================
// BuildItem.cs
// -----------------------------------------------------------------------------
// Yerleştirilebilir bir savunma elemanı (barikat, tuzak, taret).
//
// Kategori davranışı:
//   - Barricade  : pasif blok, sadece dayanıklı (görsel: kahverengi duvar)
//   - Trap       : temaslı patlama, menzile giren düşmana hasar (görsel: kırmızı sivri)
//   - Turret     : menzilli otomatik saldırı, Projectile gönderir (görsel: mavi kule)
//   - Slow       : düşmanları yavaşlatır (görsel: cyan)
//   - Special    : özel (Call Mom vb.)
//
// Inspector'dan canı, hasarı, menzili, maliyeti ayarlanabilir.
// BuildItem'lar ScriptableObject'lerle tanımlanır (BuildItemData) ve grid
// üzerine BuildManager tarafından yerleştirilir.
// =============================================================================

using UnityEngine;
using GoodNightMyAngel.Core;
using GoodNightMyAngel.Combat;
using GoodNightMyAngel.Enemies;
using GoodNightMyAngel.World;

namespace GoodNightMyAngel.Build
{
    /// <summary>
    /// Yerleştirilebilir bir savunma elemanı. Sahneye prefab olarak
    /// instantiate edilir; canı, hasarı, menzili vardır.
    /// </summary>
    public class BuildItem : MonoBehaviour
    {
        [Header("Tanım")]
        [Tooltip("ScriptableObject tanımı. Boşsa runtime değerleri kullanılır.")]
        public BuildItemData data;

        [Header("Runtime Değerler (data yoksa)")]
        public float runtimeMaxHealth = 50f;
        public float runtimeDamage = 10f;
        public float runtimeAttackRange = 3f;
        public float runtimeAttackInterval = 1f;
        public int runtimeCost = 25;
        public float runtimeRepairCostPerHp = 0.5f;

        [Header("Görsel")]
        public GameObject destructionVfxPrefab;

        // Aktüel değerler
        public float MaxHealth { get; private set; }
        public float Damage { get; private set; }
        public float AttackRange { get; private set; }
        public float AttackInterval { get; private set; }
        public int Cost { get; private set; }
        public float RepairCostPerHp { get; private set; }
        public BuildItemCategory Category { get; private set; }

        public float CurrentHealth { get; private set; }
        public bool IsBroken => CurrentHealth <= 0f;

        private float _attackTimer = 0f;
        private float _slowTimer = 0f;
        private HealthBar _healthBar;
        private GameObject _rangeIndicator;
        private MeshRenderer _bodyRenderer;
        private Material _bodyMat;
        private Color _baseColor;

        // Slow için
        private float _slowAmount = 0.4f; // yavaşlatma yüzdesi (0.4 = %40 yavaşlatır)

        // -------------------------------------------------------------------------
        // YAŞAM DÖNGÜSÜ
        // -------------------------------------------------------------------------
        private void Awake()
        {
            if (data != null)
            {
                MaxHealth = data.maxHealth;
                Damage = data.damage;
                AttackRange = data.attackRange;
                AttackInterval = data.attackInterval;
                Cost = data.cost;
                RepairCostPerHp = data.repairCostPerHp;
                Category = data.category;
            }
            else
            {
                MaxHealth = runtimeMaxHealth;
                Damage = runtimeDamage;
                AttackRange = runtimeAttackRange;
                AttackInterval = runtimeAttackInterval;
                Cost = runtimeCost;
                RepairCostPerHp = runtimeRepairCostPerHp;
                Category = BuildItemCategory.Barricade;
            }
            CurrentHealth = MaxHealth;

            // Eğer prefabımızda renderer/material yoksa basit bir görsel oluştur
            EnsureVisual();
        }

        private void Start()
        {
            // Can barı ekle
            _healthBar = gameObject.AddComponent<World.HealthBar>();
            _healthBar.BindFromBuildItem(this);

            // Menzil göstergesi (seçildiğinde görünür)
            _rangeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _rangeIndicator.name = "RangeIndicator";
            var col = _rangeIndicator.GetComponent<Collider>();
            if (col != null) Destroy(col);
            _rangeIndicator.transform.SetParent(transform, false);
            _rangeIndicator.transform.localPosition = Vector3.zero;
            _rangeIndicator.transform.localScale = new Vector3(AttackRange * 2f, 0.02f, AttackRange * 2f);
            _rangeIndicator.SetActive(false);
            var rmat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            rmat.color = new Color(0.5f, 0.8f, 1f, 0.25f);
            _rangeIndicator.GetComponent<Renderer>().sharedMaterial = rmat;

            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Build,
                    $"Yerleştirildi: {name} ({Category}, HP={CurrentHealth:F0}, hasar={Damage:F0})", false);
        }

        private void OnDestroy()
        {
            if (_rangeIndicator != null) Destroy(_rangeIndicator);
        }

        private void Update()
        {
            if (IsBroken) return;
            if (GameManager.Instance == null) return;
            var t = GameManager.Instance.TimeOfDay;
            if (t != TimeOfDay.NightDefense && t != TimeOfDay.NightBoss) return;

            _attackTimer -= Time.deltaTime;

            // Kategoriye göre davranış
            switch (Category)
            {
                case BuildItemCategory.Turret:
                    UpdateTurret();
                    break;
                case BuildItemCategory.Trap:
                    UpdateTrap();
                    break;
                case BuildItemCategory.Slow:
                    UpdateSlow();
                    break;
                case BuildItemCategory.Barricade:
                    // Pasif — sadece düşmanların yolunu keser
                    break;
                case BuildItemCategory.Special:
                    // Özel
                    break;
            }
        }

        // -------------------------------------------------------------------------
        // GÖRSEL OLUŞTURMA
        // -------------------------------------------------------------------------
        private void EnsureVisual()
        {
            // Zaten bir mesh renderer varsa onu kullan
            _bodyRenderer = GetComponentInChildren<MeshRenderer>();
            if (_bodyRenderer == null)
            {
                // Boş bir küp oluştur
                var v = GameObject.CreatePrimitive(PrimitiveType.Cube);
                v.name = "Body";
                var col = v.GetComponent<Collider>();
                if (col != null) Destroy(col);
                v.transform.SetParent(transform, false);
                v.transform.localPosition = Vector3.up * 0.5f;
                v.transform.localScale = new Vector3(0.9f, 1f, 0.9f);
                _bodyRenderer = v.GetComponent<MeshRenderer>();
            }

            if (_bodyRenderer != null)
            {
                _bodyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                _bodyColor = GetCategoryColor(Category);
                _bodyMat.color = _bodyColor;
                _bodyRenderer.sharedMaterial = _bodyMat;
            }
        }

        private Color _bodyColor;
        private Color GetCategoryColor(BuildItemCategory cat) => cat switch
        {
            BuildItemCategory.Barricade => new Color(0.45f, 0.3f, 0.18f),  // kahverengi
            BuildItemCategory.Trap => new Color(0.7f, 0.15f, 0.15f),       // kırmızı
            BuildItemCategory.Turret => new Color(0.25f, 0.5f, 0.85f),     // mavi
            BuildItemCategory.Slow => new Color(0.2f, 0.7f, 0.85f),        // cyan
            BuildItemCategory.Special => new Color(0.7f, 0.3f, 0.85f),      // mor
            _ => Color.gray
        };

        // -------------------------------------------------------------------------
        // TAVET
        // -------------------------------------------------------------------------
        private void UpdateTurret()
        {
            if (_attackTimer > 0f) return;
            var enemy = FindNearestEnemyInRange();
            if (enemy != null && Damage > 0)
            {
                // Mermi oluştur
                SpawnProjectile(enemy);
                _attackTimer = AttackInterval;
            }
        }

        private void SpawnProjectile(EnemyBase target)
        {
            var go = new GameObject("Projectile");
            go.transform.position = transform.position + Vector3.up * 0.8f;
            var p = go.AddComponent<Projectile>();
            Color projColor = Category == BuildItemCategory.Turret
                ? new Color(0.4f, 0.9f, 1f) : Color.yellow;
            p.Setup(target, Damage, projColor);
        }

        // -------------------------------------------------------------------------
        // TUZAK
        // -------------------------------------------------------------------------
        private void UpdateTrap()
        {
            if (_attackTimer > 0f) return;
            // Temas menzilindeki ilk düşmana hasar ver
            var hits = Physics.OverlapSphere(transform.position, AttackRange);
            foreach (var h in hits)
            {
                var e = h.GetComponentInParent<EnemyBase>();
                if (e == null || e.IsDead) continue;
                e.TakeDamage(Damage);
                if (DebugOverlay.Instance != null)
                    DebugOverlay.Instance.Log(LogCategory.Build,
                        $"Tuzak patladı: {e.name} (-{Damage:F1})", false);
                // Kısa görsel: 0.4 saniye sön
                if (_bodyMat != null)
                {
                    var origCol = _bodyColor;
                    _bodyMat.color = Color.white;
                    Invoke(nameof(ResetColor), 0.15f);
                }
                _attackTimer = AttackInterval;
                break;
            }
        }

        private void ResetColor()
        {
            if (_bodyMat != null) _bodyMat.color = _bodyColor;
        }

        // -------------------------------------------------------------------------
        // SLOW
        // -------------------------------------------------------------------------
        private void UpdateSlow()
        {
            // Menzildeki tüm düşmanların hızını düşür
            var hits = Physics.OverlapSphere(transform.position, AttackRange);
            foreach (var h in hits)
            {
                var e = h.GetComponentInParent<EnemyBase>();
                if (e == null || e.IsDead) continue;
                // Geçici yavaşlatma — hızı düşür, sonra geri yükle
                e.moveSpeed = e.moveSpeed * 0.5f;
                // Sonraki frame'de eski haline dönmesi için Invoke
                e.CancelInvoke(nameof(EnemyBase.ResetSpeed));  // eğer varsa
            }
        }

        // -------------------------------------------------------------------------
        // YARDIMCI
        // -------------------------------------------------------------------------
        private EnemyBase FindNearestEnemyInRange()
        {
            var hits = Physics.OverlapSphere(transform.position, AttackRange);
            EnemyBase best = null;
            float bestDist = float.MaxValue;
            foreach (var h in hits)
            {
                var e = h.GetComponentInParent<EnemyBase>();
                if (e == null || e.IsDead) continue;
                float d = Vector3.Distance(transform.position, e.transform.position);
                if (d < bestDist) { best = e; bestDist = d; }
            }
            return best;
        }

        // -------------------------------------------------------------------------
        // HASAR / TAMİR
        // -------------------------------------------------------------------------
        public void TakeDamage(float amount)
        {
            if (IsBroken) return;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            if (_healthBar != null) _healthBar.Flash(0.2f);
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Build,
                    $"{name} hasar aldı: -{amount:F1} (kalan {CurrentHealth:F0}/{MaxHealth:F0})", false);
            if (IsBroken) Break();
        }

        public void Repair(float hpAmount, float costMultiplier = 1f)
        {
            if (IsBroken) return;
            float oldHp = CurrentHealth;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + hpAmount);
            float actual = CurrentHealth - oldHp;
            float cost = actual * RepairCostPerHp * costMultiplier;
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Build,
                    $"{name} tamir edildi: +{actual:F1} HP (maliyet ~{cost:F0})", false);
        }

        private void Break()
        {
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Build, $"{name} kırıldı!", false);
            if (destructionVfxPrefab != null)
                Instantiate(destructionVfxPrefab, transform.position, Quaternion.identity);
            // Görsel: söndür
            if (_bodyMat != null) _bodyMat.color = new Color(0.2f, 0.2f, 0.2f);
        }

        public void ShowRange(bool show)
        {
            if (_rangeIndicator != null) _rangeIndicator.SetActive(show);
        }
    }
}
