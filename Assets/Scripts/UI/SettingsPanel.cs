using UnityEngine;
using UnityEngine.UI;
using LittlePlanet.RuntimeInput;

namespace LittlePlanet.UI
{
    public sealed class SettingsPanel : MonoBehaviour, IManagedWindow
    {
        [Header("References")]
        [SerializeField] private Button settingsButton;
        [SerializeField] private string settingsButtonObjectName = "SettingsButton";
        [SerializeField] private Button closeButton;
        [SerializeField] private string closeButtonObjectName = "CloseArea";
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private WindowManager windowManager;

        private bool _isOpen;

        public bool IsWindowOpen => _isOpen;

        private void Awake()
        {
            ResolveReferences();
            SetWindowOpen(false);
        }

        private void OnEnable()
        {
            ResolveReferences();
            windowManager?.Register(this);
            BindButtons();
        }

        private void OnDisable()
        {
            UnbindButtons();
            windowManager?.Unregister(this);
        }

        private void Update()
        {
            if (InputCompat.WasKeyPressedThisFrame(KeyCode.Tab))
            {
                Toggle();
            }
        }

        public void Toggle()
        {
            ResolveReferences();
            if (windowManager != null)
            {
                windowManager.ToggleExclusive(this);
                return;
            }

            SetWindowOpen(!_isOpen);
        }

        public void SetWindowOpen(bool isOpen)
        {
            _isOpen = isOpen;
            EnsureCanvasGroup();
            canvasGroup.alpha = isOpen ? 1f : 0f;
            canvasGroup.interactable = isOpen;
            canvasGroup.blocksRaycasts = isOpen;
        }

        private void ResolveReferences()
        {
            EnsureCanvasGroup();
            if (windowManager == null)
            {
                windowManager = WindowManager.Instance;
            }

            if (settingsButton == null && !string.IsNullOrWhiteSpace(settingsButtonObjectName))
            {
                var buttonObject = GameObject.Find(settingsButtonObjectName);
                if (buttonObject != null)
                {
                    settingsButton = buttonObject.GetComponent<Button>();
                }
            }

            if (settingsButton == null)
            {
                var fallbackButtonObject = GameObject.Find("Settings");
                if (fallbackButtonObject != null)
                {
                    settingsButton = fallbackButtonObject.GetComponent<Button>();
                }
            }

            if (closeButton == null)
            {
                var closeButtonTransform = FindChildByName(transform, closeButtonObjectName);
                if (closeButtonTransform != null)
                {
                    closeButton = closeButtonTransform.GetComponent<Button>();
                }
            }
        }

        private void EnsureCanvasGroup()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void BindButtons()
        {
            if (settingsButton == null)
            {
                ResolveReferences();
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(Toggle);
                settingsButton.onClick.AddListener(Toggle);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseWindow);
                closeButton.onClick.AddListener(CloseWindow);
            }
        }

        private void UnbindButtons()
        {
            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(Toggle);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseWindow);
            }
        }

        private void CloseWindow()
        {
            SetWindowOpen(false);
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            var children = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < children.Length; i++)
            {
                var child = children[i];
                if (child != null && string.Equals(child.name, childName, System.StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }
    }
}
