using LittlePlanet.PlanetSystem;
using LittlePlanet.RuntimeInput;
using LittlePlanet.UI;
using System;
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

        private enum FinishReason
        {
            Cancelled,
            NoCameraCenterHit,
            MissingReferencesDuringApproach,
            MissingApproachTile,
            MissingReferencesDuringFlight,
            DurationExpired,
            DetachDistanceExceeded
        }

        [Header("References")]
        [SerializeField] private Planet planet;
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private PlanetCameraController orbitCameraController;
        [SerializeField] private Collider planetSurfaceCollider;
        [SerializeField] private Transform shipRoot;
        [SerializeField] private GameObject shipPrefab;
        [SerializeField] private Material runtimeShipMaterial;
        [SerializeField] private Button activateButton;
        [SerializeField] private GameObject flyIcon;
        [SerializeField] private GameObject returnIcon;
        [SerializeField] private WindowManager windowManager;

        [Header("Skill")]
        [SerializeField] private bool enableHotkey = true;
        [SerializeField] private KeyCode activationHotkey = KeyCode.F;
        [SerializeField, Min(5f)] private float flightDuration = 5f;
        [SerializeField, Min(0f)] private float cooldownDuration = 15f;
        [SerializeField, Min(0.1f)] private float approachDuration = 1.25f;
        [SerializeField, Min(0.1f)] private float approachSpeed = 80f;
        [SerializeField, Min(0.05f)] private float approachStopDistance = 0.25f;
        [SerializeField, Min(0.1f)] private float buttonStateUpdateInterval = 1f;

        [Header("Flight")]
        [SerializeField] private bool autoForward = true;
        [SerializeField, Min(0f)] private float forwardSpeed = 4f;
        [SerializeField, Min(0f)] private float strafeSpeed = 4f;
        [SerializeField, Min(0f)] private float escapeSpeed = 8f;
        [SerializeField, Min(0.01f)] private float lookSensitivity = 0.12f;
        [SerializeField, Min(1f)] private float maxLookDeltaPerFrame = 80f;
        [SerializeField, Min(0.05f)] private float hoverAltitude = 0.35f;
        [SerializeField, Min(0.1f)] private float magnetRange = 4f;
        [SerializeField, Min(0.5f)] private float detachDistance = 8f;
        [SerializeField, Min(0.1f)] private float surfaceFollowSpeed = 8f;
        [SerializeField, Min(0.1f)] private float shipRotationFollowSpeed = 10f;
        [SerializeField, Min(1f)] private float surfaceProbePadding = 10f;

        [Header("Third Person Camera")]
        [SerializeField, Min(0.5f)] private float cameraFollowDistance = 6f;
        [SerializeField, Min(0f)] private float cameraFollowHeight = 2f;
        [SerializeField, Min(0f)] private float cameraLookAtHeight = 0.6f;
        [SerializeField, Min(0.1f)] private float cameraFollowSpeed = 12f;
        [SerializeField, Min(0.01f)] private float cameraReturnDuration = 0.75f;
        [SerializeField, Range(-80f, 20f)] private float initialCameraPitch = -18f;
        [SerializeField, Range(-85f, 45f)] private float minCameraPitch = -65f;
        [SerializeField, Range(-45f, 85f)] private float maxCameraPitch = 25f;

        [Header("Terraforming")]
        [SerializeField, Range(1, 12)] private int terraformRadius = 2;
        [SerializeField, Min(0f)] private float terraformPowerPerSecond = 0.18f;
        [SerializeField, Min(0f)] private float currencyPerCompletedTile = 25f;

        [Header("Completion Hint")]
        [SerializeField] private bool showCompletionHint = true;
        [SerializeField, Range(0f, 100f)] private float completionHintStartPercent = 85f;
        [SerializeField, Range(0.5f, 1f)] private float incompleteTileThreshold = 0.999f;
        [SerializeField, Min(0.05f)] private float completionHintUpdateInterval = 0.25f;
        [SerializeField, Min(0f)] private float completionHintSurfaceOffset = 0.8f;
        [SerializeField] private Transform completionHintArrow;
        [SerializeField] private Color generatedHintColor = new(1f, 0.86f, 0.12f, 1f);

        [Header("Debug")]
        [SerializeField] private bool logSkillLifecycle = true;

        private SkillState _state = SkillState.Ready;
        private float _flightEndTime;
        private float _cooldownEndTime;
        private float _pendingCurrency;
        private Vector3 _savedCameraPosition;
        private Quaternion _savedCameraRotation;
        private Vector3 _approachStartPosition;
        private Tile _approachTile;
        private float _approachStartTime;
        private Vector3 _lastShipForward;
        private Vector3 _savedShipPosition;
        private Quaternion _savedShipRotation;
        private bool _savedShipActive;
        private bool _shipStateCached;
        private bool _ownsRuntimeShipInstance;
        private float _cameraPitch;
        private bool _savedOrbitCameraEnabled = true;
        private bool _savedOrbitZoomEnabled = true;
        private bool _orbitControlsCached;
        private float _nextButtonStateUpdateTime;
        private float _nextCompletionHintUpdateTime;
        private bool _ownsCompletionHintArrow;
        private Coroutine _cameraReturnRoutine;
        private bool _isCameraReturning;
        private bool _tutorialHotkeyAllowed = true;
        public bool IsFlightModeActive => _state == SkillState.Flying || _state == SkillState.Approaching;
        public bool IsCameraTransitionActive => _isCameraReturning || _state == SkillState.Approaching;
        public float FlightDuration => flightDuration;
        public float CooldownDuration => cooldownDuration;
        public int TerraformRadius => terraformRadius;
        public float TerraformPowerPerSecond => terraformPowerPerSecond;
        public float CurrencyPerCompletedTile => currencyPerCompletedTile;
        public event Action SkillActivated;
        public event Action FlightStarted;
        public event Action FlightEnded;

        private void Awake()
        {
            ResolveReferences();
            BindButton();
            UpdateIconState();
        }

        private void OnEnable()
        {
            ResolveReferences();
            BindButton();
            UpdateIconState();

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
            StopCameraReturn();
            RestoreOrbitControls();
            SetCompletionHintVisible(false);
            UpdateIconState();
        }

        private void OnDestroy()
        {
            if (_ownsRuntimeShipInstance && shipRoot != null)
            {
                Destroy(shipRoot.gameObject);
            }

            if (_ownsCompletionHintArrow && completionHintArrow != null)
            {
                Destroy(completionHintArrow.gameObject);
            }
        }

        private void Update()
        {
            ResolveReferences();
            HandleHotkey();

            switch (_state)
            {
                case SkillState.Approaching:
                    UpdateApproach();
                    break;
                case SkillState.Flying:
                    UpdateFlight();
                    break;
            }

            UpdateButtonState();
            UpdateIconState();
        }

        private void OnValidate()
        {
            flightDuration = Mathf.Max(5f, flightDuration);
            cooldownDuration = Mathf.Max(0f, cooldownDuration);
            approachDuration = Mathf.Max(0.1f, approachDuration);
            approachSpeed = Mathf.Max(0.1f, approachSpeed);
            approachStopDistance = Mathf.Max(0.05f, approachStopDistance);
            buttonStateUpdateInterval = Mathf.Max(0.1f, buttonStateUpdateInterval);
            hoverAltitude = Mathf.Max(0.05f, hoverAltitude);
            magnetRange = Mathf.Max(0.1f, magnetRange);
            detachDistance = Mathf.Max(0.5f, detachDistance);
            surfaceFollowSpeed = Mathf.Max(0.1f, surfaceFollowSpeed);
            shipRotationFollowSpeed = Mathf.Max(0.1f, shipRotationFollowSpeed);
            surfaceProbePadding = Mathf.Max(1f, surfaceProbePadding);
            maxLookDeltaPerFrame = Mathf.Max(1f, maxLookDeltaPerFrame);
            cameraFollowDistance = Mathf.Max(0.5f, cameraFollowDistance);
            cameraFollowHeight = Mathf.Max(0f, cameraFollowHeight);
            cameraLookAtHeight = Mathf.Max(0f, cameraLookAtHeight);
            cameraFollowSpeed = Mathf.Max(0.1f, cameraFollowSpeed);
            cameraReturnDuration = Mathf.Max(0.01f, cameraReturnDuration);
            if (maxCameraPitch < minCameraPitch)
            {
                maxCameraPitch = minCameraPitch;
            }

            completionHintUpdateInterval = Mathf.Max(0.05f, completionHintUpdateInterval);
            completionHintSurfaceOffset = Mathf.Max(0f, completionHintSurfaceOffset);
        }

        public void ActivateSkill()
        {
            if (_state != SkillState.Ready || _isCameraReturning)
            {
                return;
            }

            ResolveReferences();
            windowManager?.CloseAll();
            if (!TryGetCameraCenterTile(out var tile))
            {
                LogSkill("Activation failed: camera center ray missed planet.");
                return;
            }

            BeginApproachToTile(tile);
            SkillActivated?.Invoke();
        }

        public void ToggleSkill()
        {
            if (_state == SkillState.Aiming || _state == SkillState.Approaching || _state == SkillState.Flying)
            {
                CancelSkill();
                return;
            }

            ActivateSkill();
        }

        public float GetUpgradeValue(UpgradeType upgradeType)
        {
            return upgradeType switch
            {
                UpgradeType.FlyPower => terraformPowerPerSecond,
                UpgradeType.FlyRadius => terraformRadius,
                UpgradeType.GreenReward => currencyPerCompletedTile,
                UpgradeType.FlyDuration => flightDuration,
                UpgradeType.FlyCooldown => cooldownDuration,
                _ => 0f
            };
        }

        public void AddUpgradeValue(UpgradeType upgradeType, float value)
        {
            switch (upgradeType)
            {
                case UpgradeType.FlyPower:
                    terraformPowerPerSecond = Mathf.Max(0f, terraformPowerPerSecond + value);
                    break;
                case UpgradeType.FlyRadius:
                    terraformRadius = Mathf.Clamp(Mathf.RoundToInt(terraformRadius + value), 1, 12);
                    break;
                case UpgradeType.GreenReward:
                    currencyPerCompletedTile = Mathf.Max(0f, currencyPerCompletedTile + value);
                    break;
                case UpgradeType.FlyDuration:
                    flightDuration = Mathf.Max(5f, flightDuration + value);
                    break;
                case UpgradeType.FlyCooldown:
                    cooldownDuration = Mathf.Max(0f, cooldownDuration - value);
                    break;
            }
        }

        public void CancelSkill()
        {
            if (_state == SkillState.Approaching || _state == SkillState.Flying)
            {
                FinishFlight(FinishReason.Cancelled, restoreCamera: true);
                return;
            }

            if (_state == SkillState.Aiming)
            {
                _state = SkillState.Ready;
                RestoreOrbitControls();
            }
        }

        public void CancelSkillAndStartCooldown()
        {
            if (_state == SkillState.Approaching || _state == SkillState.Flying)
            {
                _flightEndTime = Time.time;
                _cooldownEndTime = Time.time + cooldownDuration;
                FinishFlight(FinishReason.Cancelled, restoreCamera: true);
                return;
            }

            CancelSkill();
        }

        public void SetTutorialHotkeyAllowed(bool isAllowed)
        {
            _tutorialHotkeyAllowed = isAllowed;
        }

        private void HandlePlanetTileClicked(Tile tile)
        {
            if (_state != SkillState.Aiming || tile == null || planet == null || controlledCamera == null)
            {
                return;
            }

            BeginApproachToTile(tile);
        }

        private void BeginApproachToTile(Tile tile)
        {
            if (tile == null || planet == null || controlledCamera == null)
            {
                FinishFlight(FinishReason.NoCameraCenterHit, restoreCamera: false);
                return;
            }

            EnsureShipRoot();
            if (shipRoot == null)
            {
                return;
            }

            _savedCameraPosition = controlledCamera.transform.position;
            _savedCameraRotation = controlledCamera.transform.rotation;
            CacheShipState();
            PlaceShipAtTile(tile);
            _approachStartPosition = _savedCameraPosition;
            _approachStartTime = Time.time;
            _flightEndTime = 0f;
            _cooldownEndTime = 0f;
            _pendingCurrency = 0f;
            _approachTile = tile;
            CacheOrbitControls();
            SetOrbitCameraEnabled(false);
            SetOrbitZoomEnabled(false);

            _state = SkillState.Approaching;
            LogSkill($"Third-person approach started. tile={tile.Index}, approachDuration={approachDuration:0.##}");
        }

        private void UpdateApproach()
        {
            if (planet == null || controlledCamera == null)
            {
                FinishFlight(FinishReason.MissingReferencesDuringApproach, restoreCamera: true);
                return;
            }

            if (_approachTile == null)
            {
                FinishFlight(FinishReason.MissingApproachTile, restoreCamera: true);
                return;
            }

            MagnetizeShipToSurface();
            var targetPosition = GetDesiredCameraPosition();
            var durationT = Mathf.Clamp01((Time.time - _approachStartTime) / approachDuration);
            var easedT = durationT * durationT * (3f - 2f * durationT);
            var lerpPosition = Vector3.Lerp(_approachStartPosition, targetPosition, easedT);
            controlledCamera.transform.position = Vector3.MoveTowards(
                lerpPosition,
                targetPosition,
                approachSpeed * Time.deltaTime);

            LookAtShip();

            if (durationT >= 1f || Vector3.Distance(controlledCamera.transform.position, targetPosition) <= approachStopDistance)
            {
                StartFlying();
            }
        }

        private void StartFlying()
        {
            _flightEndTime = 0f;
            _cooldownEndTime = 0f;
            _state = SkillState.Flying;
            LogSkill("Flying started.");
            FlightStarted?.Invoke();
        }

        private void UpdateFlight()
        {
            if (planet == null || controlledCamera == null)
            {
                FinishFlight(FinishReason.MissingReferencesDuringFlight, restoreCamera: true);
                return;
            }

            RotateCameraOrbitIfRequested();
            MoveShip();
            if (_state != SkillState.Flying)
            {
                return;
            }

            MagnetizeShipToSurface();
            UpdateCameraFollow();
            TerraformNearShip();
        }

        private void LateUpdate()
        {
            UpdateCompletionHint();
        }

        private Vector3 GetFlyTargetPosition(Tile tile)
        {
            var fallbackSurfacePosition = planet.GetTileWorldSurfaceCenter(tile);
            var normal = (fallbackSurfacePosition - planet.transform.position).normalized;
            var surfacePosition = TryGetPlanetSurfacePoint(normal, out var raycastSurfacePosition)
                ? raycastSurfacePosition
                : fallbackSurfacePosition;
            var surfaceRadius = GetFlightSurfaceRadius(surfacePosition);
            return planet.transform.position + normal * (surfaceRadius + hoverAltitude);
        }

        private bool TryGetCameraCenterTile(out Tile tile)
        {
            tile = null;
            if (planet == null || controlledCamera == null)
            {
                return false;
            }

            ResolvePlanetSurfaceCollider();
            if (planetSurfaceCollider == null)
            {
                return false;
            }

            var ray = controlledCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!planetSurfaceCollider.Raycast(ray, out var hit, Mathf.Max(planet.Radius * 20f, 500f)))
            {
                return false;
            }

            tile = planet.FindNearestTile(hit.point);
            return tile != null;
        }

        private void RotateCameraOrbitIfRequested()
        {
            if (!TryGetLookDelta(out var delta))
            {
                return;
            }

            delta = Vector2.ClampMagnitude(delta, maxLookDeltaPerFrame);
            if (shipRoot != null)
            {
                var up = GetPlanetUp(shipRoot.position);
                var yawDelta = delta.x * lookSensitivity;
                _lastShipForward = ProjectOnSurface(Quaternion.AngleAxis(yawDelta, up) * GetShipForward(up), up);
            }

            _cameraPitch = Mathf.Clamp(_cameraPitch - delta.y * lookSensitivity, minCameraPitch, maxCameraPitch);
        }

        private void MoveShip()
        {
            if (shipRoot == null)
            {
                return;
            }

            var up = GetPlanetUp(shipRoot.position);
            var shipForward = GetShipForward(up);
            var velocity = Vector3.zero;
            var forwardVelocity = Vector3.zero;

            if (autoForward)
            {
                forwardVelocity += shipForward * forwardSpeed;
            }

            if (TryGetMoveInput(out var moveInput))
            {
                var forward = shipForward;
                var right = ProjectOnSurface(controlledCamera.transform.right, up);
                forwardVelocity += forward * (moveInput.y * forwardSpeed);
                velocity += right * (moveInput.x * strafeSpeed);
            }

            velocity += forwardVelocity;

            if (IsEscapePressed())
            {
                velocity += up * escapeSpeed;
            }

            shipRoot.position += velocity * Time.deltaTime;
            if (forwardVelocity.sqrMagnitude > 0.000001f)
            {
                _lastShipForward = ProjectOnSurface(forwardVelocity, up);
            }

            var distanceFromCenter = Vector3.Distance(shipRoot.position, planet.transform.position);
            if (IsEscapePressed() && distanceFromCenter >= planet.Radius + detachDistance)
            {
                FinishFlight(FinishReason.DetachDistanceExceeded, restoreCamera: true);
            }
        }

        private void MagnetizeShipToSurface()
        {
            if (shipRoot == null)
            {
                return;
            }

            var center = planet.transform.position;
            var normal = GetPlanetUp(shipRoot.position);
            if (!TryGetPlanetSurfacePoint(normal, out var surfacePosition))
            {
                var nearestTile = planet.FindNearestTile(shipRoot.position);
                if (nearestTile == null)
                {
                    return;
                }

                surfacePosition = planet.GetTileWorldSurfaceCenter(nearestTile);
            }

            var surfaceRadius = Vector3.Distance(surfacePosition, center);
            surfaceRadius = Mathf.Max(surfaceRadius, GetCurrentWaterRadius());
            var currentRadius = Vector3.Distance(shipRoot.position, center);
            if (currentRadius > surfaceRadius + magnetRange || IsEscapePressed())
            {
                AlignShipRotation(normal);
                return;
            }

            var targetPosition = center + normal * (surfaceRadius + hoverAltitude);
            shipRoot.position = Vector3.Lerp(shipRoot.position, targetPosition, GetFrameLerp(surfaceFollowSpeed));
            AlignShipRotation(normal);
        }

        private float GetFlightSurfaceRadius(Vector3 surfacePosition)
        {
            if (planet == null)
            {
                return 0f;
            }

            var terrainRadius = Vector3.Distance(surfacePosition, planet.transform.position);
            return Mathf.Max(terrainRadius, GetCurrentWaterRadius());
        }

        private float GetCurrentWaterRadius()
        {
            return planet != null ? Mathf.Max(0f, planet.CurrentWaterRadius) : 0f;
        }

        private void TerraformNearShip()
        {
            if (shipRoot == null)
            {
                return;
            }

            var tile = planet.FindNearestTile(shipRoot.position);
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

        private void UpdateCompletionHint()
        {
            if (!showCompletionHint || planet == null || Time.time < _nextCompletionHintUpdateTime)
            {
                return;
            }

            _nextCompletionHintUpdateTime = Time.time + completionHintUpdateInterval;
            var terraformingPercent = planet.GetTerraformingPercent();
            if (terraformingPercent < completionHintStartPercent || terraformingPercent >= 99.99f)
            {
                SetCompletionHintVisible(false);
                return;
            }

            if (!TryFindNearestIncompleteTile(out var tile))
            {
                SetCompletionHintVisible(false);
                return;
            }

            EnsureCompletionHintArrow();
            if (completionHintArrow == null)
            {
                return;
            }

            var position = planet.GetTileWorldSurfaceCenter(tile, completionHintSurfaceOffset);
            completionHintArrow.position = position;

            var up = (position - planet.transform.position).normalized;
            var forward = controlledCamera != null
                ? position - controlledCamera.transform.position
                : Vector3.ProjectOnPlane(Vector3.forward, up);
            if (forward.sqrMagnitude <= 0.000001f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            }

            completionHintArrow.rotation = Quaternion.LookRotation(forward.normalized, up);
            SetCompletionHintVisible(true);
        }

        private bool TryFindNearestIncompleteTile(out Tile bestTile)
        {
            bestTile = null;
            if (planet == null || planet.Tiles == null || planet.Tiles.Count == 0)
            {
                return false;
            }

            var referencePosition = shipRoot != null && shipRoot.gameObject.activeInHierarchy
                ? shipRoot.position
                : controlledCamera != null
                    ? controlledCamera.transform.position
                    : planet.transform.position;

            var bestDistanceSqr = float.PositiveInfinity;
            var tiles = planet.Tiles;
            for (var i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                if (tile == null || planet.IsHighlandTile(tile) || planet.GetTileTerraforming01(tile) >= incompleteTileThreshold)
                {
                    continue;
                }

                var tilePosition = planet.GetTileWorldSurfaceCenter(tile);
                var distanceSqr = (tilePosition - referencePosition).sqrMagnitude;
                if (distanceSqr >= bestDistanceSqr)
                {
                    continue;
                }

                bestDistanceSqr = distanceSqr;
                bestTile = tile;
            }

            return bestTile != null;
        }

        private void EnsureCompletionHintArrow()
        {
            if (completionHintArrow != null)
            {
                return;
            }

            var arrowObject = new GameObject("TerraformCompletionHintArrow");
            var meshFilter = arrowObject.AddComponent<MeshFilter>();
            var meshRenderer = arrowObject.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = CreateCompletionHintArrowMesh();
            meshRenderer.sharedMaterial = CreateCompletionHintMaterial();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            completionHintArrow = arrowObject.transform;
            _ownsCompletionHintArrow = true;
        }

        private void SetCompletionHintVisible(bool isVisible)
        {
            if (completionHintArrow != null && completionHintArrow.gameObject.activeSelf != isVisible)
            {
                completionHintArrow.gameObject.SetActive(isVisible);
            }
        }

        private Mesh CreateCompletionHintArrowMesh()
        {
            var mesh = new Mesh { name = "TerraformCompletionHintArrowMesh" };
            mesh.vertices = new[]
            {
                new Vector3(0f, -0.55f, 0f),
                new Vector3(-0.35f, -0.1f, 0f),
                new Vector3(-0.13f, -0.1f, 0f),
                new Vector3(-0.13f, 0.45f, 0f),
                new Vector3(0.13f, 0.45f, 0f),
                new Vector3(0.13f, -0.1f, 0f),
                new Vector3(0.35f, -0.1f, 0f)
            };
            mesh.triangles = new[]
            {
                0, 1, 2,
                0, 2, 5,
                0, 5, 6,
                2, 3, 4,
                2, 4, 5
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Material CreateCompletionHintMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Standard");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = "TerraformCompletionHintArrowMaterial",
                color = generatedHintColor
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", generatedHintColor);
            }

            return material;
        }

        private void FinishFlight(FinishReason reason, bool restoreCamera)
        {
            LogSkill($"Finished. reason={reason}, restoreCamera={restoreCamera}");

            if (restoreCamera && controlledCamera != null)
            {
                StartCameraReturn();
            }
            else
            {
                RestoreOrbitControls();
            }

            RestoreShipState();
            _approachTile = null;
            _flightEndTime = 0f;
            _cooldownEndTime = 0f;
            _state = SkillState.Ready;
            UpdateIconState();
            FlightEnded?.Invoke();
        }

        private void StartCameraReturn()
        {
            if (controlledCamera == null)
            {
                RestoreOrbitControls();
                return;
            }

            if (_cameraReturnRoutine != null)
            {
                StopCoroutine(_cameraReturnRoutine);
            }

            _cameraReturnRoutine = StartCoroutine(CameraReturnRoutine());
        }

        private void StopCameraReturn()
        {
            if (_cameraReturnRoutine != null)
            {
                StopCoroutine(_cameraReturnRoutine);
                _cameraReturnRoutine = null;
            }

            _isCameraReturning = false;
        }

        private System.Collections.IEnumerator CameraReturnRoutine()
        {
            _isCameraReturning = true;
            SetOrbitCameraEnabled(false);
            SetOrbitZoomEnabled(false);

            var startPosition = controlledCamera.transform.position;
            var startRotation = controlledCamera.transform.rotation;
            var duration = Mathf.Max(0.01f, cameraReturnDuration);
            var elapsed = 0f;

            while (elapsed < duration && controlledCamera != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var easedT = t * t * (3f - 2f * t);
                controlledCamera.transform.position = Vector3.Lerp(startPosition, _savedCameraPosition, easedT);
                controlledCamera.transform.rotation = Quaternion.Slerp(startRotation, _savedCameraRotation, easedT);
                yield return null;
            }

            if (controlledCamera != null)
            {
                controlledCamera.transform.SetPositionAndRotation(_savedCameraPosition, _savedCameraRotation);
            }

            _isCameraReturning = false;
            _cameraReturnRoutine = null;
            RestoreOrbitControls();
        }

        private void LogSkill(string message)
        {
            if (!logSkillLifecycle)
            {
                return;
            }

            Debug.Log($"[PlanetFlyTerraformSkill] {message} {BuildDebugSnapshot()}", this);
        }

        private string BuildDebugSnapshot()
        {
            var now = Time.time;
            var flightRemaining = _flightEndTime > 0f ? Mathf.Max(0f, _flightEndTime - now) : 0f;
            var cooldownRemaining = _cooldownEndTime > 0f ? Mathf.Max(0f, _cooldownEndTime - now) : 0f;
            var cameraDistance = 0f;
            var shipDistance = 0f;
            var detachThreshold = 0f;

            if (planet != null && controlledCamera != null)
            {
                cameraDistance = Vector3.Distance(controlledCamera.transform.position, planet.transform.position);
                detachThreshold = planet.Radius + detachDistance;
            }

            if (planet != null && shipRoot != null)
            {
                shipDistance = Vector3.Distance(shipRoot.position, planet.transform.position);
            }

            return $"state={_state}, time={now:0.00}, flightLeft={flightRemaining:0.00}, cooldownLeft={cooldownRemaining:0.00}, cameraDistance={cameraDistance:0.00}, shipDistance={shipDistance:0.00}, detachThreshold={detachThreshold:0.00}";
        }

        private void UpdateCameraFollow()
        {
            if (controlledCamera == null || shipRoot == null)
            {
                return;
            }

            controlledCamera.transform.position = Vector3.Lerp(
                controlledCamera.transform.position,
                GetDesiredCameraPosition(),
                GetFrameLerp(cameraFollowSpeed));
            LookAtShip();
        }

        private Vector3 GetDesiredCameraPosition()
        {
            if (shipRoot == null)
            {
                return controlledCamera != null ? controlledCamera.transform.position : Vector3.zero;
            }

            var up = GetPlanetUp(shipRoot.position);
            var forward = GetShipForward(up);
            var right = Vector3.Cross(up, forward).normalized;
            var cameraDirection = Quaternion.AngleAxis(_cameraPitch, right) * -forward;
            return shipRoot.position + up * cameraFollowHeight + cameraDirection.normalized * cameraFollowDistance;
        }

        private bool TryGetPlanetSurfacePoint(Vector3 normal, out Vector3 surfacePosition)
        {
            surfacePosition = default;
            ResolvePlanetSurfaceCollider();
            if (planet == null || planetSurfaceCollider == null || normal.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            var center = planet.transform.position;
            var currentDistance = shipRoot != null
                ? Vector3.Distance(shipRoot.position, center)
                : planet.Radius;
            var originDistance = Mathf.Max(
                currentDistance + surfaceProbePadding,
                planet.Radius + detachDistance + magnetRange + hoverAltitude + surfaceProbePadding);

            var ray = new Ray(center + normal.normalized * originDistance, -normal.normalized);
            var rayDistance = originDistance + surfaceProbePadding;
            if (!planetSurfaceCollider.Raycast(ray, out var hit, rayDistance))
            {
                return false;
            }

            surfacePosition = hit.point;
            return true;
        }

        private void LookAtShip()
        {
            if (controlledCamera == null || shipRoot == null)
            {
                return;
            }

            var up = GetPlanetUp(shipRoot.position);
            var target = shipRoot.position + up * cameraLookAtHeight;
            var toTarget = target - controlledCamera.transform.position;
            if (toTarget.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(toTarget.normalized, up);
            controlledCamera.transform.rotation = Quaternion.Slerp(controlledCamera.transform.rotation, targetRotation, GetFrameLerp(cameraFollowSpeed));
        }

        private void PlaceShipAtTile(Tile tile)
        {
            if (shipRoot == null || tile == null)
            {
                return;
            }

            var position = GetFlyTargetPosition(tile);
            shipRoot.gameObject.SetActive(true);
            shipRoot.position = position;
            var up = GetPlanetUp(position);
            _lastShipForward = ProjectOnSurface(controlledCamera.transform.forward, up);
            if (_lastShipForward.sqrMagnitude <= 0.000001f)
            {
                _lastShipForward = ProjectOnSurface(Vector3.Cross(controlledCamera.transform.right, up), up);
            }

            _cameraPitch = initialCameraPitch;
            AlignShipRotation(up);
        }

        private void AlignShipRotation(Vector3 up)
        {
            if (shipRoot == null)
            {
                return;
            }

            var forward = GetShipForward(up);
            shipRoot.rotation = Quaternion.Slerp(shipRoot.rotation, Quaternion.LookRotation(forward, up), GetFrameLerp(shipRotationFollowSpeed));
        }

        private Vector3 GetShipForward(Vector3 up)
        {
            var forward = ProjectOnSurface(_lastShipForward, up);
            if (forward.sqrMagnitude > 0.000001f)
            {
                return forward;
            }

            forward = shipRoot != null ? ProjectOnSurface(shipRoot.forward, up) : Vector3.zero;
            if (forward.sqrMagnitude > 0.000001f)
            {
                return forward;
            }

            return Vector3.Cross(up, Vector3.right).sqrMagnitude > 0.000001f
                ? Vector3.Cross(up, Vector3.right).normalized
                : Vector3.Cross(up, Vector3.forward).normalized;
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

        private static float GetFrameLerp(float speed)
        {
            return 1f - Mathf.Exp(-Mathf.Max(0.01f, speed) * Time.deltaTime);
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

            if (windowManager == null)
            {
                windowManager = WindowManager.Instance;
            }

            ResolveActionIcons();
            ResolvePlanetSurfaceCollider();
        }

        private void HandleHotkey()
        {
            if (!enableHotkey || !_tutorialHotkeyAllowed || !InputCompat.WasKeyPressedThisFrame(activationHotkey))
            {
                return;
            }

            ToggleSkill();
        }

        private void ResolvePlanetSurfaceCollider()
        {
            if (planetSurfaceCollider != null || planet == null)
            {
                return;
            }

            planetSurfaceCollider = planet.GetComponent<Collider>();
        }

        private void EnsureShipRoot()
        {
            if (shipRoot != null)
            {
                return;
            }

            GameObject shipObject;
            if (shipPrefab != null)
            {
                shipObject = Instantiate(shipPrefab);
                shipObject.name = "Ship_Runtime";
            }
            else
            {
                shipObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                shipObject.name = "TerraformShip_Runtime";
                shipObject.transform.localScale = Vector3.one * 0.5f;
                ApplyRuntimeShipMaterial(shipObject);
            }

            shipObject.SetActive(false);
            _ownsRuntimeShipInstance = true;
            shipRoot = shipObject.transform;
        }

        private void ApplyRuntimeShipMaterial(GameObject shipObject)
        {
            if (shipObject == null || !shipObject.TryGetComponent<Renderer>(out var renderer))
            {
                return;
            }

            renderer.sharedMaterial = runtimeShipMaterial != null
                ? runtimeShipMaterial
                : CreateDefaultRuntimeShipMaterial();
        }

        private static Material CreateDefaultRuntimeShipMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Standard");

            var material = new Material(shader)
            {
                name = "TerraformShip_RuntimeMaterial"
            };

            var color = new Color(0.72f, 0.74f, 0.72f, 1f);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            return material;
        }

        private void CacheShipState()
        {
            if (shipRoot == null)
            {
                _shipStateCached = false;
                return;
            }

            _savedShipPosition = shipRoot.position;
            _savedShipRotation = shipRoot.rotation;
            _savedShipActive = shipRoot.gameObject.activeSelf;
            _shipStateCached = true;
        }

        private void RestoreShipState()
        {
            if (shipRoot == null || !_shipStateCached)
            {
                return;
            }

            shipRoot.SetPositionAndRotation(_savedShipPosition, _savedShipRotation);
            shipRoot.gameObject.SetActive(_savedShipActive);
            _shipStateCached = false;
        }

        private void BindButton()
        {
            if (activateButton == null)
            {
                return;
            }

            activateButton.onClick.RemoveListener(ActivateSkill);
            activateButton.onClick.RemoveListener(ToggleSkill);
            activateButton.onClick.AddListener(ToggleSkill);
        }

        private void UnbindButton()
        {
            if (activateButton == null)
            {
                return;
            }

            activateButton.onClick.RemoveListener(ActivateSkill);
            activateButton.onClick.RemoveListener(ToggleSkill);
        }

        private void SetOrbitCameraEnabled(bool enabled)
        {
            if (orbitCameraController != null)
            {
                orbitCameraController.enabled = enabled;
            }
        }

        private void SetOrbitZoomEnabled(bool enabled)
        {
            if (orbitCameraController != null)
            {
                orbitCameraController.ZoomEnabled = enabled;
            }
        }

        private void CacheOrbitControls()
        {
            if (orbitCameraController == null)
            {
                _savedOrbitCameraEnabled = true;
                _savedOrbitZoomEnabled = true;
                return;
            }

            _savedOrbitCameraEnabled = orbitCameraController.enabled;
            _savedOrbitZoomEnabled = orbitCameraController.ZoomEnabled;
            _orbitControlsCached = true;
        }

        private void RestoreOrbitControls()
        {
            if (orbitCameraController == null)
            {
                return;
            }

            if (!_orbitControlsCached)
            {
                return;
            }

            orbitCameraController.ZoomEnabled = _savedOrbitZoomEnabled;
            orbitCameraController.enabled = _savedOrbitCameraEnabled;
            _orbitControlsCached = false;
        }

        private void UpdateButtonState()
        {
            if (activateButton == null)
            {
                return;
            }

            if (Time.time < _nextButtonStateUpdateTime)
            {
                return;
            }

            _nextButtonStateUpdateTime = Time.time + buttonStateUpdateInterval;
            activateButton.interactable = true;
        }

        private void UpdateIconState()
        {
            var showReturnIcon = IsFlightModeActive;
            var showFlyIcon = !showReturnIcon;

            if (flyIcon != null)
            {
                flyIcon.SetActive(showFlyIcon);
            }

            if (returnIcon != null)
            {
                returnIcon.SetActive(showReturnIcon);
            }
        }

        private void ResolveActionIcons()
        {
            if (activateButton == null)
            {
                return;
            }

            if (flyIcon == null)
            {
                flyIcon = FindChildObjectByName(activateButton.transform, "FlyIcon")
                    ?? FindChildObjectByName(activateButton.transform, "flyIcon")
                    ?? FindChildObjectByName(activateButton.transform, "Fly");
            }

            if (returnIcon == null)
            {
                returnIcon = FindChildObjectByName(activateButton.transform, "ReturnIcon")
                    ?? FindChildObjectByName(activateButton.transform, "returnIcon")
                    ?? FindChildObjectByName(activateButton.transform, "BackIcon");
            }
        }

        private static GameObject FindChildObjectByName(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrWhiteSpace(targetName))
            {
                return null;
            }

            var children = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < children.Length; i++)
            {
                var child = children[i];
                if (child != null && string.Equals(child.name, targetName, System.StringComparison.Ordinal))
                {
                    return child.gameObject;
                }
            }

            return null;
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
