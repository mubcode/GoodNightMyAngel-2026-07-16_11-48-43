// =============================================================================
// TopDownCamera.cs
// -----------------------------------------------------------------------------
// Orcs Must Die / Dungeon Defenders tarzı RTS-tower-defense kamera.
//
// Temel davranış:
//   - Kamera oyuncuyu takip eder, ama oyuncunun ETRAFINDA döner (pivot).
//   - Q/E veya ok tuşları ile kamera YATAY (yaw) döndürülebilir.
//   - Mouse wheel ile zoom in/out.
//   - Edge scrolling: fare ekran kenarına yaklaşırsa kamera kayar (RTS tarzı).
//   - Mouse sağ tık basılıyken sürükleme ile kamera döndürülür (opsiyonel).
//
// Inspector'dan:
//   - Hedef (oyuncu transform'u)
//   - Mesafe, yükseklik, açı (pitch)
//   - Yumuşak takip hızı
//   - Zoom sınırları
//   - Dönüş hızı (Q/E ile)
//   - Edge scroll kenarlık genişliği
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
    /// RTS/tower defense tarzı top-down kamera.
    /// </summary>
    public class TopDownCamera : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // INSPECTOR
        // -------------------------------------------------------------------------
        [Header("Hedef")]
        [Tooltip("Kameranın takip edeceği transform (oyuncu). Boşsa Camera.main yönü kullanılır.")]
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

        [Header("Sürükle Döndürme (Sağ Tık)")]
        [Tooltip("Sağ tık basılıyken fare sürükleme ile kamera döndürülsün mü?")]
        public bool enableDragRotate = true;

        [Tooltip("Sürükleme ile dönüş hızı (derece/piksel).")]
        [Min(0.01f)] public float dragRotationSpeed = 0.4f;

        // -------------------------------------------------------------------------
        // DURUM
        // -------------------------------------------------------------------------
        private float _yaw = 0f;          // Yatay açı (derece)
        private float _currentDist;
        private Vector3 _smoothVel;
        private InputAction _lookAction;
        private InputActionAsset _inputActions;

        // -------------------------------------------------------------------------
        // YAŞAM DÖNGÜSÜ
        // -------------------------------------------------------------------------
        private void Start()
        {
            _currentDist = distance;

            // InputSystem referansı — sadece gerekiyorsa yükle
            if (TryGetInputActions(out var actions))
            {
                var map = actions.FindActionMap("Player", true);
                _lookAction = map?.FindAction("Look");
                if (_lookAction != null) _lookAction.Enable();
            }

            // İlk karede snap et (yumuşak başlamasın)
            transform.position = ComputeDesiredPos();
            if (target != null)
                transform.LookAt(target.position + Vector3.up * 0.5f);
        }

        private bool TryGetInputActions(out InputActionAsset actions)
        {
            actions = null;
            // PlayerController'dan öğren
            var pc = FindFirstObjectByType<Player.PlayerController>();
            if (pc != null && pc.inputActions != null) { actions = pc.inputActions; return true; }
            // Editor'de asset'ten yükle
            #if UNITY_EDITOR
            actions = UnityEditor.AssetDatabase
                .LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            return actions != null;
            #else
            return false;
            #endif
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            // Pause sırasında kamerayı güncelleme
            if (GameManager.Instance != null && GameManager.Instance.Status == GameStatus.Paused) return;

            // --- 1) Klavye dönüş (Q/E veya Ok tuşları) ---
            if (enableKeyboardRotation)
            {
                if (LegacyInputBridge.GetKey(KeyCode.Q) || LegacyInputBridge.GetKey(KeyCode.LeftArrow))
                    _yaw -= keyboardRotationSpeed * dt;
                if (LegacyInputBridge.GetKey(KeyCode.E) || LegacyInputBridge.GetKey(KeyCode.RightArrow))
                    _yaw += keyboardRotationSpeed * dt;
            }

            // --- 2) Sağ tık + sürükleme ile dönüş ---
            if (enableDragRotate && LegacyInputBridge.GetKey(KeyCode.Mouse1))
            {
                Vector2 look = _lookAction != null
                    ? _lookAction.ReadValue<Vector2>()
                    : Vector2.zero;
                // Sürükleme delta'sı olarak yorumla (sol-sağ)
                _yaw += look.x * dragRotationSpeed;
            }

            // --- 3) Zoom ---
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

            // --- 4) Edge scroll (fare kenar kaydırma) ---
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
                    // Kameranın baktığı yöne göre world space'de kaydır
                    Quaternion yawRot = Quaternion.Euler(0, _yaw, 0);
                    Vector3 worldMove = yawRot * move.normalized * edgeScrollSpeed * dt;
                    // Hedefi de kaydır
                    target.position += worldMove;
                }
            }

            // --- 5) Pozisyon güncelle ---
            Vector3 desired = ComputeDesiredPos();
            if (followSmoothTime <= 0f) transform.position = desired;
            else transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref _smoothVel, followSmoothTime);

            // Hedefe bak
            if (target != null)
                transform.LookAt(target.position + Vector3.up * 0.5f);

            // --- 6) Harita sınırı ---
            if (clampToMap && target != null)
            {
                Vector3 p = target.position;
                p.x = Mathf.Clamp(p.x, minX, maxX);
                p.z = Mathf.Clamp(p.z, minZ, maxZ);
                target.position = p;
                // Kamera da clampsiz takip eder; yine de clamp uygulayalım
                Vector3 cp = transform.position;
                cp.x = Mathf.Clamp(cp.x, minX - 1f, maxX + 1f);
                cp.z = Mathf.Clamp(cp.z, minZ - 1f, maxZ + 1f);
                transform.position = cp;
            }

            // HUD güncelle
            if (DebugOverlay.Instance != null)
                DebugOverlay.Instance.SetHudValue("Kamera Mesafe", $"{_currentDist:F1}");
        }

        /// <summary>
        /// Kameranın olması gereken pozisyonu hesapla (pivot + yaw + pitch + mesafe).
        /// </summary>
        private Vector3 ComputeDesiredPos()
        {
            if (target == null) return transform.position;

            // Yatay yön (yaw)
            Quaternion yawRot = Quaternion.Euler(0, _yaw, 0);
            Vector3 horizontal = yawRot * Vector3.forward;

            // Pitch ile yukarı/aşağı bileşen
            float pitchRad = pitch * Mathf.Deg2Rad;
            Vector3 offset = horizontal * (_currentDist * Mathf.Cos(pitchRad))
                           + Vector3.up   * (_currentDist * Mathf.Sin(pitchRad));
            return target.position + offset + Vector3.up * 0.5f;
        }

        // Public API: kamerayı bir noktaya hızlıca odakla (sahne kurulumunda kullanışlı)
        public void FocusOn(Vector3 worldPos)
        {
            if (target != null) target.position = worldPos;
            transform.position = ComputeDesiredPos();
            if (target != null) transform.LookAt(target.position + Vector3.up * 0.5f);
        }
    }
}
