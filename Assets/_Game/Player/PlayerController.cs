// =============================================================================
// PlayerController.cs
// -----------------------------------------------------------------------------
// MOBA/RPG + Quake/CS tarzı hareket (bunny hop destekli).
//
// HAREKET MODELİ:
//   - Yerde: WASD -> anında o hızda hareket
//   - Sprint (Left Shift): maxSpeed * sprintMultiplier
//   - Havadayken (airControl): ivmelenme uygulanır (airAccelerate)
//   - Space: yerdeyse zıpla. Zıplama anındaki yatay hız korunur (bunny hop).
//   - Top speed: maxMoveSpeed (havada da geçerli, aşılamaz).
//
// Inspector'dan:
//   - moveSpeed, sprintMultiplier
//   - turnSpeed (rotasyon)
//   - gravity, jumpPower
//   - airAccelerate: havadayken uygulanan ivme (0-30)
//   - maxMoveSpeed: ulaşılabilecek maksimum yatay hız (top speed)
//   - friction (yerde)
//   - mouse aim ray, crosshair renkleri
// ayarlanabilir.
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using GoodNightMyAngel.Core;
using GoodNightMyAngel.InputBridge;

namespace GoodNightMyAngel.Player
{
    /// <summary>
    /// Quake/CS tarzı hareket + MOBA tarzı nişan.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // INSPECTOR — HAREKET
        // -------------------------------------------------------------------------
        [Header("Hareket")]
        [Tooltip("Normal yürüme hızı (birim/saniye).")]
        [Min(0f)] public float moveSpeed = 7f;

        [Tooltip("Sprint (koşma) hızı çarpanı.")]
        [Min(1f)] public float sprintMultiplier = 1.6f;

        [Tooltip("Maksimum yatay hız (top speed). Bunny hop sırasında bu sınırı aşamaz.")]
        [Min(1f)] public float maxMoveSpeed = 8f;

        [Tooltip("Sprint ile birlikte max hız (sprintMaxSpeed, maxMoveSpeed'den büyük olabilir).")]
        [Min(1f)] public float sprintMaxSpeed = 12f;

        [Tooltip("Yerçekimi (birim/saniye²).")]
        public float gravity = 20f;

        [Tooltip("Yer friction (saniyede hız kaybı çarpanı). 0 = sürtünme yok, 10 = hızlı durma.")]
        [Range(0f, 15f)] public float groundFriction = 8f;

        [Tooltip("Havadayken ivmelenme (air accelerate). Quake stili. 0 = havada kontrol yok, 30 = çok hassas.")]
        [Range(0f, 50f)] public float airAccelerate = 12f;

        [Tooltip("Zıplama gücü (birim/saniye).")]
        [Min(0f)] public float jumpPower = 7.5f;

        [Tooltip("Hareket girdisi bu eşiğin altındaysa sıfır kabul edilir.")]
        [Range(0f, 0.5f)] public float moveDeadzone = 0.1f;

        [Header("Rotasyon")]
        [Tooltip("Dönüş hızı (derece/saniye). 0 = anlık.")]
        [Min(0f)] public float turnSpeed = 720f;

        [Header("Silah")]
        [Tooltip("Bu objeye eklenen Weapon component'ine otomatik ateş eder. " +
                 "Boşsa runtime'da bir tane eklenir.")]
        public Weapon weapon;

        [Tooltip("R tuşu reload tetikler (Weapon üzerinden).")]
        public KeyCode reloadKey = KeyCode.R;

        // -------------------------------------------------------------------------
        // INSPECTOR — MOUSE / CROSSHAIR
        // -------------------------------------------------------------------------
        [Header("Mouse / Crosshair")]
        public GameObject crosshairPrefab;
        public Color crosshairColor = new Color(1f, 0.95f, 0.5f, 0.85f);
        [Min(0.05f)] public float crosshairSize = 0.35f;

        public Color rayColor = new Color(1f, 0.9f, 0.4f, 0.6f);
        [Min(0.005f)] public float rayWidth = 0.04f;

        [Tooltip("Aim ray ekran alanından çıktıktan sonra ne kadar uzatılsın (dünya birimi).")]
        [Min(5f)] public float rayOffscreenExtension = 50f;

        [Tooltip("Zemini göstermek için LayerMask.")]
        public LayerMask groundMask = ~0;

