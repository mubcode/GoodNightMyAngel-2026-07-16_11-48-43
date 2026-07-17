// =============================================================================
// Weapon.cs
// -----------------------------------------------------------------------------
// Oyuncunun silahı. Sol tık ile ateş eder, şarjör ve reload sistemi vardır.
//
// Inspector'dan:
//   - Mermi hasarı
//   - Mermi hızı
//   - Şarjör kapasitesi
//   - Reload süresi
//   - Atış aralığı (saniye)
//   - Mermi prefab'ı (boşsa Projectile runtime oluşturulur)
//   - Muzzle (namlu) pozisyonu
// ayarlanabilir.
// =============================================================================

using UnityEngine;
using GoodNightMyAngel.Core;
using GoodNightMyAngel.Combat;
using GoodNightMyAngel.Enemies;

namespace GoodNightMyAngel.Player
{
    /// <summary>
    /// Oyuncunun silahı: sol tık ile ateş, şarjör dolduğunda reload.
    /// </summary>
    public class Weapon : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // INSPECTOR
        // -------------------------------------------------------------------------
        [Header("Hasar / Mermi")]
        [Tooltip("Her merminin verdiği hasar.")]
        [Min(1f)] public float damage = 25f;

        [Tooltip("Mermi hızı.")]
        [Min(1f)] public float bulletSpeed = 25f;

        [Tooltip("Mermi menzili (max yaşam süresi, saniye).")]
        [Min(0.5f)] public float bulletLifetime = 2f;

        [Header("Şarjör / Reload")]
        [Tooltip("Şarjör kapasitesi (mermi sayısı).")]
        [Min(1)] public int magazineSize = 12;

        [Tooltip("Reload süresi (saniye).")]
        [Min(0.1f)] public float reloadTime = 1.5f;

        [Tooltip("İki atış arası minimum süre (saniye).")]
        [Min(0.05f)] public float fireInterval = 0.18f;

        [Header("Referanslar")]
        [Tooltip("Mermi prefab'ı (boşsa runtime'da Projectile oluşturulur).")]
        public GameObject bulletPrefab;

        [Tooltip("Muzzle (namlu) pozisyonu. Boşsa bu objenin pozisyonu kullanılır.")]
        public Transform muzzle;

        [Header("Görsel / Ses")]
        [Tooltip("Muzzle flash (ateş anında parlama) prefab'ı.")]
        public GameObject muzzleFlashPrefab;

        [Tooltip("Ateş sesi.")]
        public AudioClip fireSound;

        [Tooltip("Reload sesi.")]
        public AudioClip reloadSound;

        [Tooltip("Şarjör boş sesi.")]
        public AudioClip emptySound;

        // -------------------------------------------------------------------------
        // DURUM
        // -------------------------------------------------------------------------
        public int CurrentAmmo { get; private set; }
        public int MaxAmmo => magazineSize;
        public bool IsReloading { get; private set; }
        public float ReloadProgress => _reloadTimer / Mathf.Max(0.01f, reloadTime);
        public bool CanFire => !IsReloading && CurrentAmmo > 0 && _fireTimer <= 0f;

        private float _fireTimer = 0f;
        private float _reloadTimer = 0f;
        private AudioSource _audio;

        public event System.Action<int, int> OnAmmoChanged;     // (current, max)
        public event System.Action OnReloadStart;
        public event System.Action OnReloadEnd;

        // -------------------------------------------------------------------------
        // YAŞAM DÖNGÜSÜ
        // -------------------------------------------------------------------------
        private void Awake()
        {
            CurrentAmmo = magazineSize;
            _audio = GetComponent<AudioSource>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            if (muzzle == null) muzzle = transform;
        }

        private void Start()
        {
            OnAmmoChanged?.Invoke(CurrentAmmo, magazineSize);
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.Status == GameStatus.Paused) return;
            if (GameManager.Instance != null && GameManager.Instance.Status == GameStatus.GameOver) return;

            _fireTimer -= Time.deltaTime;

