// =============================================================================
// PlayerHealth.cs
// -----------------------------------------------------------------------------
// Oyuncunun (çocuğun) canı ve hasarı. Gündüz sahnesinde gerçek beden için,
// gece sahnesinde ruh formu için ortaklaşa kullanılabilir.
//
// Bu script "oyuncu" GameObject'ine eklenir ve Inspector'dan can değerleri
// ayarlanabilir.
// =============================================================================

using UnityEngine;
using UnityEngine.Events;
using GoodNightMyAngel.Core;

namespace GoodNightMyAngel.Player
{
    /// <summary>
    /// Oyuncunun can bileşeni. Düşmanlardan, tuzaklardan vb. hasar alabilir.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Can")]
        [Tooltip("Maksimum can.")]
        [Min(1f)] public float maxHealth = 100f;

        [Tooltip("Başlangıç canı.")]
        public float startHealth = 100f;

        [Tooltip("Hasar alındığında uygulanan iyileşme gecikmesi (saniye).")]
        public float invulnerabilityTime = 0.4f;

        [Header("Görsel")]
        [Tooltip("Hasar alındığında titreme süresi.")]
        public float hitShakeDuration = 0.15f;

        [Tooltip("Ölüm efekti prefab'ı.")]
        public GameObject deathVfxPrefab;

        // UnityEvent: Inspector'dan diğer sistemlere bağlanabilir (UI, ses vb.)
        [System.Serializable] public class FloatEvent : UnityEvent<float> {}
        [System.Serializable] public class EmptyEvent : UnityEvent {}

        [Header("Olaylar")]
        public FloatEvent OnHealthChanged;
        public EmptyEvent OnPlayerDied;

        // Durum
        public float CurrentHealth { get; private set; }
        public bool IsDead { get; private set; }
        private float _invulnTimer = 0f;
        private float _shakeTimer = 0f;
        private Vector3 _baseLocalPos;

        private void Awake()
        {
            CurrentHealth = Mathf.Clamp(startHealth, 0f, maxHealth);
            _baseLocalPos = transform.localPosition;
        }

        private void Start()
        {
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.SetHudValue("Oyuncu Canı", $"{CurrentHealth:F0}/{maxHealth:F0}");
        }

        private void Update()
        {
            if (_invulnTimer > 0f) _invulnTimer -= Time.deltaTime;

            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.deltaTime;
                transform.localPosition = _baseLocalPos + (Vector3)(Random.insideUnitCircle * 0.1f);
                if (_shakeTimer <= 0f) transform.localPosition = _baseLocalPos;
            }
        }

        public void TakeDamage(float amount)
        {
            if (IsDead) return;
            if (_invulnTimer > 0f) return;     // hala dokunulmaz

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            _invulnTimer = invulnerabilityTime;
            _shakeTimer = hitShakeDuration;

            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Player,
                    $"Oyuncu hasar aldı: -{amount:F1} (kalan {CurrentHealth:F1}/{maxHealth:F1})", false);
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.SetHudValue("Oyuncu Canı", $"{CurrentHealth:F0}/{maxHealth:F0}");

            OnHealthChanged?.Invoke(CurrentHealth / maxHealth);

            if (CurrentHealth <= 0f) Die();
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Player, $"Oyuncu iyileşti: +{amount:F1}", false);
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.SetHudValue("Oyuncu Canı", $"{CurrentHealth:F0}/{maxHealth:F0}");
            OnHealthChanged?.Invoke(CurrentHealth / maxHealth);
        }

        private void Die()
        {
            IsDead = true;
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.Log(LogCategory.Player, "OYUNCU ÖLDÜ.", false);
            if (deathVfxPrefab != null)
                Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
            OnPlayerDied?.Invoke();
        }
    }
}
