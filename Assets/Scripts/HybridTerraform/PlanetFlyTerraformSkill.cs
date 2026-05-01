using LittlePlanet.PlanetSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LittlePlanet.HybridTerraform
{
    [DisallowMultipleComponent]
    public sealed class PlanetFlyTerraformSkill : MonoBehaviour
    {
        private enum SkillState
        {
            Ready,
            Aiming,
            Approaching,
            Flying,
            Cooldown
        }

        [Header("References")]
        [SerializeField] private Planet planet;
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private PlanetCameraController orbitCameraController;
        [SerializeField] private Button activateButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Skill")]
        [SerializeField, Min(1f)] private float flightDuration = 60f;
        [SerializeField, Min(0f)] private float cooldownDuration = 15f;
        [SerializeField, Min(0.1f)] private float approachSpeed = 12f;
        [SerializeField, Min(0.05f)] private float approachStopDistance = 0.25f;

        [Header("Flight")]
        [SerializeField] private bool autoForward = true;
        [SerializeField, Min(0f)] private float forwardSpeed = 4f;
        [SerializeField, Min(0f)] private float strafeSpeed = 4f;
        [SerializeField, Min(0f)] private float escapeSpeed = 8f;
        [SerializeField, Min(0.01f)] private float lookSensitivity = 0.12f;
        [SerializeField, Min(0.05f)] private float hoverAltitude = 0.8f;
        [SerializeField, Min(0.1f)] private float magnetRange = 2.5f;
        [SerializeField, Min(0.5f)] private float detachDistance = 8f;
        [SerializeField, Range(0.01f, 1f)] private float surfaceAlignLerp = 0.2f;

        [Header("Terraforming")]
        [SerializeField, Range(1, 12)] private int terraformRadius = 2;
        [SerializeField, Min(0f)] private float terraformPowerPerSecond = 0.18f;
        [SerializeField, Min(0f)] private float currencyPerCompletedTile = 25f;

        private SkillState _state = SkillState.Ready;
        private float _flightEndTime;
        private float _cooldownEndTime;
        private float _pendingCurrency;
        private Vector3 _savedCameraPosition;
        private Quaternion _savedCameraRotation;
        private Tile _approachTile;

        private void Awake()
        {
            ResolveReferences();
            BindButton();
        }

        private void OnEnable()
        {
            ResolveReferences();
            BindButton();

            if (planet != null)
            {
                planet.TileClicked += HandlePlanetTileClicked;
            }
        }

        private void OnDisable()
        {
            if (planet != null)
            {
                planet.TileClicked -= HandlePlanetTileClicked;
            }

            UnbindButton();
            SetOrbitCameraEnabled(true);
        }

        private void Update()
        {
            ResolveReferences();

            switch (_state)
            {
                case SkillState.Approaching:
                    UpdateApproach();
                    break;
                case SkillState.Flying:
                    UpdateFlight();
                    break;
                case SkillState.Cooldown:
                    if (Time.time >= _cooldownEndTime)
                    {
                        _state = SkillState.Ready;
                    }
                    break;
            }

            UpdateButtonState();
            UpdateStatusText();
        }

        public void ActivateSkill()
        {
            if (_state != SkillState.Ready || Time.time < _cooldownEndTime)
            {
                return;
            }

            _state = SkillState.Aiming;
            UpdateStatusText();
        }

        public void CancelSkill()
        {
            if (_state == SkillState.Approaching || _state == SkillState.Flying)
            {
                FinishFlight(restoreCamera: true);
                return;
            }

            if (_state == SkillState.Aiming)
            {
                _state = SkillState.Ready;
            }
        }

        private void HandlePlanetTileClicked(Tile tile)
        {
            if (_state != SkillState.Aiming || tile == null || planet == null || controlledCamera == null)
            {
                return;
            }

            _savedCameraPosition = controlledCamera.transform.position;
            _savedCameraRotation = controlledCamera.transform.rotation;
            _flightEndTime = Time.time + flightDuration;
            _cooldownEndTime = _flightEndTime + cooldownDuration;
            _pendingCurrency = 0f;
            _approachTile = tile;
            SetOrbitCameraEnabled(false);

            _state = SkillState.Approaching;
        }

        private void UpdateApproach()
        {
            if (planet == null || controlledCamera == null)
            {
                FinishFlight(restoreCamera: true);
                return;
            }

            if (Time.time >= _flightEndTime)
            {
                FinishFlight(restoreCamera: true);
                return;
            }

            if (_approachTile == null)
            {
                FinishFlight(restoreCamera: true);
                return;
            }

            var targetPosition = GetFlyTargetPosition(_approachTile);
            controlledCamera.transform.position = Vector3.MoveTowards(
                controlledCamera.transform.position,
                targetPosition,
                approachSpeed * Time.deltaTime);

            LookAlongSurface(targetPosition - planet.transform.position);

            if (Vector3.Distance(controlledCamera.transform.position, targetPosition) <= approachStopDistance)
            {
                _state = SkillState.Flying;
            }
        }

        private void UpdateFlight()
        {
            if (planet == null || controlledCamera == null)
            {
                FinishFlight(restoreCamera: true);
                return;
            }

            if (Time.time >= _flightEndTime)
            {
                FinishFlight(restoreCamera: true);
                return;
            }

            RotateCameraIfRequested();
            MoveCamera();
            if (_state != SkillState.Flying)
            {
                return;
            }

            MagnetizeToSurface();
            TerraformNearCamera();
        }

        private Vector3 GetFlyTargetPosition(Tile tile)
        {
            var surfacePosition = planet.GetTileWorldSurfaceCenter(tile);
            var normal = (surfacePosition - planet.transform.position).normalized;
            return surfacePosition + normal * hoverAltitude;
        }

        private void RotateCameraIfRequested()
        {
            if (!TryGetLookDelta(out var delta))
            {
                return;
            }

            var cameraTransform = controlledCamera.transform;
            var up = GetPlanetUp(cameraTransform.position);
            var yaw = Quaternion.AngleAxis(delta.x * lookSensitivity, up);
            var pitch = Quaternion.AngleAxis(-delta.y * lookSensitivity, cameraTransform.right);
            cameraTransform.rotation = yaw * pitch * cameraTransform.rotation;
        }

        private void MoveCamera()
        {
            var cameraTransform = controlledCamera.transform;
            var up = GetPlanetUp(cameraTransform.position);
            var velocity = Vector3.zero;

            if (autoForward)
            {
                velocity += ProjectOnSurface(cameraTransform.forward, up) * forwardSpeed;
            }

            if (TryGetMoveInput(out var moveInput))
            {
                var forward = ProjectOnSurface(cameraTransform.forward, up);
                var right = ProjectOnSurface(cameraTransform.right, up);
                velocity += forward * (moveInput.y * forwardSpeed);
                velocity += right * (moveInput.x * strafeSpeed);
            }

            if (IsEscapePressed())
            {
                velocity += up * escapeSpeed;
            }

            cameraTransform.position += velocity * Time.deltaTime;

            var distanceFromCenter = Vector3.Distance(cameraTransform.position, planet.transform.position);
            if (distanceFromCenter >= planet.Radius + detachDistance)
            {
                FinishFlight(restoreCamera: true);
            }
        }

        private void MagnetizeToSurface()
        {
            var cameraTransform = controlledCamera.transform;
            var nearestTile = planet.FindNearestTile(cameraTransform.position);
            if (nearestTile == null)
            {
                return;
            }

            var center = planet.transform.position;
            var surfacePosition = planet.GetTileWorldSurfaceCenter(nearestTile);
            var normal = (surfacePosition - center).normalized;
            var surfaceRadius = Vector3.Distance(surfacePosition, center);
            var currentRadius = Vector3.Distance(cameraTransform.position, center);
            if (currentRadius > surfaceRadius + magnetRange || IsEscapePressed())
            {
                return;
            }

            var targetPosition = center + normal * (surfaceRadius + hoverAltitude);
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, targetPosition, surfaceAlignLerp);
            LookAlongSurface(normal);
        }

        private void TerraformNearCamera()
        {
            var tile = planet.FindNearestTile(controlledCamera.transform.position);
            if (tile == null)
            {
                return;
            }

            var applied = planet.AddTerraformingInfluence(tile, terraformRadius, terraformPowerPerSecond * Time.deltaTime);
            if (applied <= 0f || currencyPerCompletedTile <= 0f)
            {
                return;
            }

            _pendingCurrency += applied * currencyPerCompletedTile;
            var wholeCurrency = Mathf.FloorToInt(_pendingCurrency);
            if (wholeCurrency <= 0)
            {
                return;
            }

            planet.Currency.Add(wholeCurrency);
            _pendingCurrency -= wholeCurrency;
        }

        private void FinishFlight(bool restoreCamera)
        {
            if (restoreCamera && controlledCamera != null)
            {
                controlledCamera.transform.SetPositionAndRotation(_savedCameraPosition, _savedCameraRotation);
            }

            SetOrbitCameraEnabled(true);
            _approachTile = null;
            _state = Time.time < _cooldownEndTime ? SkillState.Cooldown : SkillState.Ready;
        }

        private void LookAlongSurface(Vector3 surfaceNormal)
        {
            if (controlledCamera == null)
            {
                return;
            }

            var up = surfaceNormal.sqrMagnitude > 0.000001f ? surfaceNormal.normalized : controlledCamera.transform.up;
            var forward = ProjectOnSurface(controlledCamera.transform.forward, up);
            if (forward.sqrMagnitude <= 0.000001f)
            {
                forward = Vector3.Cross(up, controlledCamera.transform.right).normalized;
            }

            var targetRotation = Quaternion.LookRotation(forward, up);
            controlledCamera.transform.rotation = Quaternion.Slerp(controlledCamera.transform.rotation, targetRotation, surfaceAlignLerp);
        }

        private Vector3 GetPlanetUp(Vector3 worldPosition)
        {
            if (planet == null)
            {
                return Vector3.up;
            }

            var up = worldPosition - planet.transform.position;
            return up.sqrMagnitude > 0.000001f ? up.normalized : Vector3.up;
        }

        private static Vector3 ProjectOnSurface(Vector3 direction, Vector3 up)
        {
            var projected = Vector3.ProjectOnPlane(direction, up);
            return projected.sqrMagnitude > 0.000001f ? projected.normalized : Vector3.zero;
        }

        private void ResolveReferences()
        {
            if (planet == null)
            {
                planet = FindFirstObjectByType<Planet>();
            }

            if (controlledCamera == null)
            {
                controlledCamera = Camera.main;
            }

            if (orbitCameraController == null)
            {
                orbitCameraController = FindFirstObjectByType<PlanetCameraController>();
            }
        }

        private void BindButton()
        {
            if (activateButton == null)
            {
                return;
            }

            activateButton.onClick.RemoveListener(ActivateSkill);
            activateButton.onClick.AddListener(ActivateSkill);
        }

        private void UnbindButton()
        {
            if (activateButton == null)
            {
                return;
            }

            activateButton.onClick.RemoveListener(ActivateSkill);
        }

        private void SetOrbitCameraEnabled(bool enabled)
        {
            if (orbitCameraController != null)
            {
                orbitCameraController.enabled = enabled;
            }
        }

        private void UpdateButtonState()
        {
            if (activateButton == null)
            {
                return;
            }

            activateButton.interactable = _state == SkillState.Ready && Time.time >= _cooldownEndTime;
        }

        private void UpdateStatusText()
        {
            if (statusText == null)
            {
                return;
            }

            statusText.text = _state switch
            {
                SkillState.Aiming => "Select planet tile",
                SkillState.Approaching => "Approaching",
                SkillState.Flying => $"{Mathf.CeilToInt(Mathf.Max(0f, _flightEndTime - Time.time))} s",
                SkillState.Cooldown => $"{Mathf.CeilToInt(Mathf.Max(0f, _cooldownEndTime - Time.time))} s",
                _ => string.Empty
            };
        }

        private static bool TryGetMoveInput(out Vector2 value)
        {
            value = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed)
                {
                    value.x -= 1f;
                }

                if (Keyboard.current.dKey.isPressed)
                {
                    value.x += 1f;
                }

                if (Keyboard.current.sKey.isPressed)
                {
                    value.y -= 1f;
                }

                if (Keyboard.current.wKey.isPressed)
                {
                    value.y += 1f;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            value.x += Input.GetAxisRaw("Horizontal");
            value.y += Input.GetAxisRaw("Vertical");
#endif

            value = Vector2.ClampMagnitude(value, 1f);
            return value.sqrMagnitude > 0.0001f;
        }

        private static bool IsEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.isPressed)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.E))
            {
                return true;
            }
#endif

            return false;
        }

        private static bool TryGetLookDelta(out Vector2 delta)
        {
            delta = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            {
                delta = Mouse.current.delta.ReadValue();
                return delta.sqrMagnitude > 0f;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButton(1))
            {
                delta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
                return delta.sqrMagnitude > 0f;
            }
#endif

            return false;
        }
    }
}
