using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LittlePlanet.RuntimeInput
{
    public static class InputCompat
    {
        private static bool leftPressedPreviousFrame;
        private static int leftPressFrame = -1;
        private static bool leftPressCached;

        public static bool IsLeftMousePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                return true;
            }

            if (Pen.current != null && Pen.current.tip.isPressed)
            {
                return true;
            }

            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                return true;
            }

            if (Pointer.current != null && Pointer.current.press.isPressed)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButton(0))
            {
                return true;
            }
#endif

            return false;
        }

        public static bool WasLeftMousePressedThisFrame()
        {
            var frame = Time.frameCount;
            if (leftPressFrame == frame)
            {
                return leftPressCached;
            }

            var currentlyPressed = IsLeftMousePressed();
            var pressedThisFrame = currentlyPressed && !leftPressedPreviousFrame;

#if ENABLE_INPUT_SYSTEM
            if (!pressedThisFrame && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                pressedThisFrame = true;
            }

            if (!pressedThisFrame && Pen.current != null && Pen.current.tip.wasPressedThisFrame)
            {
                pressedThisFrame = true;
            }

            if (!pressedThisFrame && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                pressedThisFrame = true;
            }

            if (!pressedThisFrame && Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                pressedThisFrame = true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (!pressedThisFrame && Input.GetMouseButtonDown(0))
            {
                pressedThisFrame = true;
            }
#endif

            leftPressedPreviousFrame = currentlyPressed;
            leftPressFrame = frame;
            leftPressCached = pressedThisFrame;
            return pressedThisFrame;
        }

        public static Vector2 GetMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return ClampToScreen(Mouse.current.position.ReadValue());
            }

            if (Pen.current != null)
            {
                return ClampToScreen(Pen.current.position.ReadValue());
            }

            if (Touchscreen.current != null)
            {
                return ClampToScreen(Touchscreen.current.primaryTouch.position.ReadValue());
            }

            if (Pointer.current != null)
            {
                return ClampToScreen(Pointer.current.position.ReadValue());
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return ClampToScreen(Input.mousePosition);
#else
            return Vector2.zero;
#endif
        }

        public static bool HasPointer()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null
                || Pen.current != null
                || Touchscreen.current != null
                || Pointer.current != null;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return true;
#else
            return false;
#endif
        }

        private static Vector2 ClampToScreen(Vector2 position)
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return position;
            }

            var x = Mathf.Clamp(position.x, 0f, Screen.width);
            var y = Mathf.Clamp(position.y, 0f, Screen.height);
            return new Vector2(x, y);
        }
    }
}
