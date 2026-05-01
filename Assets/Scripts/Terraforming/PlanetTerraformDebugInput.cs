using UnityEngine;
using UnityEngine.InputSystem;

namespace SGJ26.Terraforming
{
    public static class PlanetInputRuntime
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeSceneLoad()
        {
            EnsurePointerInputReady();
        }

        public static void EnsurePointerInputReady()
        {
            EnsureInputUpdateMode();
            EnableDeviceIfNeeded(Mouse.current);
            EnableDeviceIfNeeded(Keyboard.current);
            EnableDeviceIfNeeded(Touchscreen.current);
            EnableDeviceIfNeeded(Pen.current);
        }

        private static void EnsureInputUpdateMode()
        {
            if (InputSystem.settings == null)
                return;

            if (InputSystem.settings.updateMode != InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
                InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;

#if UNITY_EDITOR
            if (InputSystem.settings.editorInputBehaviorInPlayMode != InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView)
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            if (InputSystem.settings.backgroundBehavior != InputSettings.BackgroundBehavior.IgnoreFocus)
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#endif
        }

        private static void EnableDeviceIfNeeded(InputDevice device)
        {
            if (device == null || device.enabled)
                return;

            InputSystem.EnableDevice(device);
        }
    }

    public sealed class PlanetTerraformDebugInput : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private PlanetTerraformController terraformController;
        [SerializeField] private LayerMask raycastMask = ~0;
        [SerializeField, Min(0.01f)] private float probeRadius = 1.6f;
        [SerializeField, Min(0f)] private float probeStrength = 0.75f;
        [SerializeField, Min(0.01f)] private float probeDuration = 8f;
        [SerializeField, Min(0.01f)] private float instantPaintRadius = 2.2f;
        [SerializeField, Min(0f)] private float instantPaintAmount = 0.45f;
        [SerializeField, Min(0.01f)] private float rotateSpeed = 0.18f;
        [SerializeField] private bool enablePlanetRotation;
        [SerializeField] private bool enableClickLogs;
        [SerializeField] private bool addRuntimeMeshColliderIfMissing = true;

        private bool setupLogged;

        private void Reset()
        {
            targetCamera = Camera.main;
            terraformController = FindFirstObjectByType<PlanetTerraformController>();
        }

        private void OnEnable()
        {
            PlanetInputRuntime.EnsurePointerInputReady();
            EnsureRuntimeSetup();
        }

        private void Update()
        {
            PlanetInputRuntime.EnsurePointerInputReady();
            EnsureRuntimeSetup();

            if (targetCamera == null)
                targetCamera = Camera.main;

            if (targetCamera == null || terraformController == null)
                return;

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                TryDropProbe();
            }

            bool shiftPressed = IsShiftPressed();

            if (mouse.rightButton.wasPressedThisFrame && shiftPressed)
            {
                TryPaintImmediate();
            }

            if (enablePlanetRotation && mouse.rightButton.isPressed && !shiftPressed)
                RotatePlanet(mouse.delta.ReadValue());
        }

        private void EnsureRuntimeSetup()
        {
            if (terraformController == null)
                terraformController = FindFirstObjectByType<PlanetTerraformController>();

            if (targetCamera == null)
                targetCamera = Camera.main;

            if (terraformController != null && addRuntimeMeshColliderIfMissing)
                EnsurePlanetCollider(terraformController);

            if (setupLogged)
                return;

            setupLogged = true;
        }

        private void TryDropProbe()
        {
            if (!TryGetHitPoint(out Vector3 hitPoint))
                return;

            terraformController.DropProbe(hitPoint, probeRadius, probeStrength, probeDuration);
        }

        private void TryPaintImmediate()
        {
            if (!TryGetHitPoint(out Vector3 hitPoint))
                return;

            terraformController.PaintImmediate(hitPoint, instantPaintRadius, instantPaintAmount);
        }

        private void RotatePlanet(Vector2 mouseDelta)
        {
            if (mouseDelta.sqrMagnitude <= 0.0001f || terraformController == null)
                return;

            Transform planetTransform = terraformController.transform;
            Vector3 yawAxis = targetCamera != null ? targetCamera.transform.up : Vector3.up;
            Vector3 pitchAxis = targetCamera != null ? targetCamera.transform.right : Vector3.right;

            planetTransform.Rotate(yawAxis, -mouseDelta.x * rotateSpeed, Space.World);
            planetTransform.Rotate(pitchAxis, mouseDelta.y * rotateSpeed, Space.World);
        }

        private static bool IsShiftPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        }

        private bool TryGetHitPoint(out Vector3 hitPoint)
        {
            hitPoint = default;

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return false;

            Vector2 mousePosition = mouse.position.ReadValue();
            Ray ray = targetCamera.ScreenPointToRay(mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, raycastMask, QueryTriggerInteraction.Ignore))
            {
                hitPoint = hit.point;
                return true;
            }

            return false;
        }

        private static void EnsurePlanetCollider(PlanetTerraformController controller)
        {
            if (controller == null || controller.GetComponent<Collider>() != null)
                return;

            MeshFilter meshFilter = controller.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return;

            MeshCollider collider = controller.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = meshFilter.sharedMesh;
        }
    }
}
