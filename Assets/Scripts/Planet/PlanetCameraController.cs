using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LittlePlanet.PlanetSystem
{
    public enum MouseDragButton
    {
        Left = 0,
        Right = 1,
        Middle = 2
    }

    [DisallowMultipleComponent]
    public sealed class PlanetCameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform planetRoot;
        [SerializeField] private Camera controlledCamera;

        [Header("Zoom")]
        [SerializeField, Min(0.1f)] private float minDistance = 4f;
        [SerializeField, Min(0.1f)] private float maxDistance = 30f;
        [SerializeField, Min(0.001f)] private float zoomSpeed = 1f;
        [SerializeField, Min(0.0001f)] private float inputSystemScrollScale = 0.01f;
        [SerializeField, Min(0.01f)] private float keyboardZoomStep = 0.75f;

        [Header("Rotation")]
        [SerializeField] private MouseDragButton dragButton = MouseDragButton.Right;
        [SerializeField, Min(0.01f)] private float rotationSpeed = 0.25f;
        [SerializeField] private bool invertHorizontal;
        [SerializeField] private bool invertVertical;

        private float _currentDistance;
        private Vector3 _cameraDirection;
        private Coroutine _focusRoutine;
        private Coroutine _shakeRoutine;
        private Vector3 _shakeOffset;

        public bool ZoomEnabled { get; set; } = true;

        private void Awake()
        {
            ResolveReferences();
            InitializeFromCurrentCameraState();
        }

        private void OnValidate()
        {
            minDistance = Mathf.Max(0.1f, minDistance);
            maxDistance = Mathf.Max(minDistance, maxDistance);
            zoomSpeed = Mathf.Max(0.001f, zoomSpeed);
            inputSystemScrollScale = Mathf.Max(0.0001f, inputSystemScrollScale);
            keyboardZoomStep = Mathf.Max(0.01f, keyboardZoomStep);
            rotationSpeed = Mathf.Max(0.01f, rotationSpeed);
        }

        private void Update()
        {
            if (!ResolveReferences())
            {
                return;
            }

            HandleZoom();
            HandleRotation();
        }

        private bool ResolveReferences()
        {
            if (planetRoot == null)
            {
                planetRoot = transform;
            }

            if (controlledCamera == null)
            {
                controlledCamera = Camera.main;
            }

            return planetRoot != null && controlledCamera != null;
        }

        private void InitializeFromCurrentCameraState()
        {
            if (!ResolveReferences())
            {
                return;
            }

            var offset = controlledCamera.transform.position - planetRoot.position;
            if (offset.sqrMagnitude < 0.0001f)
            {
                offset = -controlledCamera.transform.forward;
            }

            var initialDistance = offset.magnitude;
            if (initialDistance > maxDistance)
            {
                maxDistance = initialDistance;
            }

            _cameraDirection = offset.normalized;
            _currentDistance = Mathf.Clamp(initialDistance, minDistance, maxDistance);
            ApplyCameraDistance();
        }

        private void HandleZoom()
        {
            if (!ZoomEnabled)
            {
                return;
            }

            if (!TryGetScrollDelta(out var scrollDelta))
            {
                return;
            }

            var normalizedDelta = NormalizeScrollDelta(scrollDelta);
            _currentDistance = Mathf.Clamp(_currentDistance - normalizedDelta * zoomSpeed, minDistance, maxDistance);
            ApplyCameraDistance();
        }

        private void HandleRotation()
        {
            if (!TryGetDragDelta(out var dragDelta))
            {
                return;
            }

            var horizontalSign = invertHorizontal ? -1f : 1f;
            var verticalSign = invertVertical ? -1f : 1f;

            var yaw = dragDelta.x * rotationSpeed * horizontalSign;
            var pitch = dragDelta.y * rotationSpeed * verticalSign;
            var yawRotation = Quaternion.AngleAxis(yaw, Vector3.up);
            var nextDirection = yawRotation * _cameraDirection;

            var pitchAxis = Vector3.Cross(Vector3.up, nextDirection);
            if (pitchAxis.sqrMagnitude > 0.000001f)
            {
                pitchAxis.Normalize();
                var pitchRotation = Quaternion.AngleAxis(pitch, pitchAxis);
                nextDirection = pitchRotation * nextDirection;
            }

            var normalizedDirection = nextDirection.sqrMagnitude > 0.000001f
                ? nextDirection.normalized
                : _cameraDirection;
            var verticalDot = Mathf.Abs(Vector3.Dot(normalizedDirection, Vector3.up));
            if (verticalDot < 0.995f)
            {
                _cameraDirection = normalizedDirection;
            }

            ApplyCameraDistance();
        }

        private void ApplyCameraDistance()
        {
            if (controlledCamera == null || planetRoot == null)
            {
                return;
            }

            controlledCamera.transform.position = planetRoot.position + _cameraDirection * _currentDistance + _shakeOffset;
            controlledCamera.transform.LookAt(planetRoot.position);
        }

        public void FocusOnLocalDirection(Vector3 localDirection, float targetDistance, float durationSeconds)
        {
            if (!ResolveReferences() || localDirection.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            RefreshCameraStateFromCurrentPosition();
            if (_focusRoutine != null)
            {
                StopCoroutine(_focusRoutine);
            }

            _focusRoutine = StartCoroutine(FocusRoutine(localDirection.normalized, targetDistance, durationSeconds));
        }

        private System.Collections.IEnumerator FocusRoutine(Vector3 localDirection, float targetDistance, float durationSeconds)
        {
            var startRotation = planetRoot.rotation;
            var startDistance = _currentDistance;
            var desiredWorldDirection = _cameraDirection.sqrMagnitude > 0.000001f ? _cameraDirection.normalized : (controlledCamera.transform.position - planetRoot.position).normalized;
            var currentWorldDirection = planetRoot.TransformDirection(localDirection);
            var targetRotation = Quaternion.FromToRotation(currentWorldDirection, desiredWorldDirection) * planetRoot.rotation;
            if (targetDistance > maxDistance)
            {
                maxDistance = targetDistance;
            }

            var clampedDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
            var duration = Mathf.Max(0.01f, durationSeconds);
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var easedT = t * t * (3f - 2f * t);
                planetRoot.rotation = Quaternion.Slerp(startRotation, targetRotation, easedT);
                _currentDistance = Mathf.Lerp(startDistance, clampedDistance, easedT);
                ApplyCameraDistance();
                yield return null;
            }

            planetRoot.rotation = targetRotation;
            _currentDistance = clampedDistance;
            ApplyCameraDistance();
            _focusRoutine = null;
        }

        public void ShakeCamera(float durationSeconds, float strength)
        {
            ShakeCamera(durationSeconds, strength, 4f);
        }

        public void ShakeCamera(float durationSeconds, float strength, float frequency)
        {
            if (!ResolveReferences() || durationSeconds <= 0f || strength <= 0f)
            {
                return;
            }

            if (_shakeRoutine != null)
            {
                StopCoroutine(_shakeRoutine);
            }

            _shakeRoutine = StartCoroutine(ShakeRoutine(durationSeconds, strength, frequency));
        }

        private System.Collections.IEnumerator ShakeRoutine(float durationSeconds, float strength, float frequency)
        {
            var duration = Mathf.Max(0.01f, durationSeconds);
            var safeFrequency = Mathf.Max(0.1f, frequency);
            var seed = UnityEngine.Random.Range(0f, 1000f);
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var fade = 1f - Mathf.Clamp01(elapsed / duration);
                var time = elapsed * safeFrequency;
                var x = Mathf.PerlinNoise(seed, time) * 2f - 1f;
                var y = Mathf.PerlinNoise(seed + 13.7f, time) * 2f - 1f;
                var z = Mathf.PerlinNoise(seed + 29.3f, time) * 2f - 1f;
                _shakeOffset = new Vector3(x, y, z) * (strength * fade);
                ApplyCameraDistance();
                yield return null;
            }

            _shakeOffset = Vector3.zero;
            ApplyCameraDistance();
            _shakeRoutine = null;
        }

        private void RefreshCameraStateFromCurrentPosition()
        {
            if (controlledCamera == null || planetRoot == null)
            {
                return;
            }

            var offset = controlledCamera.transform.position - planetRoot.position;
            if (offset.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            _cameraDirection = offset.normalized;
            _currentDistance = Mathf.Clamp(offset.magnitude, minDistance, maxDistance);
        }

        private bool TryGetScrollDelta(out float value)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                value = Mouse.current.scroll.ReadValue().y * inputSystemScrollScale;
                if (Mathf.Abs(value) > Mathf.Epsilon)
                {
                    return true;
                }
            }
