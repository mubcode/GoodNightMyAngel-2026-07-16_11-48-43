// =============================================================================
// PlayerController.cs
// -----------------------------------------------------------------------------
// MOBA/RPG tarzı kontrol: WASD ile hareket, mouse ile rotasyon, Space ile
// zıplama, ray (sanal çizgi) ile hedef göstergesi.
//
// Inspector'dan:
//   - Hareket hızı
//   - Sprint çarpanı
//   - Dönüş hızı
//   - Zıplama gücü, yer çekimi
//   - Crosshair prefab'ı (boşsa basit küp oluşturulur)
//   - Ray görsel rengi/kalınlığı
//   - Yer çekimi
// ayarlanabilir.
//
// Yeni Input System üzerinden: Move (Vector2), Sprint (Button), Jump (Button).
// Mouse rotasyonu LegacyInputBridge üzerinden okunur (Pointer position).
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using GoodNightMyAngel.Core;
using GoodNightMyAngel.InputBridge;

namespace GoodNightMyAngel.Player
{
    /// <summary>
    /// MOBA/RPG tarzı oyuncu kontrol scripti.
    /// Hareket: WASD / Sol analog.
    /// Rotasyon: Mouse pozisyonuna göre (mouse = oyundaki crosshair).
    /// Zıplama: Space.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // INSPECTOR — HAREKET
        // -------------------------------------------------------------------------
        [Header("Hareket")]
        [Tooltip("Normal yürüme hızı (birim/saniye).")]
        [Min(0f)] public float moveSpeed = 4f;

        [Tooltip("Sprint (koşma) hızı çarpanı.")]
        [Min(1f)] public float sprintMultiplier = 1.6f;

        [Tooltip("Dönüş hızı (derece/saniye). 0 = anlık dönüş.")]
        [Min(0f)] public float turnSpeed = 720f;

        [Tooltip("Hareket girdisi bu eşiğin altındaysa sıfır kabul edilir (ölü bölge).")]
        [Range(0f, 0.5f)] public float moveDeadzone = 0.1f;

        [Header("Zıplama / Yerçekimi")]
        [Tooltip("Yerçekimi (birim/saniye²).")]
        public float gravity = 20f;

        [Tooltip("Zıplama gücü (birim/saniye).")]
        [Min(0f)] public float jumpPower = 7f;

        [Tooltip("Havadayken kontrol azalsın mı? (False = havada tam kontrol)")]
        public bool airControl = true;

        [Tooltip("Havadayken hareket çarpanı (airControl=true ise).")]
        [Range(0f, 1f)] public float airControlFactor = 0.5f;

        [Header("Input")]
        [Tooltip("Yeni Input System için InputActionAsset.")]
        public InputActionAsset inputActions;

        // -------------------------------------------------------------------------
        // INSPECTOR — MOUSE / CROSSHAIR
        // -------------------------------------------------------------------------
        [Header("Mouse / Crosshair")]
        [Tooltip("Crosshair (oyun içi cursor) için prefab. Boşsa runtime'da basit bir küp oluşturulur.")]
        public GameObject crosshairPrefab;

        [Tooltip("Crosshair rengi (küre/küp rengi).")]
        public Color crosshairColor = new Color(1f, 0.95f, 0.5f, 0.85f);

        [Tooltip("Crosshair boyutu.")]
        [Min(0.05f)] public float crosshairSize = 0.35f;

        [Tooltip("Mouse ray çizgisi rengi.")]
        public Color rayColor = new Color(1f, 0.9f, 0.4f, 0.6f);

        [Tooltip("Mouse ray çizgisi kalınlığı.")]
        [Min(0.005f)] public float rayWidth = 0.04f;

        [Tooltip("Ray yüksekliği (zeminden ne kadar yukarı).")]
        [Min(0f)] public float rayHeight = 0.05f;

        [Tooltip("Zemini göstermek için LayerMask (ray hangi katmana çarpar).")]
        public LayerMask groundMask = ~0;

