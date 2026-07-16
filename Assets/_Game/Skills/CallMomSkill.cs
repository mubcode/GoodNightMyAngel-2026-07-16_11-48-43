// =============================================================================
// CallMomSkill.cs
// -----------------------------------------------------------------------------
// "Call Mom" — her gece sadece 1 kez kullanılabilen ultimate skill.
//
// Kullanıldığında:
//   1) Harita dışına yakın bir noktada devasa bir kapı açılır (Prefab).
//   2) Kapıdan güçlü bir ışık hüzmesi tüm haritayı aydınlatır.
//   3) Annenin sesi gelir: "Honey, are you okay? I'm here for you."
//   4) Ekrandaki tüm yaratıklar anında ölür.
//   5) Henüz spawn olmamış yaratıklar yine gelmeye devam eder.
//
// Inspector'dan:
//   - Kapı prefab'ı ve ışık prefab'ı
//   - Ses klibi
//   - Cooldown / gecelik kullanım limiti
//   - Işık süresi, kapı açılma animasyonu süresi
//   ayarlanabilir.
// =============================================================================

using System.Collections;
using UnityEngine;
using GoodNightMyAngel.Core;
using GoodNightMyAngel.Enemies;
using GoodNightMyAngel.InputBridge;

namespace GoodNightMyAngel.Skills
{
    /// <summary>
    /// Call Mom ultimate skill yöneticisi.
    /// </summary>
    public class CallMomSkill : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // INSPECTOR
        // -------------------------------------------------------------------------
        [Header("Gereksinimler")]
        [Tooltip("Açılacak devasa kapı prefab'ı. Null ise runtime oluşturulur.")]
        public GameObject doorPrefab;

        [Tooltip("Işık hüzmesi prefab'ı. Null ise runtime oluşturulur.")]
        public GameObject lightBeamPrefab;

        [Header("Kullanım")]
        [Tooltip("Her gece maksimum kaç kez kullanılabilir.")]
        [Min(1)] public int usesPerNight = 1;

        [Tooltip("Aktif olabilmesi için oyuncunun sahip olması gereken durum. " +
                 "Boş bırakılırsa her zaman kullanılabilir.")]
        public bool onlyDuringNightDefense = true;

        [Header("Görsel")]
        [Tooltip("Kapının haritada belireceği noktalar. Boşsa düşmanların ters yönüne otomatik hesaplanır.")]
        public Transform[] doorSpawnPoints;

        [Tooltip("Kapının haritada görünme süresi (saniye).")]
        [Min(0.5f)] public float doorLifetime = 4f;

        [Tooltip("Işık hüzmesinin fade-out süresi.")]
        [Min(0.1f)] public float lightFadeOut = 2f;

        [Header("Ses")]
        [Tooltip("Annenin ses klibi: 'Honey, are you okay? I'm here for you.'")]
        public AudioClip momVoiceClip;

        [Tooltip("Ses seviyesi.")]
        [Range(0f, 1f)] public float voiceVolume = 1f;

        [Tooltip("Işık hüzmesi sesi (ambient).")]
        public AudioClip lightBeamSfx;

        [Header("Etki")]
        [Tooltip("Işın tüm düşmanları öldürür mü, yoksa sadece yüksek hasar mı verir?")]
        public bool instantKillAllEnemies = true;

        [Tooltip("Instant kill yerine yüksek hasar uygulanacaksa miktarı.")]
        public float fallbackDamage = 9999f;

        // -------------------------------------------------------------------------
        // DURUM
        // -------------------------------------------------------------------------
        public int RemainingUses { get; private set; }

        private AudioSource _audio;
        private Coroutine _activeRoutine;

        // -------------------------------------------------------------------------
        // YAŞAM DÖNGÜSÜ
        // -------------------------------------------------------------------------
        private void Awake()
        {
            _audio = GetComponent<AudioSource>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f; // 2D
        }

