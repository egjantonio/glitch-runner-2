using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif


public static class GameInput
{
#if ENABLE_INPUT_SYSTEM
    private static bool Held(params Key[] keys)
    {
        var kb = Keyboard.current;
        if (kb == null) return false;
        foreach (var k in keys)
            if (kb[k].isPressed) return true;
        return false;
    }

    public static bool Left => Held(Key.A, Key.LeftArrow);
    public static bool Right => Held(Key.D, Key.RightArrow);
    public static bool JumpHeld => Held(Key.Space, Key.W, Key.UpArrow);
    public static bool AttackHeld => Held(Key.J, Key.Z, Key.X);
    public static bool RunHeld => Held(Key.LeftCtrl, Key.RightCtrl);
#else
    private static bool Held(params KeyCode[] keys)
    {
        foreach (var k in keys)
            if (Input.GetKey(k)) return true;
        return false;
    }

    public static bool Left => Held(KeyCode.A, KeyCode.LeftArrow);
    public static bool Right => Held(KeyCode.D, KeyCode.RightArrow);
    public static bool JumpHeld => Held(KeyCode.Space, KeyCode.W, KeyCode.UpArrow);
    public static bool AttackHeld => Held(KeyCode.J, KeyCode.Z, KeyCode.X);
    public static bool RunHeld => Held(KeyCode.LeftControl, KeyCode.RightControl);
#endif
}
