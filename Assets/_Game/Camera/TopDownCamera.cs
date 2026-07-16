// =============================================================================
// TopDownCamera.cs
// -----------------------------------------------------------------------------
// Tam tepeden değil, hafif açılı bir top-down kamera. Inspector'dan:
//   - Takip edilecek hedef (player)
//   - Kamera yüksekliği
//   - Açı (pitch)
//   - Mesafe
//   - Yumuşak takip hızı
//   - Sınır (clamp) değerleri
// ayarlanabilir.
//
// Yeni Input System'in "Look" aksiyonu (sağ analog) ile hafif ofset
// değiştirilebilir; mouse wheel ile zoom yapılabilir.
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using GoodNightMyAngel.Core;
using GoodNightMyAngel.Player;

namespace GoodNightMyAngel.CameraSys
{
    /// <summary>
    /// Hafif açılı top-down kamera kontrol scripti.
    /// </summary>
    public class TopDownCamera : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // INSPECTOR
        // -------------------------------------------------------------------------
        [Header("Hedef")]
        [Tooltip("Kameranın takip edeceği transform (genellikle oyuncu).")]
        public Transform target;

        [Header("Konumlandırma")]
        [Tooltip("Kamera yüksekliği (birim).")]
        [Min(1f)] public float height = 12f;

        [Tooltip("Kamera yatay mesafesi (birim).")]
        [Min(1f)] public float distance = 10f;

        [Tooltip("Aşağı doğru bakış açısı (derece). 90 = tam tepeden, 30 = daha yatay.")]
        [Range(20f, 80f)] public float pitch = 55f;

        [Header("Yumuşak Takip")]
        [Tooltip("Kameranın hedefe yetişme hızı. 0 = anlık takip.")]
        [Min(0f)] public float followSmoothTime = 0.15f;

        [Header("Zoom")]
        [Tooltip("Mouse wheel ile zoom yapılabilir mi?")]
        public bool enableZoom = true;

        [Tooltip("Minimum mesafe (zoom in sınırı).")]
        [Min(1f)] public float minDistance = 5f;

        [Tooltip("Maksimum mesafe (zoom out sınırı).")]
        [Min(1f)] public float maxDistance = 25f;

        [Tooltip("Mouse wheel zoom hassasiyeti.")]
        public float zoomSensitivity = 2f;

        [Header("Sınırlar (opsiyonel)")]
        [Tooltip("Kameranın X/Z ekseninde harita dışına çıkmasını engelle.")]
        public bool clampPosition = false;

        [Tooltip("Minimum X")]
        public float minX = -50f;
        [Tooltip("Maksimum X")]
        public float maxX = 50f;
        [Tooltip("Minimum Z")]
        public float minZ = -50f;
        [Tooltip("Maksimum Z")]
        public float maxZ = 50f;

        [Header("Ofset (sağ analog)")]
        [Tooltip("Look aksiyonu ile kamera ofset kaydırma hassasiyeti.")]
        public float lookOffsetSensitivity = 2f;

        [Tooltip("Look ofsetinin sıfırlanma hızı.")]
        public float lookOffsetDecay = 3f;

        // -------------------------------------------------------------------------
        // DURUM
        // -------------------------------------------------------------------------
        private Vector3 _smoothVel;
        private Vector3 _lookOffset;
        private InputAction _lookAction;
        private InputActionAsset _inputActions;

        // -------------------------------------------------------------------------
        // YAŞAM DÖNGÜSÜ
        // -------------------------------------------------------------------------
        private void Start()
        {
            if (cameraTransformNull()) return;

            // Başlangıçta hedefe snap et
            if (target != null)
            {
                transform.position = ComputeDesiredPos(Vector3.zero);
                transform.LookAt(target.position + Vector3.up * 1f);
            }

            // Input System: Look action (gamepad right stick veya mouse delta)
            _inputActions = GetComponentInParent<Player.PlayerController>()?.inputActions;
            if (_inputActions == null)
            {
                #if UNITY_EDITOR
                _inputActions = UnityEditor.AssetDatabase
                    .LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
                #endif
            }
            if (_inputActions != null)
            {
                var map = _inputActions.FindActionMap("Player", true);
                _lookAction = map?.FindAction("Look");
                if (_lookAction != null) _lookAction.Enable();
            }
        }

        private bool cameraTransformNull() => transform == null;

        private void LateUpdate()
        {
            if (target == null) return;

            // Look ofset güncelle
            if (_lookAction != null)
            {
                Vector2 l = _lookAction.ReadValue<Vector2>();
                if (l.sqrMagnitude > 0.01f)
                {
                    _lookOffset += new Vector3(l.x, 0, l.y) * lookOffsetSensitivity * Time.deltaTime;
                }
                else
                {
                    // Yavaşça sıfırlanır
                    _lookOffset = Vector3.Lerp(_lookOffset, Vector3.zero,
                        lookOffsetDecay * Time.deltaTime);
                }
            }

            // Zoom
            if (enableZoom && Mouse.current != null)
            {
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    distance = Mathf.Clamp(distance - wheel * zoomSensitivity * 0.01f,
                                           minDistance, maxDistance);
                }
            }

            Vector3 desired = ComputeDesiredPos(_lookOffset);

            if (followSmoothTime <= 0f)
            {
                transform.position = desired;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position, desired, ref _smoothVel, followSmoothTime);
            }

            transform.LookAt(target.position + Vector3.up * 1f);

            if (clampPosition)
            {
                Vector3 p = transform.position;
                p.x = Mathf.Clamp(p.x, minX, maxX);
                p.z = Mathf.Clamp(p.z, minZ, maxZ);
                transform.position = p;
            }

            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.SetHudValue("Kamera Mesafe", $"{distance:F1}");
        }

        private Vector3 ComputeDesiredPos(Vector3 ofset)
        {
            // pitch derecesini radyana çevir, yükseklik/mesafe kullanarak nokta hesapla
            float rad = pitch * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(0, Mathf.Sin(rad), Mathf.Cos(rad));
            Vector3 pos = target.position - dir * distance + Vector3.up * height + ofset;
            return pos;
        }
    }
}