        private void OnEnable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnNightStarted += HandleNightStarted;
                GameManager.Instance.OnDayStarted += HandleDayStarted;
            }
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnNightStarted -= HandleNightStarted;
                GameManager.Instance.OnDayStarted -= HandleDayStarted;
            }
        }

        private void HandleNightStarted(int day)
        {
            RemainingUses = usesPerNight;
        }

        private void HandleDayStarted(int day)
        {
            RemainingUses = 0;
        }

        private void Update()
        {
            // Q tuşu ile tetikle (Inspector'dan tuşu değiştirebilirsin)
            if (LegacyInputBridge.GetKeyDown(KeyCode.Q))
            {
                TryUse();
            }
        }

        // -------------------------------------------------------------------------
        // PUBLIC API
        // -------------------------------------------------------------------------

        public bool CanUse()
        {
            if (RemainingUses <= 0) return false;
            if (GameManager.Instance == null) return false;
            if (GameManager.Instance.Status != GameStatus.Playing) return false;
            if (onlyDuringNightDefense)
            {
                var t = GameManager.Instance.TimeOfDay;
                if (t != TimeOfDay.NightDefense && t != TimeOfDay.NightBoss) return false;
            }
            return true;
        }

        public bool TryUse()
        {
            if (!CanUse()) return false;
            RemainingUses--;
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Skill,
                    $"CALL MOM tetiklendi! (Kalan kullanım: {RemainingUses})", false);
            if (_activeRoutine != null) StopCoroutine(_activeRoutine);
            _activeRoutine = StartCoroutine(CallMomRoutine());
            return true;
        }

        // -------------------------------------------------------------------------
        // COROUTINE
        // -------------------------------------------------------------------------
        private IEnumerator CallMomRoutine()
        {
            // 1) Kapıyı oluştur
            Vector3 doorPos = ComputeDoorPosition();
            GameObject door = null;
            if (doorPrefab != null)
            {
                door = Instantiate(doorPrefab, doorPos, Quaternion.identity);
            }
            else
            {
                // Geçici: büyük bir kutu
                door = GameObject.CreatePrimitive(PrimitiveType.Cube);
                door.transform.position = doorPos;
                door.transform.localScale = new Vector3(3f, 6f, 0.5f);
                var r = door.GetComponent<Renderer>();
                if (r != null) r.material.color = new Color(0.9f, 0.9f, 1f, 0.6f);
            }

            // 2) Işık hüzmesi
            GameObject beam = null;
            if (lightBeamPrefab != null)
            {
                beam = Instantiate(lightBeamPrefab, doorPos, Quaternion.identity);
            }
            else
            {
                // Geçici: yukarı doğru ışık silindiri
                beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                beam.transform.position = doorPos + Vector3.up * 10f;
                beam.transform.localScale = new Vector3(2f, 10f, 2f);
                var r = beam.GetComponent<Renderer>();
                if (r != null)
                {
                    r.material.color = new Color(1f, 0.95f, 0.7f, 0.6f);
                }
            }

            // 3) Ses: ışık sesi + anne sesi
            if (lightBeamSfx != null) _audio.PlayOneShot(lightBeamSfx, voiceVolume);
            if (momVoiceClip != null) _audio.PlayOneShot(momVoiceClip, voiceVolume);

            // 4) Kısa bir bekleme (kapı açılma animasyonu)
            yield return new WaitForSeconds(0.8f);

            // 5) Tüm düşmanları öldür veya yüksek hasar ver
            int killed = KillAllEnemies();
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Skill,
                    $"Anne ışığı tüm haritayı yıkadı: {killed} düşman öldü.", false);

            // 6) Işık fade-out
            float t = 0f;
            while (t < lightFadeOut)
            {
                t += Time.deltaTime;
                if (beam != null)
                {
                    foreach (var r in beam.GetComponentsInChildren<Renderer>())
                    {
                        var c = r.material.color;
                        c.a = Mathf.Lerp(0.6f, 0f, t / lightFadeOut);
                        r.material.color = c;
                    }
                }
                yield return null;
            }

            if (door != null) Destroy(door, doorLifetime);
            if (beam != null) Destroy(beam);
            _activeRoutine = null;
        }

        private int KillAllEnemies()
        {
            int count = 0;
            // Tüm EnemyBase'leri bul. Tag'lı objeleri de dahil eder.
            var enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            foreach (var e in enemies)
            {
                if (e == null || e.IsDead) continue;
                if (instantKillAllEnemies)
                {
                    e.TakeDamage(e.CurrentHealth + 1f);
                    count++;
                }
                else
                {
                    e.TakeDamage(fallbackDamage);
                    count++;
                }
            }
            return count;
        }

        private Vector3 ComputeDoorPosition()
        {
            if (doorSpawnPoints != null && doorSpawnPoints.Length > 0)
            {
                return doorSpawnPoints[Random.Range(0, doorSpawnPoints.Length)].position;
            }
            // Yatağın ters yönüne, harita sınırına yakın bir nokta
            Vector3 bedPos = GameManager.Instance != null && GameManager.Instance.bed != null
                ? GameManager.Instance.bed.transform.position
                : Vector3.zero;
            Vector3 dir = bedPos.normalized == Vector3.zero ? Vector3.forward : bedPos.normalized;
            return bedPos + (-dir) * 25f + Vector3.up * 0.1f;
        }
    }
}
