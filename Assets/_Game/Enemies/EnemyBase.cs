// =============================================================================
// EnemyBase.cs
// -----------------------------------------------------------------------------
// Tüm düşmanların (normal ve boss) temel davranışı. Inspector'dan:
//   - Hareket hızı
//   - Can / hasar
//   - Hedef (yatak veya oyuncu)
//   - Saldırı menzili ve hasarı
// ayarlanabilir.
//
// Düşman, yatağa doğru yürür; oyuncu müdahale ederse oyuncuya yönelir.
// Ölünce düşer ve spawner'a haber verir.
// =============================================================================

using UnityEngine;
using UnityEngine.AI;
using GoodNightMyAngel.Core;
using GoodNightMyAngel.World;

namespace GoodNightMyAngel.Enemies
{
    /// <summary>
    /// Tüm düşmanlar için temel sınıf. NavMeshAgent veya basit transform
    /// hareketi kullanır. Yatak ve oyuncu arasında önceliklendirme yapar.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class EnemyBase : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // INSPECTOR
        // -------------------------------------------------------------------------
        [Header("Can / Hasar")]
        [Tooltip("Maksimum can.")]
        [Min(1f)] public float maxHealth = 20f;

        [Tooltip("Bir vuruşta verdiği hasar (yatar veya oyuncu).")]
        [Min(0f)] public float damage = 5f;

        [Tooltip("Hasar çarpanı (GameManager tarafından dalva geçtikçe uygulanır).")]
        public float healthMultiplier = 1f;

        [Header("Hareket")]
        [Tooltip("Hareket hızı.")]
        [Min(0f)] public float moveSpeed = 2.5f;

        [Tooltip("NavMeshAgent kullanılsın mı? Kullanılmazsa doğrudan transform.MovePosition kullanılır.")]
        public bool useNavMesh = true;

        [Tooltip("Dönüş hızı.")]
        [Min(0f)] public float turnSpeed = 540f;

        [Header("Hedef")]
        [Tooltip("Yatak hedefi. Inspector'dan atanır, boşsa GameManager üzerinden aranır.")]
        public Bed targetBed;

        [Tooltip("Oyuncu hedefi. Atanmazsa 'Player' taglı obje aranır.")]
        public Transform targetPlayer;

        [Tooltip("Yatağa kaç birim yaklaşınca saldırmaya başlasın.")]
        [Min(0.1f)] public float bedAttackRange = 1.5f;

        [Tooltip("Oyuncuya kaç birim yaklaşınca saldırsın (varsa).")]
        [Min(0.1f)] public float playerAttackRange = 1.2f;

        [Tooltip("Saldırı bekleme süresi (saniye).")]
        [Min(0.1f)] public float attackCooldown = 0.8f;

        [Header("Öncelik")]
        [Tooltip("Oyuncu yataktan daha yakınsa oyuncuya yönel.")]
        public bool preferCloserTarget = true;

        [Header("Görsel")]
        [Tooltip("Düşman ölünce yok olmadan önce ne kadar beklesin (efekte zaman tanır).")]
        public float deathDestroyDelay = 1.5f;

        [Tooltip("Ölüm VFX prefab'ı.")]
        public GameObject deathVfxPrefab;

        [Header("Boss")]
        [Tooltip("Bu düşman boss mu?")]
        public bool isBoss = false;

        [Tooltip("Boss ise, öldüğünde GameManager'a bildir.")]
        public bool notifyOnBossDeath = true;

        // -------------------------------------------------------------------------
        // DURUM
        // -------------------------------------------------------------------------
        public float CurrentHealth { get; private set; }
        public bool IsDead { get; private set; }

        private NavMeshAgent _agent;
        private float _attackTimer;
        private float _damageCooldown;

        // -------------------------------------------------------------------------
        // YAŞAM DÖNGÜSÜ
        // -------------------------------------------------------------------------
        protected virtual void Awake()
        {
            CurrentHealth = maxHealth * Mathf.Max(0.1f, healthMultiplier);
            if (useNavMesh) _agent = GetComponent<NavMeshAgent>();
        }

