// =============================================================================
// TopDownCamera.cs
// -----------------------------------------------------------------------------
// Orcs Must Die / Dungeon Defenders tarzı RTS-tower-defense kamera.
//
// Kontroller (güncellenmiş):
//   - Q/E veya ←/→ ile yatay (yaw) döndürme
//   - Mouse edge scrolling (fare ekranın kenarına yaklaşınca kayar)
//   - Mouse wheel zoom
//   - Sağ tık ile döndürme KALDIRILDI (PlayerController için ayrıldı)
//
// Inspector'dan:
//   - Hedef (oyuncu)
//   - Mesafe, yükseklik, pitch
//   - Yumuşak takip hızı
//   - Zoom sınırları
//   - Dönüş hızı (Q/E)
//   - Edge scroll ayarları
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
    /// RTS-tower-defense tarzı top-down kamera. Oyuncuyu takip eder,
    /// oyuncu etrafında yatay olarak döndürülebilir.
    /// </summary>
    public class TopDownCamera : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // INSPECTOR
        // -------------------------------------------------------------------------
        [Header("Hedef")]
        [Tooltip("Kameranın takip edeceği transform (oyuncu). Boşsa sadece yönü kullanır.")]
        public Transform target;

        [Header("Konumlandırma")]
        [Tooltip("Oyuncudan yatay mesafe (birim).")]
        [Min(1f)] public float distance = 9f;

        [Tooltip("Kamera yüksekliği (birim).")]
        [Min(1f)] public float height = 9f;

        [Tooltip("Aşağı bakış açısı (derece). 60-70 arası tower defense için ideal.")]
        [Range(30f, 80f)] public float pitch = 60f;

        [Header("Yumuşak Takip")]
        [Tooltip("Kamera takip yumuşaklığı (küçük = daha yavaş). 0 = anlık.")]
        [Min(0f)] public float followSmoothTime = 0.12f;

        [Header("Yatay Dönüş (Q/E)")]
        [Tooltip("Q/E veya Ok tuşları ile kamera dönsün mü?")]
        public bool enableKeyboardRotation = true;

        [Tooltip("Dönüş hızı (derece/saniye).")]
        [Min(0f)] public float keyboardRotationSpeed = 90f;

        [Header("Zoom")]
        [Tooltip("Mouse wheel zoom aktif mi?")]
        public bool enableZoom = true;

        [Tooltip("Minimum zoom (yakın).")]
        [Min(2f)] public float minDistance = 5f;

        [Tooltip("Maksimum zoom (uzak).")]
        [Min(2f)] public float maxDistance = 18f;

        [Tooltip("Mouse wheel zoom hassasiyeti.")]
        [Min(0.01f)] public float zoomSensitivity = 1.2f;

        [Header("Edge Scrolling (fare kenar kaydırma)")]
        [Tooltip("Fare ekran kenarına yaklaşınca kamera kaysın mı?")]
        public bool enableEdgeScroll = true;

        [Tooltip("Kenar bölgesi kalınlığı (piksel).")]
        [Min(1f)] public float edgeSize = 20f;

        [Tooltip("Edge scroll hızı (birim/saniye).")]
        [Min(0.1f)] public float edgeScrollSpeed = 12f;

        [Header("Harita Sınırları")]
        [Tooltip("Kameranın harita dışına çıkmasını engelle.")]
        public bool clampToMap = true;

        public float minX = -45f;
        public float maxX = 45f;
        public float minZ = -45f;
        public float maxZ = 45f;

        // -------------------------------------------------------------------------
        // DURUM
        // -------------------------------------------------------------------------
        private float _yaw = 0f;
        private float _currentDist;
        private Vector3 _smoothVel;

        // -------------------------------------------------------------------------
        // YAŞAM DÖNGÜSÜ
        // -------------------------------------------------------------------------
        private void Start()
        {
            _currentDist = distance;
            transform.position = ComputeDesiredPos();
            if (target != null)
                transform.LookAt(target.position + Vector3.up * 0.5f);
        }

        private void LateUpdate()
        {
            if (GameManager.Instance != null && GameManager.Instance.Status == GameStatus.Paused) return;

            float dt = Time.deltaTime;

            // --- Klavye dönüş (Q/E veya Ok tuşları) ---
            if (enableKeyboardRotation)
            {
                if (LegacyInputBridge.GetKey(KeyCode.Q) || LegacyInputBridge.GetKey(KeyCode.LeftArrow))
                    _yaw -= keyboardRotationSpeed * dt;
                if (LegacyInputBridge.GetKey(KeyCode.E) || LegacyInputBridge.GetKey(KeyCode.RightArrow))
                    _yaw += keyboardRotationSpeed * dt;
            }

            // --- Zoom ---
            if (enableZoom && Mouse.current != null)
            {
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    _currentDist = Mathf.Clamp(
                        _currentDist - wheel * zoomSensitivity * 0.05f,
                        minDistance, maxDistance);
                }
            }

            // --- Edge scroll ---
            if (enableEdgeScroll && target != null)
            {
                Vector2 m = LegacyInputBridge.mousePosition;
                Vector3 move = Vector3.zero;
                if (m.x < edgeSize) move.x = -1;
                else if (m.x > Screen.width - edgeSize) move.x = 1;
                if (m.y < edgeSize) move.z = -1;
                else if (m.y > Screen.height - edgeSize) move.z = 1;

                if (move.sqrMagnitude > 0.01f)
                {
                    Quaternion yawRot = Quaternion.Euler(0, _yaw, 0);
                    Vector3 worldMove = yawRot * move.normalized * edgeScrollSpeed * dt;
                    target.position += worldMove;
                }
            }

            // --- Pozisyon güncelle ---
            Vector3 desired = ComputeDesiredPos();
            if (followSmoothTime <= 0f) transform.position = desired;
            else transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref _smoothVel, followSmoothTime);

            if (target != null)
                transform.LookAt(target.position + Vector3.up * 0.5f);

            // --- Harita sınırı ---
            if (clampToMap && target != null)
            {
                Vector3 p = target.position;
                p.x = Mathf.Clamp(p.x, minX, maxX);
                p.z = Mathf.Clamp(p.z, minZ, maxZ);
                target.position = p;
                Vector3 cp = transform.position;
                cp.x = Mathf.Clamp(cp.x, minX - 1f, maxX + 1f);
                cp.z = Mathf.Clamp(cp.z, minZ - 1f, maxZ + 1f);
                transform.position = cp;
            }

            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.SetHudValue("Kamera Mesafe", $"{_currentDist:F1}");
        }

        private Vector3 ComputeDesiredPos()
        {
            if (target == null) return transform.position;
            Quaternion yawRot = Quaternion.Euler(0, _yaw, 0);
            Vector3 horizontal = yawRot * Vector3.forward;
            float pitchRad = pitch * Mathf.Deg2Rad;
            Vector3 offset = horizontal * (_currentDist * Mathf.Cos(pitchRad))
                           + Vector3.up   * (_currentDist * Mathf.Sin(pitchRad));
            return target.position + offset + Vector3.up * 0.5f;
        }
    }
}