            if (IsReloading)
            {
                _reloadTimer -= Time.deltaTime;
                if (_reloadTimer <= 0f)
                {
                    CurrentAmmo = magazineSize;
                    IsReloading = false;
                    OnAmmoChanged?.Invoke(CurrentAmmo, magazineSize);
                    OnReloadEnd?.Invoke();
                }
                return;
            }

            // Otomatik reload: mermi 0 olunca reload başlat
            if (CurrentAmmo <= 0)
            {
                StartReload();
            }
        }

        // -------------------------------------------------------------------------
        // PUBLIC API
        // -------------------------------------------------------------------------

        /// <summary>Oyuncunun ateş etme girişimi (genellikle sol tık).</summary>
        public bool TryFire()
        {
            if (IsReloading)
            {
                // Zaten reload ediyor, ses çıkar
                PlayClip(emptySound);
                return false;
            }
            if (CurrentAmmo <= 0)
            {
                PlayClip(emptySound);
                StartReload();
                return false;
            }
            if (_fireTimer > 0f) return false;

            Fire();
            return true;
        }

        /// <summary>Reload başlat (R tuşu veya otomatik).</summary>
        public void StartReload()
        {
            if (IsReloading) return;
            if (CurrentAmmo >= magazineSize) return;

            IsReloading = true;
            _reloadTimer = reloadTime;
            PlayClip(reloadSound);
            OnReloadStart?.Invoke();
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Player, "Reload başladı...", false);
        }

        // -------------------------------------------------------------------------
        // İÇ METOD
        // -------------------------------------------------------------------------
        private void Fire()
        {
            CurrentAmmo--;
            _fireTimer = fireInterval;
            OnAmmoChanged?.Invoke(CurrentAmmo, magazineSize);

            // Mermi oluştur
            Vector3 spawnPos = muzzle != null ? muzzle.position : transform.position + transform.forward * 0.5f;
            Quaternion spawnRot = muzzle != null ? muzzle.rotation : transform.rotation;

            GameObject go;
            if (bulletPrefab != null)
            {
                go = Instantiate(bulletPrefab, spawnPos, spawnRot);
            }
            else
            {
                go = new GameObject("PlayerBullet");
                go.transform.position = spawnPos;
                go.transform.rotation = spawnRot;
            }
            var bullet = go.GetComponent<Projectile>();
            if (bullet == null) bullet = go.AddComponent<Projectile>();
            // Hedef bul: aim noktası yönünde en yakın düşman (basit: shoot yönünde ray)
            EnemyBase target = FindClosestEnemyInAim();
            bullet.damage = damage;
            bullet.speed = bulletSpeed;
            bullet.maxLifetime = bulletLifetime;
            // Renk: sarı-yeşil (oyuncu mermisi)
            bullet.Setup(target, damage, new Color(1f, 0.95f, 0.3f));

            // Muzzle flash
            if (muzzleFlashPrefab != null)
                Instantiate(muzzleFlashPrefab, spawnPos, spawnRot);

            // Ateş sesi
            PlayClip(fireSound);

            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Player, $"Ateş! (Mermi: {CurrentAmmo}/{magazineSize})", false);
        }

        private EnemyBase FindClosestEnemyInAim()
        {
            // Karakterin aim yönünde 50 birim içindeki en yakın düşmanı bul
            Vector3 origin = muzzle != null ? muzzle.position : transform.position;
            Vector3 forward = muzzle != null ? muzzle.forward : transform.forward;

            var enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            EnemyBase best = null;
            float bestScore = float.MaxValue;
            foreach (var e in enemies)
            {
                if (e == null || e.IsDead) continue;
                Vector3 toE = e.transform.position - origin;
                float dist = toE.magnitude;
                if (dist > 50f) continue;
                // Açısal benzerlik skoru (düşük = daha iyi)
                float angle = Vector3.Angle(forward, toE);
                float score = angle + dist * 0.5f;
                if (score < bestScore) { best = e; bestScore = score; }
            }
            return best;
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip != null && _audio != null) _audio.PlayOneShot(clip);
        }
    }
}
