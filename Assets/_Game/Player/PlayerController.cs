// =============================================================================
// PlayerController.cs
// -----------------------------------------------------------------------------
// Yeni Input System (com.unity.inputsystem 1.19) kullanarak oyuncuyu kontrol
// eder. Inspector'dan:
//   - Hareket hızı
//   - Dönüş hızı
//   - Referans alınacak InputActionAsset
//   - Kamera referansı (yön hesabı için)
// ayarlanabilir.
//
// Tuş atamaları InputSystem_Actions.inputactions içindeki "Player" action
// map'inden gelir (Move, Look, Attack, Interact, Sprint, Pause).
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using GoodNightMyAngel.Core;

namespace GoodNightMyAngel.Player
{
    /// <summary>
    /// Oyuncuyu kontrol eden ana script. Hem gündüz bedeni hem de gece ruh
    /// formu aynı scripti kullanır; sahipleri (gündüz mü, gece mi) farklı
    /// alt-sistemleri aktive eder.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // INSPECTOR
        // -------------------------------------------------------------------------
        [Header("Referanslar")]
        [Tooltip("Yeni Input System için InputActionAsset. Boş bırakılırsa kendisi yükler.")]
        public InputActionAsset inputActions;

        [Tooltip("Hareket yönü hesaplamak için kamera. Boşsa Camera.main kullanılır.")]
        public Transform cameraTransform;

        [Header("Hareket")]
        [Tooltip("Normal yürüme hızı (birim/saniye).")]
        [Min(0f)] public float moveSpeed = 4f;

        [Tooltip("Sprint (koşma) hızı çarpanı.")]
        [Min(1f)] public float sprintMultiplier = 1.6f;

        [Tooltip("Dönüş hızı (derece/saniye). 0 = anlık dönüş.")]
        [Min(0f)] public float turnSpeed = 720f;

        [Tooltip("Yerçekimi (birim/saniye²). 0 = yerçekimi yok.")]
        public float gravity = 20f;

        [Header("Salınım/Hareket Hassasiyeti")]
        [Tooltip("Hareket girdisi bu eşiğin altındaysa sıfır kabul edilir (ölü bölge).")]
        [Range(0f, 0.5f)] public float moveDeadzone = 0.1f;

        [Header("Debug")]
        [Tooltip("Hareket vektörünü sahnede göster.")]
        public bool drawMovementGizmo = true;

        [Tooltip("Hareket vektörünü konsola her saniye yazdır.")]
        public bool logMoveEverySecond = false;

        // -------------------------------------------------------------------------
        // DURUM
        // -------------------------------------------------------------------------
        private CharacterController _cc;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _sprintAction;
        private InputAction _attackAction;
        private InputAction _interactAction;

        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private bool _sprintHeld;
        private float _verticalVel;
        private float _logTimer;

        // Dışarıdan dinlenecek eventler
        public event System.Action OnAttackPressed;
        public event System.Action OnInteractPressed;

        public Vector3 CurrentMoveVector { get; private set; }

        // -------------------------------------------------------------------------
        // YAŞAM DÖNGÜSÜ
        // -------------------------------------------------------------------------
        private void Awake()
        {
            _cc = GetComponent<CharacterController>();

            // InputAction referanslarını çöz
            if (inputActions == null)
            {
                // Resources klasöründen veya Assets'ten yüklemeyi dene
                #if UNITY_EDITOR
                inputActions = UnityEditor.AssetDatabase
                    .LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
                #endif
            }

            if (inputActions != null)
            {
                var map = inputActions.FindActionMap("Player", true);
                _moveAction = map.FindAction("Move");
                _lookAction = map.FindAction("Look");
                _sprintAction = map.FindAction("Sprint");
                _attackAction = map.FindAction("Attack");
                _interactAction = map.FindAction("Interact");
            }
        }

        private void OnEnable()
        {
            if (_moveAction != null) _moveAction.Enable();
            if (_lookAction != null) _lookAction.Enable();
            if (_sprintAction != null) _sprintAction.Enable();
            if (_attackAction != null) _attackAction.Enable();
            if (_interactAction != null) _interactAction.Enable();

            if (_attackAction != null) _attackAction.performed += OnAttack;
            if (_interactAction != null) _interactAction.performed += OnInteract;
            if (_sprintAction != null) _sprintAction.performed += ctx => _sprintHeld = true;
            if (_sprintAction != null) _sprintAction.canceled += ctx => _sprintHeld = false;
        }

        private void OnDisable()
        {
            if (_attackAction != null) _attackAction.performed -= OnAttack;
            if (_interactAction != null) _interactAction.performed -= OnInteract;

            if (_moveAction != null) _moveAction.Disable();
            if (_lookAction != null) _lookAction.Disable();
            if (_sprintAction != null) _sprintAction.Disable();
            if (_attackAction != null) _attackAction.Disable();
            if (_interactAction != null) _interactAction.Disable();
        }

        private void Update()
        {
            // Input oku
            if (_moveAction != null) _moveInput = _moveAction.ReadValue<Vector2>();
            if (_lookAction != null) _lookInput = _lookAction.ReadValue<Vector2>();

            // Kamera referansı
            Transform cam = cameraTransform != null
                ? cameraTransform
                : (Camera.main != null ? Camera.main.transform : null);

            // Hareket yönünü hesapla
            Vector3 dir = Vector3.zero;
            if (_moveInput.sqrMagnitude > moveDeadzone * moveDeadzone)
            {
                if (cam != null)
                {
                    // Kamera düzlemine göre yön
                    Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                    Vector3 right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
                    dir = forward * _moveInput.y + right * _moveInput.x;
                }
                else
                {
                    dir = new Vector3(_moveInput.x, 0, _moveInput.y);
                }
            }

            float speed = moveSpeed * (_sprintHeld ? sprintMultiplier : 1f);
            Vector3 horizontal = dir.normalized * speed;

            // Yerçekimi
            if (_cc != null && _cc.isGrounded && _verticalVel < 0f) _verticalVel = -1f;
            _verticalVel -= gravity * Time.deltaTime;

            Vector3 motion = horizontal + Vector3.up * _verticalVel;
            if (_cc != null) _cc.Move(motion * Time.deltaTime);

            // Dönüş
            if (dir.sqrMagnitude > 0.01f && turnSpeed > 0f)
            {
                Quaternion target = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, target, turnSpeed * Time.deltaTime);
            }

            CurrentMoveVector = horizontal;

            // Opsiyonel periyodik log
            if (logMoveEverySecond)
            {
                _logTimer += Time.deltaTime;
                if (_logTimer > 1f)
                {
                    _logTimer = 0f;
                    if (DebugOverlay.Instance != null)
                        DebugOverlay.Instance.Log(LogCategory.Player,
                            $"Hareket: input=({_moveInput.x:F2},{_moveInput.y:F2}) " +
                            $"world={horizontal.magnitude:F2} sprint={_sprintHeld}", false);
                }
            }
        }

        private void OnAttack(InputAction.CallbackContext ctx) => OnAttackPressed?.Invoke();
        private void OnInteract(InputAction.CallbackContext ctx) => OnInteractPressed?.Invoke();

        private void OnDrawGizmosSelected()
        {
            if (!drawMovementGizmo) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position + Vector3.up,
                CurrentMoveVector != Vector3.zero ? CurrentMoveVector.normalized * 2f : Vector3.forward * 2f);
        }
    }
}
