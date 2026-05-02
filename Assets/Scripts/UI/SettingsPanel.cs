using UnityEngine;
using UnityEngine.UI;

namespace LittlePlanet.UI
{
    public sealed class SettingsPanel : MonoBehaviour, IManagedWindow
    {
        [Header("References")]
        [SerializeField] private Button settingsButton;
        [SerializeField] private string settingsButtonObjectName = "Settings";
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private WindowManager windowManager;

        [Header("State")]
        [SerializeField] private bool startOpen;

        private bool _isOpen;

        public bool IsWindowOpen => _isOpen;

        private void Awake()
        {
            ResolveReferences();
            SetWindowOpen(startOpen);
        }

        private void OnEnable()
        {
            ResolveReferences();
            windowManager?.Register(this);
            BindButton();
        }

        private void OnDisable()
        {
            UnbindButton();
            windowManager?.Unregister(this);
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

        private void BindButton()
        {
            if (settingsButton == null)
            {
                return;
            }

            settingsButton.onClick.RemoveListener(Toggle);
            settingsButton.onClick.AddListener(Toggle);
        }

        private void UnbindButton()
        {
            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(Toggle);
            }
        }
    }
}