        protected virtual void Start()
        {
            // Hedefleri otomatik bul
            if (targetBed == null && GameManager.Instance != null)
                targetBed = GameManager.Instance.bed;
            if (targetPlayer == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) targetPlayer = p.transform;
            }

            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Enemy,
                    $"Spawn oldu: {name} (HP={CurrentHealth:F0})", false);

            if (useNavMesh && _agent != null)
            {
                _agent.speed = moveSpeed;
                _agent.angularSpeed = turnSpeed;
            }
        }

        protected virtual void Update()
        {
            if (IsDead) return;

            _attackTimer -= Time.deltaTime;
            _damageCooldown -= Time.deltaTime;

            // Hedef seçimi
            Transform moveTarget = ChooseMoveTarget();
            if (moveTarget == null) return;

            float dist = Vector3.Distance(transform.position, moveTarget.position);

            // Saldırı menzilinde miyiz?
            if (dist <= (moveTarget == targetPlayer ? playerAttackRange : bedAttackRange))
            {
                if (_attackTimer <= 0f)
                {
                    DoAttack(moveTarget);
                    _attackTimer = attackCooldown;
                }
            }
            else
            {
                MoveTowards(moveTarget.position);
            }
        }

        // -------------------------------------------------------------------------
        // HEDEF SEÇİMİ
        // -------------------------------------------------------------------------
        protected virtual Transform ChooseMoveTarget()
        {
            if (targetBed == null && targetPlayer == null) return null;

            if (!preferCloserTarget) return targetBed != null ? targetBed.transform : targetPlayer;

            float dBed = targetBed != null
                ? Vector3.Distance(transform.position, targetBed.transform.position) : float.MaxValue;
            float dPlayer = targetPlayer != null
                ? Vector3.Distance(transform.position, targetPlayer.position) : float.MaxValue;

            return dPlayer < dBed ? targetPlayer : (targetBed != null ? targetBed.transform : null);
        }

        // -------------------------------------------------------------------------
        // HAREKET
        // -------------------------------------------------------------------------
        protected virtual void MoveTowards(Vector3 pos)
        {
            if (useNavMesh && _agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.SetDestination(pos);
            }
            else
            {
                Vector3 dir = (pos - transform.position);
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f) return;
                dir.Normalize();

                Vector3 next = transform.position + dir * moveSpeed * Time.deltaTime;
                // Basit yer çarpışması: BoxCast yerine doğrudan hareket ettiriyoruz;
                // duvarlar NavMesh tarafından ya da manuel collider'larla engellenir.
                transform.position = next;

                if (turnSpeed > 0f)
                {
                    Quaternion target = Quaternion.LookRotation(dir);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation, target, turnSpeed * Time.deltaTime);
                }
            }
        }

        // -------------------------------------------------------------------------
        // SALDIRI
        // -------------------------------------------------------------------------
        protected virtual void DoAttack(Transform target)
        {
            if (target == null) return;
            if (_damageCooldown > 0f) return;
            _damageCooldown = 0.1f;

            if (target.TryGetComponent<Bed>(out var bed))
            {
                bed.TakeDamage(damage, gameObject);
            }
            else if (target.TryGetComponent<Player.PlayerHealth>(out var ph))
            {
                ph.TakeDamage(damage);
            }
        }

        // -------------------------------------------------------------------------
        // HASAR ALMA
        // -------------------------------------------------------------------------
        public virtual void TakeDamage(float amount)
        {
            if (IsDead) return;
            CurrentHealth -= amount;
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Combat,
                    $"{name} hasar aldı: -{amount:F1} (kalan {CurrentHealth:F0})", false);
            if (CurrentHealth <= 0f) Die();
        }

        protected virtual void Die()
        {
            IsDead = true;
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Enemy, $"{name} öldü.", false);

            if (deathVfxPrefab != null)
                Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);

            // Spawner'a haber ver
            EnemySpawner.OnEnemyDied(this);

            if (isBoss && notifyOnBossDeath && GameManager.Instance != null)
            {
                GameManager.Instance.NotifyBossDefeated();
            }

            Destroy(gameObject, deathDestroyDelay);
        }
    }
}
