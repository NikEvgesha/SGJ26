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

        public static bool WasKeyPressedThisFrame(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboardKey = ToInputSystemKey(keyCode);
            if (keyboardKey != Key.None && Keyboard.current != null && Keyboard.current[keyboardKey].wasPressedThisFrame)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(keyCode))
            {
                return true;
            }
#endif

            return false;
        }

        public static bool WasNumberKeyPressedThisFrame(int number)
        {
            if (number < 0 || number > 9)
            {
                return false;
            }

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                var digitKey = NumberToDigitKey(number);
                var numpadKey = NumberToNumpadKey(number);
                if (Keyboard.current[digitKey].wasPressedThisFrame || Keyboard.current[numpadKey].wasPressedThisFrame)
                {
                    return true;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            var alphaKey = NumberToAlphaKey(number);
            var keypadKey = NumberToKeypadKey(number);
            if (Input.GetKeyDown(alphaKey) || Input.GetKeyDown(keypadKey))
            {
                return true;
            }
#endif

            return false;
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

#if ENABLE_INPUT_SYSTEM
        private static Key NumberToDigitKey(int number)
        {
            return number switch
            {
                0 => Key.Digit0,
                1 => Key.Digit1,
                2 => Key.Digit2,
                3 => Key.Digit3,
                4 => Key.Digit4,
                5 => Key.Digit5,
                6 => Key.Digit6,
                7 => Key.Digit7,
                8 => Key.Digit8,
                9 => Key.Digit9,
                _ => Key.None
            };
        }

        private static Key NumberToNumpadKey(int number)
        {
            return number switch
            {
                0 => Key.Numpad0,
                1 => Key.Numpad1,
                2 => Key.Numpad2,
                3 => Key.Numpad3,
                4 => Key.Numpad4,
                5 => Key.Numpad5,
                6 => Key.Numpad6,
                7 => Key.Numpad7,
                8 => Key.Numpad8,
                9 => Key.Numpad9,
                _ => Key.None
            };
        }

        private static Key ToInputSystemKey(KeyCode keyCode)
        {
            return keyCode switch
            {
                KeyCode.A => Key.A,
                KeyCode.B => Key.B,
                KeyCode.C => Key.C,
                KeyCode.D => Key.D,
                KeyCode.E => Key.E,
                KeyCode.F => Key.F,
                KeyCode.G => Key.G,
                KeyCode.H => Key.H,
                KeyCode.I => Key.I,
                KeyCode.J => Key.J,
                KeyCode.K => Key.K,
                KeyCode.L => Key.L,
                KeyCode.M => Key.M,
                KeyCode.N => Key.N,
                KeyCode.O => Key.O,
                KeyCode.P => Key.P,
                KeyCode.Q => Key.Q,
                KeyCode.R => Key.R,
                KeyCode.S => Key.S,
                KeyCode.T => Key.T,
                KeyCode.U => Key.U,
                KeyCode.V => Key.V,
                KeyCode.W => Key.W,
                KeyCode.X => Key.X,
                KeyCode.Y => Key.Y,
                KeyCode.Z => Key.Z,
                KeyCode.Escape => Key.Escape,
                KeyCode.Space => Key.Space,
                KeyCode.Tab => Key.Tab,
                KeyCode.LeftShift => Key.LeftShift,
                KeyCode.RightShift => Key.RightShift,
                KeyCode.LeftControl => Key.LeftCtrl,
                KeyCode.RightControl => Key.RightCtrl,
                KeyCode.LeftAlt => Key.LeftAlt,
                KeyCode.RightAlt => Key.RightAlt,
                _ => Key.None
            };
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        private static KeyCode NumberToAlphaKey(int number)
        {
            return number switch
            {
                0 => KeyCode.Alpha0,
                1 => KeyCode.Alpha1,
                2 => KeyCode.Alpha2,
                3 => KeyCode.Alpha3,
                4 => KeyCode.Alpha4,
                5 => KeyCode.Alpha5,
                6 => KeyCode.Alpha6,
                7 => KeyCode.Alpha7,
                8 => KeyCode.Alpha8,
                9 => KeyCode.Alpha9,
                _ => KeyCode.None
            };
        }

        private static KeyCode NumberToKeypadKey(int number)
        {
            return number switch
            {
                0 => KeyCode.Keypad0,
                1 => KeyCode.Keypad1,
                2 => KeyCode.Keypad2,
                3 => KeyCode.Keypad3,
                4 => KeyCode.Keypad4,
                5 => KeyCode.Keypad5,
                6 => KeyCode.Keypad6,
                7 => KeyCode.Keypad7,
                8 => KeyCode.Keypad8,
                9 => KeyCode.Keypad9,
                _ => KeyCode.None
            };
        }
#endif
    }
}
