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

        [Header("Health Bar")]
        [Tooltip("Düşman üzerinde can barı göster.")]
        public bool showHealthBar = true;

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

        // Waypoint takibi (PathManager'dan alınır)
        private System.Collections.Generic.List<Vector3> _currentPath;
        private int _pathIndex = 0;
        private float _waitTimer = 0f;
        private float _currentSpeedMul = 1f;

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

            // Waypoint yolunu al (PathManager varsa)
            if (PathManager.Instance != null)
            {
                _currentPath = PathManager.Instance.GetRandomPath();
                _pathIndex = 0;
            }

            // Can barı ekle
            if (showHealthBar)
            {
                var hb = gameObject.AddComponent<World.HealthBar>();
                hb.Bind(() => maxHealth > 0 ? CurrentHealth / maxHealth : 0f);
                hb.width = isBoss ? 2.0f : 1.0f;
                hb.heightOffset = isBoss ? 2.6f : 1.4f;
                hb.height = isBoss ? 0.22f : 0.14f;
                hb.foregroundColor = isBoss ? new Color(1f, 0.3f, 0.3f) : new Color(0.9f, 0.4f, 0.4f);
            }
        }

        /// <summary>Düşmanın hızını eski haline döndür (Slow tuzakları için).</summary>
        public void ResetSpeed()
        {
            // Bu metodun override'ı ileride yapılabilir; burada moveSpeed'i
            // default haline döndürmek yerine yapacak bir şey yok çünkü
            // baseSpeed inspector'da tutulmuyor. Bu yüzden slow tuzağı
            // yavaşlamayı bir sonraki dalga sonuna kadar koruyamaz;
            // ancak Update'te her frame zaten moveSpeed değişmiyorsa
            // yavaşlama kalıcı olur. BuildItem.UpdateSlow bunu yönetir.
        }

        protected virtual void Update()
        {
            if (IsDead) return;

            _attackTimer -= Time.deltaTime;
            _damageCooldown -= Time.deltaTime;

            // Önce yatak/oyuncu menzilinde miyiz? (saldırı)
            Transform attackTarget = ChooseAttackTarget();
            if (attackTarget != null)
            {
                float dist = Vector3.Distance(transform.position, attackTarget.position);
                if (dist <= (attackTarget == targetPlayer ? playerAttackRange : bedAttackRange))
                {
                    if (_attackTimer <= 0f)
                    {
                        DoAttack(attackTarget);
                        _attackTimer = attackCooldown;
                    }
                    return; // saldırı modundayken yol ilerlemez
                }
            }

            // Yol üzerinden ilerle
            FollowPath();
        }

        // -------------------------------------------------------------------------
        // YOL TAKİBİ (waypoint)
        // -------------------------------------------------------------------------
        protected virtual void FollowPath()
        {
            // Yol yoksa eski yönteme düş (yatak hedefi)
            if (_currentPath == null || _currentPath.Count == 0)
            {
                Transform moveTarget = ChooseMoveTarget();
                if (moveTarget != null) MoveTowards(moveTarget.position);
                return;
            }

            // Bekleme
            if (_waitTimer > 0f)
            {
                _waitTimer -= Time.deltaTime;
                return;
            }

            if (_pathIndex >= _currentPath.Count)
            {
                // Yolun sonuna geldik, yatağa saldır
                if (targetBed != null)
                {
                    float d = Vector3.Distance(transform.position, targetBed.transform.position);
                    if (d > bedAttackRange) MoveTowards(targetBed.transform.position);
                }
                return;
            }

            Vector3 waypoint = _currentPath[_pathIndex];
            float distWp = Vector3.Distance(transform.position, waypoint);

            // Waypoint'e yakınız, bir sonrakine geç
            if (distWp < 0.6f)
            {
                _pathIndex++;
                // Bir sonraki waypoint'te bekleme var mı? (PathWaypoint'ten)
                if (_pathIndex < _currentPath.Count)
                {
                    // Şu anki pozisyona en yakın PathWaypoint'i bul ve waitTime'ı al
                    var allWps = FindObjectsByType<PathWaypoint>(FindObjectsSortMode.None);
                    foreach (var wp in allWps)
                    {
                        if (wp == null) continue;
                        if (Vector3.Distance(wp.transform.position, waypoint) < 0.1f)
                        {
                            _waitTimer = wp.waitTime;
                            _currentSpeedMul = wp.speedMultiplier;
                            break;
                        }
                    }
                }
                return;
            }

            // Waypoint'e doğru ilerle
            float effectiveSpeed = moveSpeed * _currentSpeedMul;
            if (useNavMesh && _agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.speed = effectiveSpeed;
                _agent.SetDestination(waypoint);
            }
            else
            {
                Vector3 dir = (waypoint - transform.position);
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f) return;
                dir.Normalize();
                transform.position += dir * effectiveSpeed * Time.deltaTime;
                if (turnSpeed > 0f)
                {
                    Quaternion target = Quaternion.LookRotation(dir);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation, target, turnSpeed * Time.deltaTime);
                }
            }
        }

        /// <summary>Sonraki waypoint'in yönü (minimap için).</summary>
        public Vector3 GetNextWaypointDirection()
        {
            if (_currentPath == null || _pathIndex >= _currentPath.Count) return Vector3.zero;
            Vector3 d = _currentPath[_pathIndex] - transform.position;
            d.y = 0;
            return d.normalized;
        }

        public Vector3 GetCurrentWaypoint()
        {
            if (_currentPath == null || _pathIndex >= _currentPath.Count) return transform.position;
            return _currentPath[_pathIndex];
        }

        public int GetPathIndex() => _pathIndex;
        public int GetPathLength() => _currentPath?.Count ?? 0;

        // -------------------------------------------------------------------------
        // HEDEF SEÇİMİ (saldırı için)
        // -------------------------------------------------------------------------
        protected virtual Transform ChooseAttackTarget()
        {
            if (targetBed == null && targetPlayer == null) return null;

            if (!preferCloserTarget) return targetBed != null ? targetBed.transform : targetPlayer;

            float dBed = targetBed != null
                ? Vector3.Distance(transform.position, targetBed.transform.position) : float.MaxValue;
            float dPlayer = targetPlayer != null
                ? Vector3.Distance(transform.position, targetPlayer.position) : float.MaxValue;

            return dPlayer < dBed ? targetPlayer : (targetBed != null ? targetBed.transform : null);
        }

        // Eski isim için de alias (geriye uyumluluk)
        protected virtual Transform ChooseMoveTarget() => ChooseAttackTarget();

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
