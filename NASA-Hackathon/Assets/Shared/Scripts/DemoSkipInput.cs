using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NasaHack
{
    /// <summary>Keyboard-only input helper for the desktop demo shortcuts.</summary>
    public static class DemoSkipInput
    {
        public static bool WasAnyKeyboardKeyPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if (key == KeyCode.None || key >= KeyCode.Mouse0)
                    continue;
                if (Input.GetKeyDown(key))
                    return true;
            }
#endif

            return false;
        }
    }
}