        [Header("Debug")]
        [Tooltip("Hareket vektörünü konsola her saniye yazdır.")]
        public bool logMoveEverySecond = false;

        // -------------------------------------------------------------------------
        // DURUM
        // -------------------------------------------------------------------------
        private CharacterController _cc;
        private InputAction _moveAction;
        private InputAction _sprintAction;
        private InputAction _jumpAction;

        private Vector2 _moveInput;
        private bool _sprintHeld;
        private bool _jumpQueued;
        private float _verticalVel;
        private float _logTimer;

        // Mouse / crosshair
        private Camera _cam;
        private GameObject _crosshair;
        private Renderer _crosshairRenderer;
        private LineRenderer _rayLine;
        private Vector3 _lastGroundHit;       // en son ray'in yere değdiği nokta
        private bool _hasGroundHit;

        // Dışarıdan okunacak
        public Vector3 AimPoint => _lastGroundHit;
        public bool HasAim => _hasGroundHit;
        public Vector3 CurrentMoveVector { get; private set; }

        // Events
        public event System.Action OnAttackPressed;
        public event System.Action OnInteractPressed;
        public event System.Action OnJumped;

        // -------------------------------------------------------------------------
        // YAŞAM DÖNGÜSÜ
        // -------------------------------------------------------------------------
        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _cam = Camera.main;