        [Header("Debug")]
        public bool logMoveEverySecond = false;

        // -------------------------------------------------------------------------
        // DURUM
        // -------------------------------------------------------------------------
        private CharacterController _cc;
        private InputAction _moveAction;
        private InputAction _sprintAction;
        private InputAction _jumpAction;
        private InputAction _attackAction;
        private InputAction _interactAction;

        private Vector2 _moveInput;
        private bool _sprintHeld;
        private bool _jumpQueued;
        private float _verticalVel;
        private float _logTimer;

        // Yatay hız (xz düzleminde)
        private Vector3 _horizontalVel = Vector3.zero;
        public Vector3 HorizontalVelocity => _horizontalVel;
        public float CurrentSpeed => _horizontalVel.magnitude;

        // Mouse / crosshair
        private Camera _cam;
        private GameObject _crosshair;
        private LineRenderer _rayLine;
        private Vector3 _lastGroundHit;
        private bool _hasGroundHit;

        public Vector3 AimPoint => _lastGroundHit;
        public bool HasAim => _hasGroundHit;
        public Vector3 CurrentMoveVector => _horizontalVel;

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
                _attackAction = map?.FindAction("Attack");
                _interactAction = map?.FindAction("Interact");
            }
        }

        private void OnEnable()
        {
            if (_moveAction != null) _moveAction.Enable();
            if (_sprintAction != null) _sprintAction.Enable();
            if (_jumpAction != null) _jumpAction.Enable();
            if (_attackAction != null) _attackAction.Enable();
            if (_interactAction != null) _interactAction.Enable();

            if (_jumpAction != null) _jumpAction.performed += OnJump;
            if (_attackAction != null) _attackAction.performed += OnAttack;
            if (_interactAction != null) _interactAction.performed += OnInteract;
        }

        private void OnDisable()
        {
            if (_jumpAction != null) _jumpAction.performed -= OnJump;
            if (_attackAction != null) _attackAction.performed -= OnAttack;
            if (_interactAction != null) _interactAction.performed -= OnInteract;

            if (_moveAction != null) _moveAction.Disable();
            if (_sprintAction != null) _sprintAction.Disable();
            if (_jumpAction != null) _jumpAction.Disable();
            if (_attackAction != null) _attackAction.Disable();
            if (_interactAction != null) _interactAction.Disable();
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
            _crosshair.SetActive(false);
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
            if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask, QueryTriggerInteraction.Ignore))
            {
                _lastGroundHit = hit.point;
                _hasGroundHit = true;
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

            // Aim ray: sonsuz uzanır (ekran dışında da devam eder, performans için
            // belli bir mesafede kesilir)
            if (_rayLine != null)
            {
                _rayLine.enabled = true;
                Vector3 a = transform.position + Vector3.up * 0.6f;
                Vector3 b;

                if (_hasGroundHit)
                {
                    b = _lastGroundHit + Vector3.up * 0.05f;
                }
                else
                {
                    // Eğer yere değmiyorsa, mouse yönünde ileriye doğru uzat
                    Vector3 screenPoint = LegacyInputBridge.mousePosition;
                    // Ekran dışındaysa da yönü hesapla
                    Vector3 dir3D = ray.direction.normalized;
                    // Eğer mouse ekran sınırları dışındaysa, yönü daha da uzat
                    b = a + dir3D * rayOffscreenExtension;
                }

                _rayLine.SetPosition(0, a);
                _rayLine.SetPosition(1, b);
            }
        }

        // -------------------------------------------------------------------------
        // HAREKET — Quake / CS tarzı
        // -------------------------------------------------------------------------
        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.Status == GameStatus.Paused) return;

            UpdateMouseAim();

            // ATEŞ (sol tık) — LegacyInputBridge üzerinden
            if (LegacyInputBridge.GetKeyDown(KeyCode.Mouse0))
            {
                if (weapon != null) weapon.TryFire();
            }
            // Reload (R)
            if (LegacyInputBridge.GetKeyDown(reloadKey))
            {
                if (weapon != null) weapon.StartReload();
            }

            // Input oku
            if (_moveAction != null) _moveInput = _moveAction.ReadValue<Vector2>();
            _sprintHeld = _sprintAction != null && _sprintAction.IsPressed();

            // Hedef yön (kamera yönüne göre)
            Vector3 wishDir = ComputeWishDirection();

            // Yatay hız işlemleri
            bool grounded = _cc != null && _cc.isGrounded;

            if (grounded)
            {
                // Yerde: friction uygula
                ApplyFriction();
                // Yatay hızı hedef yöne doğru ekle (anında ivmelenme)
                if (wishDir.sqrMagnitude > 0.01f)
                {
                    float maxSpeed = GetMaxSpeed();
                    Accelerate(wishDir, maxSpeed, 10f);   // yerde yüksek accel
                }
                // Yere değince yatay hızı sınırla
                ClampSpeed(GetMaxSpeed());
            }
            else
            {
                // Havada: air accelerate
                if (wishDir.sqrMagnitude > 0.01f)
                    Accelerate(wishDir, GetMaxSpeed(), airAccelerate);
                // Havadayken de max hız sınırı (bunny hop hız limiti)
                ClampSpeed(GetMaxSpeed());
            }

            // Zıplama (yerdeyse)
            if (grounded && _jumpQueued)
            {
                _verticalVel = jumpPower;
                _jumpQueued = false;
                OnJumped?.Invoke();
                if (DebugOverlay.Instance != null)
                    DebugOverlay.Instance.Log(LogCategory.Player, "Zıpladı!", false);
            }

            // Yerçekimi
            if (grounded && _verticalVel < 0f) _verticalVel = -1f;
            _verticalVel -= gravity * Time.deltaTime;

            // Character controller hareket
            if (_cc != null)
            {
                Vector3 motion = _horizontalVel + Vector3.up * _verticalVel;
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

            // Opsiyonel log
            if (logMoveEverySecond)
            {
                _logTimer += Time.deltaTime;
                if (_logTimer > 1f)
                {
                    _logTimer = 0f;
                    if (DebugOverlay.Instance != null)
                        DebugOverlay.Instance.Log(LogCategory.Player,
                            $"Speed={_horizontalVel.magnitude:F1} " +
                            $"grounded={grounded} sprint={_sprintHeld}", false);
                }
            }
        }

        private Vector3 ComputeWishDirection()
        {
            if (_moveInput.sqrMagnitude < moveDeadzone * moveDeadzone)
                return Vector3.zero;

            Vector3 dir;
            if (_cam != null)
            {
                Vector3 forward = Vector3.ProjectOnPlane(_cam.transform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(_cam.transform.right, Vector3.up).normalized;
                dir = forward * _moveInput.y + right * _moveInput.x;
            }
            else
            {
                dir = new Vector3(_moveInput.x, 0, _moveInput.y);
            }
            dir.y = 0f;
            return dir.normalized;
        }

        private float GetMaxSpeed()
        {
            return _sprintHeld ? sprintMaxSpeed : maxMoveSpeed;
        }

        // Quake tarzı accelerate: hedef yöne doğru mevcut hızı ekler (kısa zaman içinde)
        private void Accelerate(Vector3 wishDir, float wishSpeed, float accel)
        {
            float currentSpeedInDir = Vector3.Dot(_horizontalVel, wishDir);
            float addSpeed = Mathf.Clamp(wishSpeed - currentSpeedInDir, 0f, accel * Time.deltaTime);
            _horizontalVel += wishDir * addSpeed;
        }

        // Friction (yerde)
        private void ApplyFriction()
        {
            float speed = _horizontalVel.magnitude;
            if (speed < 0.01f) { _horizontalVel = Vector3.zero; return; }

            float drop = speed * groundFriction * Time.deltaTime;
            float newSpeed = Mathf.Max(0f, speed - drop);
            _horizontalVel *= (newSpeed / speed);
        }

        // Top speed clamp (havada da geçerli)
        private void ClampSpeed(float max)
        {
            float sp = _horizontalVel.magnitude;
            if (sp > max)
                _horizontalVel *= max / sp;
        }

        private void OnJump(InputAction.CallbackContext ctx) => _jumpQueued = true;
        private void OnAttack(InputAction.CallbackContext ctx) => OnAttackPressed?.Invoke();
        private void OnInteract(InputAction.CallbackContext ctx) => OnInteractPressed?.Invoke();

        private void OnDestroy()
        {
            if (_crosshair != null) Destroy(_crosshair);
        }

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
