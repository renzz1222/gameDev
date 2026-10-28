using UnityEngine;
using UnityEngine.InputSystem;

namespace BawalUmihiMaze
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerCubeController : MonoBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 6.0f;
        public float sprintSpeed = 10.0f;
        public float jumpSpeed = 5.2f;
        public float gravity = -19.62f;
        public float turnSmoothSpeed = 14f;

        [Header("Camera Follow & Look")]
        public Camera followCamera;
        public Transform cubeVisual;
        public float mouseSensitivity = 0.16f;
        public float cameraPivotHeight = 1.35f;
        public float cameraDistance = 2.4f;
        public float minPitch = -25f;
        public float maxPitch = 65f;

        private CharacterController _controller;
        private float _yaw;
        private float _pitch = 14f;
        private float _verticalVelocity;
        private float _currentCamDist;
        private readonly RaycastHit[] _camHits = new RaycastHit[8];

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (followCamera == null)
                followCamera = Camera.main;

            if (cubeVisual == null && transform.childCount > 0)
                cubeVisual = transform.GetChild(0);

            _yaw = transform.eulerAngles.y;
            _currentCamDist = cameraDistance;
        }

        private void Start()
        {
            LockCursor(true);
        }

        private void Update()
        {
            HandleCursorToggle();
            HandleLookInput();
            HandleMovement();
        }

        private void LateUpdate()
        {
            UpdateCameraFollow();
        }

        private void HandleCursorToggle()
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                LockCursor(false);
            }
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor(true);
            }
        }

        private void HandleLookInput()
        {
            if (Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                _yaw += delta.x * mouseSensitivity;
                _pitch = Mathf.Clamp(_pitch - delta.y * mouseSensitivity, minPitch, maxPitch);
            }

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.rightStick.ReadValue();
                if (stick.sqrMagnitude > 0.01f)
                {
                    _yaw += stick.x * 120f * Time.deltaTime;
                    _pitch = Mathf.Clamp(_pitch - stick.y * 90f * Time.deltaTime, minPitch, maxPitch);
                }
            }
        }

        private void HandleMovement()
        {
            Vector2 moveInput = Vector2.zero;
            bool sprinting = false;
            bool jumpPressed = false;

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) moveInput.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) moveInput.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) moveInput.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) moveInput.x -= 1f;

                sprinting = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                jumpPressed = kb.spaceKey.wasPressedThisFrame;
            }

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > moveInput.sqrMagnitude)
                    moveInput = stick;
                if (pad.leftStickButton.isPressed || pad.rightShoulder.isPressed)
                    sprinting = true;
                if (pad.buttonSouth.wasPressedThisFrame)
                    jumpPressed = true;
            }

            if (moveInput.sqrMagnitude > 1f)
                moveInput.Normalize();

            Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 moveDir = yawRot * new Vector3(moveInput.x, 0f, moveInput.y);
            float speed = sprinting ? sprintSpeed : walkSpeed;

            if (_controller.isGrounded)
            {
                if (_verticalVelocity < 0f)
                    _verticalVelocity = -2f;

                if (jumpPressed)
                    _verticalVelocity = jumpSpeed;
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 velocity = moveDir * speed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);

            // Rotate the cube smoothly toward movement direction
            if (moveDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                if (cubeVisual != null)
                    cubeVisual.rotation = Quaternion.Slerp(cubeVisual.rotation, targetRot, turnSmoothSpeed * Time.deltaTime);
                else
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSmoothSpeed * Time.deltaTime);
            }
        }

        private void UpdateCameraFollow()
        {
            if (followCamera == null)
                return;

            Vector3 pivot = transform.position + Vector3.up * cameraPivotHeight;
            Quaternion camRot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 backDir = camRot * Vector3.back;

            // SphereCastNonAlloc against maze walls (ignoring Player itself) so camera never clips behind walls
            float targetDist = cameraDistance;
            int hitCount = Physics.SphereCastNonAlloc(
                pivot,
                0.20f,
                backDir,
                _camHits,
                cameraDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Transform ht = _camHits[i].transform;
                if (ht == null || ht == transform || ht.IsChildOf(transform))
                    continue;

                float clamped = Mathf.Max(0.30f, _camHits[i].distance - 0.12f);
                if (clamped < targetDist)
                    targetDist = clamped;
            }

            _currentCamDist = targetDist < _currentCamDist
                ? targetDist
                : Mathf.Lerp(_currentCamDist, targetDist, 16f * Time.deltaTime);

            followCamera.transform.SetPositionAndRotation(pivot + backDir * _currentCamDist, camRot);
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
