using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using System.Collections;

namespace SGJ26.Terraforming
{
    public sealed class TerraformFlyCamera : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlanetTerraformController terraformController;
        [SerializeField] private Collider planetCollider;

        [Header("Flight")]
        [SerializeField, Min(0f)] private float forwardSpeed = 12f;
        [SerializeField, Min(0f)] private float boostMultiplier = 2.5f;
        [SerializeField, Min(0f)] private float mouseLookSensitivity = 0.12f;
        [SerializeField, Range(-89f, 89f)] private float minPitch = -80f;
        [SerializeField, Range(-89f, 89f)] private float maxPitch = 80f;
        [SerializeField] private bool autopilotEnabled = true;
        [SerializeField] private Key autopilotToggleKey = Key.F;
        [SerializeField] private Key cursorToggleKey = Key.Tab;
        [SerializeField] private bool lockCursorOnPlay = true;

        [Header("Terraforming")]
        [SerializeField, Min(0f)] private float activationDistance = 8f;
        [SerializeField, Min(0.01f)] private float terraformRadius = 3.2f;
        [SerializeField, Min(0f)] private float terraformAmountPerSecond = 0.45f;
        [SerializeField] private bool useDirectVertexPaint = true;
        [SerializeField] private bool logDebug;

        [Header("Surface Magnet")]
        [SerializeField] private bool surfaceMagnetEnabled = true;
        [SerializeField, Min(0f)] private float magnetEnterDistance = 10f;
        [SerializeField, Min(0f)] private float magnetExitDistance = 14f;
        [SerializeField, Min(0f)] private float hoverHeight = 3.2f;
        [SerializeField, Min(0f)] private float magnetAlignSpeed = 6f;
        [SerializeField, Min(0f)] private float magnetYawSensitivityMultiplier = 2.2f;
        [SerializeField, Min(0f)] private float surfaceForwardSpeedMultiplier = 0.65f;

        private float yaw;
        private float pitch;
        private bool magnetized;
        private Vector3 surfaceUp = Vector3.up;
        private Vector3 magnetForward = Vector3.forward;
        private float magnetDisabledUntil;

        private void Reset()
        {
            terraformController = FindFirstObjectByType<PlanetTerraformController>();
            planetCollider = terraformController != null ? terraformController.GetComponent<Collider>() : null;
        }

        public void SetTarget(PlanetTerraformController controller)
        {
            terraformController = controller;
            planetCollider = null;
            magnetized = false;
            EnsureReferences();
        }