            // InputAction referanslarını çöz
            if (inputActions == null)
            {
                #if UNITY_EDITOR
                inputActions = UnityEditor.AssetDatabase
                    .LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
                #endif
            }
            if (inputActions != null)
            {
                var map = inputActions.FindActionMap("Player", true);
                _moveAction = map?.FindAction("Move");
                _sprintAction = map?.FindAction("Sprint");
                _jumpAction = map?.FindAction("Jump");
            }
        }

        private void OnEnable()
        {
            if (_moveAction != null) _moveAction.Enable();
            if (_sprintAction != null) _sprintAction.Enable();
            if (_jumpAction != null) _jumpAction.Enable();

            if (_jumpAction != null) _jumpAction.performed += OnJump;
        }

        private void OnDisable()
        {
            if (_jumpAction != null) _jumpAction.performed -= OnJump;

            if (_moveAction != null) _moveAction.Disable();
            if (_sprintAction != null) _sprintAction.Disable();
            if (_jumpAction != null) _jumpAction.Disable();
        }

        private void Start()
        {
            CreateCrosshair();
            CreateRayVisual();
        }

        // -------------------------------------------------------------------------
        // CROSSHAIR + RAY
        // -------------------------------------------------------------------------
        private void CreateCrosshair()
        {
            if (crosshairPrefab != null)
            {
                _crosshair = Instantiate(crosshairPrefab);
            }
            else
            {
                // Basit bir küp oluştur (mouse ucunda görünecek)
                _crosshair = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _crosshair.name = "Crosshair";
                var col = _crosshair.GetComponent<Collider>();
                if (col != null) Destroy(col);
                _crosshair.transform.localScale = Vector3.one * crosshairSize;
                var r = _crosshair.GetComponent<Renderer>();
                if (r != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    mat.color = crosshairColor;
                    r.sharedMaterial = mat;
                }
            }
            _crosshair.SetActive(false);   // sahne yüklenene kadar gizle
        }

        private void CreateRayVisual()
        {
            var go = new GameObject("MouseAimRay");
            go.transform.SetParent(transform, false);
            _rayLine = go.AddComponent<LineRenderer>();
            _rayLine.material = new Material(Shader.Find("Sprites/Default"));
            _rayLine.startColor = rayColor;
            _rayLine.endColor = rayColor;
            _rayLine.startWidth = rayWidth;
            _rayLine.endWidth = rayWidth;
            _rayLine.positionCount = 2;
            _rayLine.useWorldSpace = true;
            _rayLine.enabled = false;
        }

        private void UpdateMouseAim()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) { _hasGroundHit = false; return; }

            Ray ray = _cam.ScreenPointToRay(LegacyInputBridge.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f, groundMask, QueryTriggerInteraction.Ignore))
            {
                _lastGroundHit = hit.point;
                _hasGroundHit = true;

                // Crosshair mouse ile aynı koordinatta
                if (_crosshair != null)
                {
                    if (!_crosshair.activeSelf) _crosshair.SetActive(true);
                    _crosshair.transform.position = hit.point + Vector3.up * 0.1f;
                }
            }
            else
            {
                _hasGroundHit = false;
                if (_crosshair != null) _crosshair.SetActive(false);
            }

            // Ray çizgisi (karakter -> crosshair)
            if (_rayLine != null && _hasGroundHit)
            {
                _rayLine.enabled = true;
                Vector3 a = transform.position + Vector3.up * 0.6f;
                Vector3 b = _lastGroundHit + Vector3.up * rayHeight;
                _rayLine.SetPosition(0, a);
                _rayLine.SetPosition(1, b);
            }
            else if (_rayLine != null)
            {
                _rayLine.enabled = false;
            }
        }

        // -------------------------------------------------------------------------
        // UPDATE
        // -------------------------------------------------------------------------
        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.Status == GameStatus.Paused) return;

            UpdateMouseAim();

            // Input oku
            if (_moveAction != null) _moveInput = _moveAction.ReadValue<Vector2>();
            _sprintHeld = _sprintAction != null && _sprintAction.IsPressed();

            // Kamera yönüne göre dünya hareketi
            Vector3 dir = Vector3.zero;
            if (_moveInput.sqrMagnitude > moveDeadzone * moveDeadzone)
            {
                if (_cam != null)
                {
                    Vector3 forward = Vector3.ProjectOnPlane(_cam.forward, Vector3.up).normalized;
                    Vector3 right = Vector3.ProjectOnPlane(_cam.right, Vector3.up).normalized;
                    dir = forward * _moveInput.y + right * _moveInput.x;
                }
                else
                {
                    dir = new Vector3(_moveInput.x, 0, _moveInput.y);
                }
            }

            // Zıplama kuyruğu
            if (_jumpQueued)
            {
                _jumpQueued = false;
            }

            // Yerçekimi + zıplama
            bool grounded = _cc != null && _cc.isGrounded;
            if (grounded && _verticalVel < 0f) _verticalVel = -1f;
            _verticalVel -= gravity * Time.deltaTime;

            // Hız
            float speedMul = _sprintHeld ? sprintMultiplier : 1f;
            float airMul = (airControl && !grounded) ? airControlFactor : 1f;
            Vector3 horizontal = dir.normalized * moveSpeed * speedMul * airMul;

            // Karakter controller
            if (_cc != null)
            {
                Vector3 motion = horizontal + Vector3.up * _verticalVel;
                _cc.Move(motion * Time.deltaTime);
            }

            // ROTASYON: mouse crosshair'e doğru
            if (_hasGroundHit && turnSpeed > 0f)
            {
                Vector3 look = _lastGroundHit - transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.001f)
                {
                    Quaternion target = Quaternion.LookRotation(look);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation, target, turnSpeed * Time.deltaTime);
                }
            }

            CurrentMoveVector = horizontal;

            // Opsiyonel log
            if (logMoveEverySecond)
            {
                _logTimer += Time.deltaTime;
                if (_logTimer > 1f)
                {
                    _logTimer = 0f;
                    if (DebugOverlay.Instance != null)
                        DebugOverlay.Instance.Log(LogCategory.Player,
                            $"Move input=({_moveInput.x:F2},{_moveInput.y:F2}) " +
                            $"grounded={grounded} sprint={_sprintHeld}", false);
                }
            }
        }

        private void OnJump(InputAction.CallbackContext ctx) => _jumpQueued = true;

        private void OnDestroy()
        {
            if (_crosshair != null) Destroy(_crosshair);
        }

        // -------------------------------------------------------------------------
        // DEBUG
        // -------------------------------------------------------------------------
        private void OnDrawGizmos()
        {
            if (_hasGroundHit)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_lastGroundHit, 0.3f);
            }
        }
    }
}
