// =============================================================================
// BuildItem.cs
// -----------------------------------------------------------------------------
// Yerleştirilebilir bir savunma elemanı (barikat, tuzak, otomatik kule vb.).
// Inspector'dan canı, hasarı, menzili, maliyeti ayarlanabilir.
//
// BuildItem'lar ScriptableObject'lerle tanımlanır (BuildItemData) ve grid
// üzerine BuildManager tarafından yerleştirilir.
// =============================================================================

using UnityEngine;
using GoodNightMyAngel.Core;
using GoodNightMyAngel.Enemies;

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

        // Aktüel değerler (data'dan veya runtime'dan)
        public float MaxHealth { get; private set; }
        public float Damage { get; private set; }
        public float AttackRange { get; private set; }
        public float AttackInterval { get; private set; }
        public int Cost { get; private set; }
        public float RepairCostPerHp { get; private set; }

        public float CurrentHealth { get; private set; }
        public bool IsBroken => CurrentHealth <= 0f;

        private float _attackTimer = 0f;

        private void Awake()
        {
            // Değerleri ata
            if (data != null)
            {
                MaxHealth = data.maxHealth;
                Damage = data.damage;
                AttackRange = data.attackRange;
                AttackInterval = data.attackInterval;
                Cost = data.cost;
                RepairCostPerHp = data.repairCostPerHp;
            }
            else
            {
                MaxHealth = runtimeMaxHealth;
                Damage = runtimeDamage;
                AttackRange = runtimeAttackRange;
                AttackInterval = runtimeAttackInterval;
                Cost = runtimeCost;
                RepairCostPerHp = runtimeRepairCostPerHp;
            }
            CurrentHealth = MaxHealth;
        }

        private void Start()
        {
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Build,
                    $"Yerleştirildi: {name} (HP={CurrentHealth:F0}, hasar={Damage:F0})", false);
        }

        private void Update()
        {
            if (IsBroken) return;

            // Gece savaş sırasında otomatik saldırı
            if (GameManager.Instance == null) return;
            var t = GameManager.Instance.TimeOfDay;
            if (t != TimeOfDay.NightDefense && t != TimeOfDay.NightBoss) return;

            _attackTimer -= Time.deltaTime;
            if (_attackTimer > 0f) return;

            // Menzildeki en yakın düşmana vur
            var enemy = FindNearestEnemyInRange();
            if (enemy != null)
            {
                enemy.TakeDamage(Damage);
                _attackTimer = AttackInterval;
            }
        }

        private Enemies.EnemyBase FindNearestEnemyInRange()
        {
            // OverlapSphere ile hızlı sorgu
            var hits = Physics.OverlapSphere(transform.position, AttackRange);
            Enemies.EnemyBase best = null;
            float bestDist = float.MaxValue;
            foreach (var h in hits)
            {
                if (h == null) continue;
                var e = h.GetComponentInParent<Enemies.EnemyBase>();
                if (e == null || e.IsDead) continue;
                float d = Vector3.Distance(transform.position, e.transform.position);
                if (d < bestDist) { best = e; bestDist = d; }
            }
            return best;
        }

        public void TakeDamage(float amount)
        {
            if (IsBroken) return;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
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
            // İleride görsel değiştirilebilir (yanık/sigara)
        }
    }
}