#endif

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.equalsKey.wasPressedThisFrame || Keyboard.current.numpadPlusKey.wasPressedThisFrame)
                {
                    value = 1f;
                    return true;
                }

                if (Keyboard.current.minusKey.wasPressedThisFrame || Keyboard.current.numpadMinusKey.wasPressedThisFrame)
                {
                    value = -1f;
                    return true;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            value = Input.mouseScrollDelta.y;
            if (Mathf.Abs(value) > Mathf.Epsilon)
            {
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
            {
                value = 1f;
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                value = -1f;
                return true;
            }
#endif

            value = 0f;
            return false;
        }

        private float NormalizeScrollDelta(float rawDelta)
        {
            if (Mathf.Abs(rawDelta - 1f) < 0.001f || Mathf.Abs(rawDelta + 1f) < 0.001f)
            {
                return rawDelta * keyboardZoomStep;
            }

            var clamped = Mathf.Clamp(rawDelta, -10f, 10f);
            var distanceFactor = Mathf.Max(1f, _currentDistance * 0.05f);
            return clamped * distanceFactor;
        }

        private bool TryGetDragDelta(out Vector2 value)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && IsInputSystemDragPressed())
            {
                value = Mouse.current.delta.ReadValue();
                if (value.sqrMagnitude > 0f)
                {
                    return true;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButton((int)dragButton))
            {
                value = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
                if (value.sqrMagnitude > 0f)
                {
                    return true;
                }
            }
#endif

            value = Vector2.zero;
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        private bool IsInputSystemDragPressed()
        {
            return dragButton switch
            {
                MouseDragButton.Left => Mouse.current.leftButton.isPressed,
                MouseDragButton.Middle => Mouse.current.middleButton.isPressed,
                _ => Mouse.current.rightButton.isPressed
            };
        }
#endif
    }
}
