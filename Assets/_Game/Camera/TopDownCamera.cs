// =============================================================================
// TopDownCamera.cs
// -----------------------------------------------------------------------------
// Sadece karakteri takip eden basit top-down kamera.
//
// Kontroller (sadeleştirildi):
//   - Mouse wheel zoom (opsiyonel)
//   - Kamera sadece takip eder; döndürme / edge scroll YOK
//
// Inspector'dan:
//   - Hedef (oyuncu)
//   - Mesafe, yükseklik, pitch
//   - Yumuşak takip hızı
//   - Zoom sınırları
//   - Harita sınırları (clamp)
// ayarlanabilir.
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using GoodNightMyAngel.Core;
using GoodNightMyAngel.InputBridge;

namespace GoodNightMyAngel.CameraSys
{
    /// <summary>
    /// Sadece takip eden top-down kamera. Karakteri ekranın merkezinde tutar.
    /// </summary>
    public class TopDownCamera : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // INSPECTOR
        // -------------------------------------------------------------------------
        [Header("Hedef")]
        [Tooltip("Kameranın takip edeceği transform (oyuncu).")]
        public Transform target;

        [Header("Konumlandırma")]
        [Tooltip("Kamera yüksekliği (birim).")]
        [Min(1f)] public float height = 11f;

        [Tooltip("Aşağı bakış açısı (derece). 60-70 arası tower defense için ideal.")]
        [Range(30f, 80f)] public float pitch = 65f;

        [Tooltip("Yatay mesafe (kameranın oyuncudan arkaya doğru uzaklığı).")]
        [Min(0f)] public float distance = 4f;

        [Header("Yumuşak Takip")]
        [Tooltip("Kamera takip yumuşaklığı (saniye). 0 = anlık takip.")]
        [Min(0f)] public float followSmoothTime = 0.12f;

        [Header("Zoom")]
        [Tooltip("Mouse wheel zoom aktif mi?")]
        public bool enableZoom = true;

        [Tooltip("Zoom çarpanı (0.5 = yarı mesafe, 1.5 = 1.5x mesafe).")]
        [Range(0.4f, 2.5f)] public float zoomLevel = 1f;

        [Tooltip("Zoom step (her scroll adımı).")]
        [Min(0.05f)] public float zoomStep = 0.1f;

        [Header("Harita Sınırları")]
        [Tooltip("Kameranın harita dışına çıkmasını engelle.")]
        public bool clampToMap = false;

        public float minX = -45f;
        public float maxX = 45f;
        public float minZ = -45f;
        public float maxZ = 45f;

        // -------------------------------------------------------------------------
        // DURUM
        // -------------------------------------------------------------------------
        private Vector3 _smoothVel;
        private Camera _cam;

        // -------------------------------------------------------------------------
        // YAŞAM DÖNGÜSÜ
        // -------------------------------------------------------------------------
        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = Camera.main;
        }

        private void Start()
        {
            // İlk karede snap et
            if (target != null)
            {
                transform.position = ComputeDesiredPos();
                transform.LookAt(target.position + Vector3.up * 0.5f);
            }
        }

        private void LateUpdate()
        {
            if (GameManager.Instance != null && GameManager.Instance.Status == GameStatus.Paused) return;

            // Zoom (mouse wheel)
            if (enableZoom && Mouse.current != null)
            {
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    zoomLevel = Mathf.Clamp(zoomLevel - wheel * zoomStep * 0.05f, 0.4f, 2.5f);
                }
            }

            // Hedef yoksa sabit kal
            if (target == null) return;

            // Hedefi harita sınırları içinde tut
            if (clampToMap)
            {
                Vector3 p = target.position;
                p.x = Mathf.Clamp(p.x, minX, maxX);
                p.z = Mathf.Clamp(p.z, minZ, maxZ);
                target.position = p;
            }

            // Pozisyon güncelle
            Vector3 desired = ComputeDesiredPos();
            if (followSmoothTime <= 0f)
                transform.position = desired;
            else
                transform.position = Vector3.SmoothDamp(
                    transform.position, desired, ref _smoothVel, followSmoothTime);

            // Hedefe bak
            transform.LookAt(target.position + Vector3.up * 0.5f);

            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.SetHudValue("Kamera Zoom", $"{zoomLevel:F2}x");
        }

        /// <summary>
        /// Kameranın olması gereken pozisyonu hesapla (pivot + pitch + mesafe).
        /// Kamera karakterin ÜSTÜNDE ve biraz arkasında olur (top-down his).
        /// </summary>
        private Vector3 ComputeDesiredPos()
        {
            if (target == null) return transform.position;

            float pitchRad = pitch * Mathf.Deg2Rad;
            // Kamera yönü: pitch aşağı, distance kadar geri
            Vector3 back = -target.forward;     // karakterin baktığı yönün tersi
            Vector3 up = Vector3.up;

            // Yatay geri vektör (yer çekimi yönünde)
            Vector3 horizontalBack = Vector3.ProjectOnPlane(back, Vector3.up).normalized;

            // final pozisyon: hedef + (yatay_geri * mesafe) + (yukarı * yükseklik)
            Vector3 offset = horizontalBack * (distance * zoomLevel)
                           + up * (height * zoomLevel);
            return target.position + offset;
        }
    }
}
