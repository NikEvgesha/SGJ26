using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace LittlePlanet.RuntimeInput
{
    public static class UiInputRuntime
    {
        private static readonly List<RaycastResult> PointerRaycastResults = new(32);

#if ENABLE_INPUT_SYSTEM
        private static InputActionReference pointReference;
        private static InputActionReference leftClickReference;
        private static InputActionReference rightClickReference;
        private static InputActionReference middleClickReference;
        private static InputActionReference scrollWheelReference;
        private static InputActionReference moveReference;
        private static InputActionReference submitReference;
        private static InputActionReference cancelReference;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeSceneLoad()
        {
            EnsureInputUpdateMode();
            EnsureEventSystem();
        }

        public static void EnsureEventSystem()
        {
            var eventSystem = EventSystem.current != null
                ? EventSystem.current
                : Object.FindFirstObjectByType<EventSystem>();

            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
                Object.DontDestroyOnLoad(eventSystemObject);
            }

            if (eventSystem == null)
            {
                return;
            }

            if (!eventSystem.gameObject.activeSelf)
            {
                eventSystem.gameObject.SetActive(true);
            }

#if ENABLE_INPUT_SYSTEM
            EnsurePointerDevicesEnabled();
#endif

            eventSystem.enabled = true;
            EnsureInputModules(eventSystem.gameObject);
            EnsureSingleActiveEventSystem(eventSystem);
            SanitizeUiRaycasts();
        }

        public static bool IsPointerOverInteractiveUi()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || !InputCompat.HasPointer())
            {
                return false;
            }

            var pointerEventData = new PointerEventData(eventSystem)
            {
                position = InputCompat.GetMousePosition()
            };

            PointerRaycastResults.Clear();
            eventSystem.RaycastAll(pointerEventData, PointerRaycastResults);

            for (var i = 0; i < PointerRaycastResults.Count; i++)
            {
                var hitObject = PointerRaycastResults[i].gameObject;
                if (hitObject == null || !hitObject.activeInHierarchy)
                {
                    continue;
                }

                var selectable = hitObject.GetComponentInParent<Selectable>();
                if (selectable == null || !selectable.IsActive() || !selectable.interactable)
                {
                    continue;
                }

                if (IsBlockedByCanvasGroup(selectable.transform))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static void EnsureInputModules(GameObject eventSystemObject)
        {
            if (eventSystemObject == null)
            {
                return;
            }

#if ENABLE_INPUT_SYSTEM
            var inputSystemModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
            if (inputSystemModule == null)
            {
                inputSystemModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            }

            ConfigureInputSystemModule(inputSystemModule);
            inputSystemModule.enabled = true;
            if (inputSystemModule.actionsAsset != null && !inputSystemModule.actionsAsset.enabled)
            {
                inputSystemModule.actionsAsset.Enable();
            }
#endif

            var legacyModule = eventSystemObject.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                legacyModule.enabled = false;
            }
        }

        private static bool IsBlockedByCanvasGroup(Transform target)
        {
            var canvasGroups = target.GetComponentsInParent<CanvasGroup>(includeInactive: true);
            for (var i = 0; i < canvasGroups.Length; i++)
            {
                var group = canvasGroups[i];
                if (group != null && !group.blocksRaycasts)
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureSingleActiveEventSystem(EventSystem primaryEventSystem)
        {
            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            for (var i = 0; i < eventSystems.Length; i++)
            {
                var candidate = eventSystems[i];
                if (candidate == null || candidate == primaryEventSystem)
                {
                    continue;
                }

                candidate.enabled = false;

#if ENABLE_INPUT_SYSTEM
                var inputSystemModule = candidate.GetComponent<InputSystemUIInputModule>();
                if (inputSystemModule != null)
                {
                    inputSystemModule.enabled = false;
                }
#endif

                var legacyModule = candidate.GetComponent<StandaloneInputModule>();
                if (legacyModule != null)
                {
                    legacyModule.enabled = false;
                }
            }
        }

        private static void EnsureInputUpdateMode()
        {
#if ENABLE_INPUT_SYSTEM
            if (InputSystem.settings == null)
            {
                return;
            }

            if (InputSystem.settings.updateMode != InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
            {
                InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            }

#if UNITY_EDITOR
            if (InputSystem.settings.editorInputBehaviorInPlayMode != InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView)
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            }

            if (InputSystem.settings.backgroundBehavior != InputSettings.BackgroundBehavior.IgnoreFocus)
            {
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            }
#endif
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static void ConfigureInputSystemModule(InputSystemUIInputModule inputSystemModule)
        {
            if (inputSystemModule == null)
            {
                return;
            }

            var projectActions = InputSystem.actions;
            if (projectActions == null)
            {
                inputSystemModule.AssignDefaultActions();
                return;
            }

            var point = projectActions.FindAction("UI/Point", throwIfNotFound: false);
            var click = projectActions.FindAction("UI/Click", throwIfNotFound: false);
            var rightClick = projectActions.FindAction("UI/RightClick", throwIfNotFound: false);
            var middleClick = projectActions.FindAction("UI/MiddleClick", throwIfNotFound: false);
            var scrollWheel = projectActions.FindAction("UI/ScrollWheel", throwIfNotFound: false);
            var navigate = projectActions.FindAction("UI/Navigate", throwIfNotFound: false);
            var submit = projectActions.FindAction("UI/Submit", throwIfNotFound: false);
            var cancel = projectActions.FindAction("UI/Cancel", throwIfNotFound: false);

            if (point == null || click == null || navigate == null || submit == null || cancel == null)
            {
                inputSystemModule.AssignDefaultActions();
                return;
            }

            if (!projectActions.enabled)
            {
                projectActions.Enable();
            }

            projectActions.bindingMask = null;
            projectActions.devices = null;

            pointReference = InputActionReference.Create(point);
            leftClickReference = InputActionReference.Create(click);
            rightClickReference = CreateOptionalReference(rightClick);
            middleClickReference = CreateOptionalReference(middleClick);
            scrollWheelReference = CreateOptionalReference(scrollWheel);
            moveReference = InputActionReference.Create(navigate);
            submitReference = InputActionReference.Create(submit);
            cancelReference = InputActionReference.Create(cancel);

            inputSystemModule.actionsAsset = projectActions;
            inputSystemModule.point = pointReference;
            inputSystemModule.leftClick = leftClickReference;
            inputSystemModule.rightClick = rightClickReference;
            inputSystemModule.middleClick = middleClickReference;
            inputSystemModule.scrollWheel = scrollWheelReference;
            inputSystemModule.move = moveReference;
            inputSystemModule.submit = submitReference;
            inputSystemModule.cancel = cancelReference;
            inputSystemModule.pointerBehavior = UIPointerBehavior.AllPointersAsIs;
        }

        private static InputActionReference CreateOptionalReference(InputAction action)
        {
            return action != null ? InputActionReference.Create(action) : null;
        }

        private static void EnsurePointerDevicesEnabled()
        {
            EnableDeviceIfNeeded(Mouse.current);
            EnableDeviceIfNeeded(Touchscreen.current);
            EnableDeviceIfNeeded(Pen.current);
            EnableDeviceIfNeeded(Keyboard.current);
        }

        private static void EnableDeviceIfNeeded(InputDevice device)
        {
            if (device == null || device.enabled)
            {
                return;
            }

            InputSystem.EnableDevice(device);
        }
#endif

        private static void SanitizeUiRaycasts()
        {
            var graphics = Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None);
            for (var i = 0; i < graphics.Length; i++)
            {
                var graphic = graphics[i];
                if (graphic == null || !graphic.raycastTarget)
                {
                    continue;
                }

                if (graphic.GetComponentInParent<Selectable>(includeInactive: true) != null)
                {
                    continue;
                }

                graphic.raycastTarget = false;
            }
        }
    }
}
