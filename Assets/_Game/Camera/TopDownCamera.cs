// =============================================================================
// TopDownCamera.cs
// -----------------------------------------------------------------------------
// Sabit açılı top-down kamera. Dünya koordinatlarında SABİT kalır:
//   - Hiçbir zaman dönmez
//   - Karaktere bakmaz, LookAt YAPMAZ
//   - Sadece karakterin XZ pozisyonunu takip eder
//   - Pitch/yaw/yön değişmez
//
// Bu sayede karakter fareye doğru döndüğünde kamera yerinde kalır ve
// dünya "kendi etrafında döner" hissi oluşur (RTS / Diablo tarzı).
//
// Kontroller (sadeleştirildi):
//   - Mouse wheel zoom (opsiyonel)
//   - Kamera sadece takip eder
//
// Inspector'dan:
//   - Hedef (oyuncu)
//   - Yükseklik
//   - Pitch (aşağı bakış açısı)
//   - Zoom
//   - Takip yumuşaklığı
//   - Harita sınırları
// ayarlanabilir.
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using GoodNightMyAngel.Core;
using GoodNightMyAngel.InputBridge;

namespace GoodNightMyAngel.CameraSys
{
    /// <summary>
    /// Sabit top-down kamera. Karakteri takip eder ama asla dönmez.
    /// Karakterin yönü değiştiğinde dünya değişir, kamera değişmez.
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
        [Tooltip("Kamera yüksekliği (birim). Yüksek değer = daha tepeden görünüm.")]
        [Min(1f)] public float height = 11f;

        [Tooltip("Aşağı bakış açısı (derece). 60-70 arası tower defense için ideal.")]
        [Range(30f, 80f)] public float pitch = 65f;

        [Header("Yumuşak Takip")]
        [Tooltip("Kamera takip yumuşaklığı (saniye). 0 = anlık takip.")]
        [Min(0f)] public float followSmoothTime = 0.12f;

        [Header("Zoom")]
        [Tooltip("Mouse wheel zoom aktif mi?")]
        public bool enableZoom = true;

        [Tooltip("Zoom çarpanı (1.0 = normal, 1.5 = 1.5x daha uzak).")]
        [Range(0.4f, 2.5f)] public float zoomLevel = 1f;

        [Tooltip("Zoom step (her scroll adımı).")]
        [Min(0.05f)] public float zoomStep = 0.1f;

        [Header("Yön")]
        [Tooltip("Kamera SABİT yönde kalır. 'Sabit yön' değiştirilirse kamera o yöne döner, sonra sabit kalır.")]
        public bool resetDirectionOnStart = true;

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
        private Quaternion _fixedRotation;        // hiç değişmeyen kamera rotasyonu
        private bool _rotationInitialized;

        // -------------------------------------------------------------------------
        // YAŞAM DÖNGÜSÜ
        // -------------------------------------------------------------------------
        private void Start()
        {
            // Kameranın rotasyonunu şu anki rotasyon olarak sabitle.
            // Bu rotasyon dünya koordinatlarında değişmeyecek.
            if (resetDirectionOnStart)
            {
                _fixedRotation = Quaternion.Euler(pitch, 0, 0);
                transform.rotation = _fixedRotation;
                _rotationInitialized = true;
            }

            // İlk karede snap et
            if (target != null)
                transform.position = ComputeDesiredPos();
        }

        private void LateUpdate()
        {
            if (GameManager.Instance != null && GameManager.Instance.Status == GameStatus.Paused) return;

            // Zoom
            if (enableZoom && Mouse.current != null)
            {
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    zoomLevel = Mathf.Clamp(zoomLevel - wheel * zoomStep * 0.05f, 0.4f, 2.5f);
                }
            }

            if (target == null) return;

            // Hedefi harita sınırları içinde tut
            if (clampToMap)
            {
                Vector3 p = target.position;
                p.x = Mathf.Clamp(p.x, minX, maxX);
                p.z = Mathf.Clamp(p.z, minZ, maxZ);
                target.position = p;
            }

            // Sadece XZ pozisyonunu takip et, rotasyon hiç değişmez
            Vector3 desired = ComputeDesiredPos();
            if (followSmoothTime <= 0f)
            {
                transform.position = desired;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position, desired, ref _smoothVel, followSmoothTime);
            }

            // Rotasyonu HER FRAME sabit tut (karakterin dönmesi kamerayı etkilemez)
            if (_rotationInitialized)
                transform.rotation = _fixedRotation;

            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.SetHudValue("Kamera Zoom", $"{zoomLevel:F2}x");
        }

        /// <summary>
        /// Kameranın olması gereken pozisyon. SABİT yöne göre hesaplanır,
        /// karakterin yönü dikkate alınmaz.
        /// </summary>
        private Vector3 ComputeDesiredPos()
        {
            if (target == null) return transform.position;

            // Kameranın sabit yönüne göre offset hesapla
            // Pitch aşağı, geriye doğru (kameranın local -Z yönü)
            float pitchRad = pitch * Mathf.Deg2Rad;

            // Kamera dünya koordinatlarında -Z yönüne bakıyor
            // Offset: hedef + (ileri yönde * yatay_mesafe) + (yukarı * yükseklik)
            // yaw=0 olduğu için ileri yön = (0, 0, 1) (kuzey)
            // Ama pitch ile baktığı için geri yön (-forward) hesaplanmalı
            float horizDist = height / Mathf.Tan(pitchRad);   // yükseklik ve pitch'ten yatay mesafe
            Vector3 offset = new Vector3(0, height, -horizDist) * zoomLevel;
            return target.position + offset;
        }
    }
}
