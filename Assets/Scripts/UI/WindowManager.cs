using System.Collections.Generic;
using UnityEngine;

namespace LittlePlanet.UI
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class WindowManager : MonoBehaviour
    {
        [SerializeField] private bool autoDiscoverWindows = true;
        [SerializeField] private List<MonoBehaviour> managedWindows = new();

        private static WindowManager _instance;

        public static WindowManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<WindowManager>(FindObjectsInactive.Include);
                }

                if (_instance == null && Application.isPlaying)
                {
                    var managerObject = new GameObject(nameof(WindowManager));
                    _instance = managerObject.AddComponent<WindowManager>();
                }

                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                return;
            }

            _instance = this;
            if (autoDiscoverWindows)
            {
                DiscoverWindows();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public void Register(MonoBehaviour window)
        {
            if (window is not IManagedWindow || managedWindows.Contains(window))
            {
                return;
            }

            managedWindows.Add(window);
        }

        public void Unregister(MonoBehaviour window)
        {
            managedWindows.Remove(window);
        }

        public void ToggleExclusive(MonoBehaviour window)
        {
            if (window is not IManagedWindow managedWindow)
            {
                return;
            }

            if (managedWindow.IsWindowOpen)
            {
                managedWindow.SetWindowOpen(false);
                return;
            }

            OpenExclusive(window);
        }

        public void OpenExclusive(MonoBehaviour window)
        {
            if (window is not IManagedWindow managedWindow)
            {
                return;
            }

            CloseAllExcept(window);
            managedWindow.SetWindowOpen(true);
        }

        public void CloseAll()
        {
            CloseAllExcept(null);
        }

        public void CloseAllExcept(MonoBehaviour exceptWindow)
        {
            for (var i = managedWindows.Count - 1; i >= 0; i--)
            {
                var window = managedWindows[i];
                if (window == null)
                {
                    managedWindows.RemoveAt(i);
                    continue;
                }

                if (window == exceptWindow || window is not IManagedWindow managedWindow)
                {
                    continue;
                }

                managedWindow.SetWindowOpen(false);
            }
        }

        [ContextMenu("Discover Windows")]
        private void DiscoverWindows()
        {
            var components = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < components.Length; i++)
            {
                Register(components[i]);
            }
        }
    }
}