        private void OnEnable()
        {
            PlanetInputRuntime.EnsurePointerInputReady();

            Vector3 euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = NormalizePitch(euler.x);

            EnsureReferences();

            if (lockCursorOnPlay)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            if (lockCursorOnPlay)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Update()
        {
            PlanetInputRuntime.EnsurePointerInputReady();
            EnsureReferences();
            UpdateCursorToggle();
            UpdateAutopilotToggle();
            UpdateMagnetState();
            UpdateLook();
            UpdateFlight();
            UpdateTerraforming();
        }

        private void EnsureReferences()
        {
            if (terraformController == null)
                terraformController = FindFirstObjectByType<PlanetTerraformController>();

            if (planetCollider == null && terraformController != null)
            {
                planetCollider = terraformController.GetComponent<Collider>();

                if (planetCollider == null)
                    planetCollider = EnsureMeshCollider(terraformController);
            }
        }

        private void UpdateLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            Vector2 delta = mouse.delta.ReadValue();
            if (magnetized)
            {
                magnetForward = Vector3.ProjectOnPlane(magnetForward, surfaceUp);
                if (magnetForward.sqrMagnitude <= 0.0001f)
                    magnetForward = Vector3.ProjectOnPlane(transform.forward, surfaceUp);
                if (magnetForward.sqrMagnitude <= 0.0001f)
                    magnetForward = Vector3.Cross(surfaceUp, transform.right);

                magnetForward = magnetForward.normalized;
                magnetForward = Quaternion.AngleAxis(delta.x * mouseLookSensitivity * magnetYawSensitivityMultiplier, surfaceUp) * magnetForward;
                pitch = Mathf.Clamp(pitch - delta.y * mouseLookSensitivity, minPitch, maxPitch);

                Vector3 right = Vector3.Cross(surfaceUp, magnetForward).normalized;
                Vector3 lookForward = Quaternion.AngleAxis(pitch, right) * magnetForward;
                Quaternion targetRotation = Quaternion.LookRotation(lookForward, surfaceUp);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-magnetAlignSpeed * Time.deltaTime));
                return;
            }

            yaw += delta.x * mouseLookSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * mouseLookSensitivity, minPitch, maxPitch);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void UpdateFlight()
        {
            Keyboard keyboard = Keyboard.current;
            Vector3 move = Vector3.zero;
            Vector3 forward = magnetized ? Vector3.ProjectOnPlane(transform.forward, surfaceUp) : transform.forward;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = magnetForward;
            forward.Normalize();
            Vector3 right = magnetized ? Vector3.Cross(surfaceUp, forward).normalized : transform.right;
            Vector3 up = magnetized ? surfaceUp : transform.up;

            if (autopilotEnabled)
                move += forward;

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed)
                    move += forward;
                if (keyboard.sKey.isPressed)
                    move -= forward;
                if (keyboard.dKey.isPressed)
                    move += right;
                if (keyboard.aKey.isPressed)
                    move -= right;
                if (keyboard.eKey.isPressed)
                {
                    if (magnetized)
                    {
                        magnetized = false;
                        magnetDisabledUntil = Time.time + 1.25f;
                    }

                    move += Time.time < magnetDisabledUntil ? surfaceUp : up;
                }
                if (keyboard.qKey.isPressed)
                    move -= up;
            }

            if (move.sqrMagnitude <= 0.0001f)
                return;

            float speed = forwardSpeed;
            if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
                speed *= boostMultiplier;
            if (magnetized)
                speed *= surfaceForwardSpeedMultiplier;

            transform.position += move.normalized * (speed * Time.deltaTime);
            UpdateHoverHeight();
        }

        private void UpdateAutopilotToggle()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            KeyControl key = keyboard[autopilotToggleKey];
            if (key != null && key.wasPressedThisFrame)
                autopilotEnabled = !autopilotEnabled;
        }

        private void UpdateCursorToggle()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            KeyControl key = keyboard[cursorToggleKey];
            if (key == null || !key.wasPressedThisFrame)
                return;

            bool shouldLock = Cursor.lockState != CursorLockMode.Locked;
            Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !shouldLock;
        }

        private void UpdateTerraforming()
        {
            if (terraformController == null || planetCollider == null)
                return;

            Vector3 closestPoint = planetCollider.ClosestPoint(transform.position);
            float distance = Vector3.Distance(transform.position, closestPoint);
            if (distance > activationDistance)
                return;

            float proximity = 1f - Mathf.Clamp01(distance / Mathf.Max(0.001f, activationDistance));
            float amount = terraformAmountPerSecond * proximity * Time.deltaTime;

            if (useDirectVertexPaint)
                terraformController.PaintVerticesImmediate(closestPoint, terraformRadius, amount);
            else
                terraformController.PaintImmediate(closestPoint, terraformRadius, amount);

        }

        private void UpdateMagnetState()
        {
            if (!surfaceMagnetEnabled || terraformController == null || planetCollider == null)
            {
                magnetized = false;
                return;
            }

            if (Time.time < magnetDisabledUntil)
                return;

            Vector3 fromCenter = transform.position - terraformController.WorldCenter;
            float centerDistance = fromCenter.magnitude;
            float surfaceDistance = centerDistance - terraformController.WorldSurfaceRadius;

            if (!magnetized && surfaceDistance >= -hoverHeight && surfaceDistance <= magnetEnterDistance)
            {
                magnetized = true;
                pitch = 0f;
                magnetForward = Vector3.ProjectOnPlane(transform.forward, fromCenter.normalized);
                if (magnetForward.sqrMagnitude <= 0.0001f)
                    magnetForward = Vector3.Cross(fromCenter.normalized, transform.right);
                magnetForward.Normalize();
            }
            else if (magnetized && (surfaceDistance < -hoverHeight * 1.5f || surfaceDistance >= magnetExitDistance))
            {
                magnetized = false;
            }

            if (!magnetized)
                return;

            Vector3 normal = fromCenter.normalized;
            if (normal.sqrMagnitude > 0.0001f)
                surfaceUp = Vector3.Slerp(surfaceUp, normal, 1f - Mathf.Exp(-magnetAlignSpeed * Time.deltaTime));
        }

        private void UpdateHoverHeight()
        {
            if (!magnetized || planetCollider == null)
                return;

            Vector3 closestPoint = terraformController.WorldCenter + surfaceUp * terraformController.WorldSurfaceRadius;
            Vector3 targetPosition = closestPoint + surfaceUp * hoverHeight;
            transform.position = Vector3.Lerp(transform.position, targetPosition, 1f - Mathf.Exp(-magnetAlignSpeed * Time.deltaTime));
        }

        private static float NormalizePitch(float value)
        {
            return value > 180f ? value - 360f : value;
        }

        private static MeshCollider EnsureMeshCollider(PlanetTerraformController controller)
        {
            MeshFilter meshFilter = controller.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return null;

            MeshCollider meshCollider = controller.gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;
            return meshCollider;
        }
    }

    public sealed class PlanetSequenceManager : MonoBehaviour
    {
        [SerializeField] private GameObject[] planetPrefabs;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private TerraformFlyCamera flyCamera;
        [SerializeField, Range(0f, 1f)] private float completeProgress = 0.98f;
        [SerializeField, Min(0f)] private float nextPlanetDelay = 1.25f;
        [SerializeField] private bool spawnFirstOnStart = true;
        [SerializeField] private bool destroyPreviousPlanet = true;

        private PlanetTerraformController currentPlanet;
        private int currentIndex = -1;
        private bool switching;

        public PlanetTerraformController CurrentPlanet => currentPlanet;

        private void Start()
        {
            if (flyCamera == null)
                flyCamera = FindFirstObjectByType<TerraformFlyCamera>();

            if (spawnFirstOnStart)
                SpawnPlanet(0);
        }

        private void Update()
        {
            if (switching || currentPlanet == null)
                return;

            if (currentPlanet.VisibleTerraformProgress >= completeProgress)
                StartCoroutine(SpawnNextAfterDelay());
        }

        [ContextMenu("Spawn Next Planet")]
        public void SpawnNextPlanet()
        {
            SpawnPlanet(currentIndex + 1);
        }

        public void SpawnPlanet(int index)
        {
            if (planetPrefabs == null || planetPrefabs.Length == 0)
                return;

            if (index < 0 || index >= planetPrefabs.Length)
                return;

            GameObject prefab = planetPrefabs[index];
            if (prefab == null)
                return;

            if (destroyPreviousPlanet && currentPlanet != null)
                Destroy(currentPlanet.gameObject);

            Transform targetSpawn = spawnPoint != null ? spawnPoint : transform;
            GameObject instance = Instantiate(prefab, targetSpawn.position, targetSpawn.rotation);
            currentPlanet = instance.GetComponentInChildren<PlanetTerraformController>();
            currentIndex = index;

            if (flyCamera != null && currentPlanet != null)
                flyCamera.SetTarget(currentPlanet);
        }

        private IEnumerator SpawnNextAfterDelay()
        {
            switching = true;
            yield return new WaitForSeconds(nextPlanetDelay);
            SpawnPlanet(currentIndex + 1);
            switching = false;
        }
    }
}
