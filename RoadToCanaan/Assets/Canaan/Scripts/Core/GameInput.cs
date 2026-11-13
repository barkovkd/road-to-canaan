using UnityEngine;
using UnityEngine.InputSystem;

namespace Canaan
{
    public enum Btn
    {
        Sprint, Walk, Crouch, Jump, Cover, Interact, Melee, Aim, Fire, DeadEye, Sense, Reload, Wheel, Whistle, View,
        Satchel, Map, Pause, Slot1, Slot2, Slot3, Slot4, Slot5, Slot6,
        Up, Down, Left, Right, Accept, Back,
        Count
    }

    /// <summary>
    /// All player input goes through here. Devices are polled once per frame; the autotest bot switches to
    /// virtual mode and presses the same buttons, so tests exercise the real control code.
    /// </summary>
    public static class GameInput
    {
        const int N = (int)Btn.Count;
        public static Vector2 Move;          // x: strafe, y: forward, length 0..1
        public static Vector2 Look;          // degrees to turn this frame (x: yaw, y: pitch up)
        public static Vector2 Pointer;       // mouse position in screen pixels
        public static bool PointerMoved;
        public static bool Gamepad;
        /// <summary>The aim is held on the keyboard (Z), not the mouse: like a stick, it then swings onto the nearest foe.</summary>
        public static bool AimKey;
        public static float MouseSensitivity = 0.11f, StickSpeed = 170f;

        static readonly bool[] held = new bool[N], prev = new bool[N];

        // virtual device (autotest)
        public static bool Virtual;
        public static Vector2 vMove, vLook;
        public static readonly bool[] vHeld = new bool[N];
        static readonly bool[] vTap = new bool[N];

        public static bool Held(Btn b) { return held[(int)b]; }
        public static bool Pressed(Btn b) { return held[(int)b] && !prev[(int)b]; }
        public static bool Released(Btn b) { return !held[(int)b] && prev[(int)b]; }

        /// <summary>Presses a virtual button for exactly one frame.</summary>
        public static void Tap(Btn b) { vTap[(int)b] = true; }

        public static void ClearVirtual()
        {
            vMove = Vector2.zero; vLook = Vector2.zero;
            for (int i = 0; i < N; i++) { vHeld[i] = false; vTap[i] = false; }
        }

        public static void SetCursor(bool gameplay)
        {
            if (Virtual) return;
            Cursor.lockState = gameplay ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !gameplay;
        }

        public static void Tick(float unscaledDt)
        {
            for (int i = 0; i < N; i++) prev[i] = held[i];
            if (Virtual)
            {
                Move = Vector2.ClampMagnitude(vMove, 1f);
                Look = vLook;
                for (int i = 0; i < N; i++) { held[i] = vHeld[i] || vTap[i]; vTap[i] = false; }
                PointerMoved = false;
                AimKey = false;
                return;
            }
            for (int i = 0; i < N; i++) held[i] = false;
            Move = Vector2.zero;
            Look = Vector2.zero;

            Keyboard kb = Keyboard.current;
            Mouse ms = Mouse.current;
            Gamepad gp = UnityEngine.InputSystem.Gamepad.current;

            if (kb != null)
            {
                // WASD or the arrows; the aim and the shot on Z and C as well as on the mouse, beside X (dead eye),
                // so the left hand can do it all while the right one is on the arrows
                float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
                float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
                Move = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
                Or(Btn.Sprint, kb.leftShiftKey.isPressed);
                Or(Btn.Walk, kb.capsLockKey.isPressed);
                Or(Btn.Crouch, kb.leftCtrlKey.isPressed);
                Or(Btn.Jump, kb.spaceKey.isPressed);
                Or(Btn.Cover, kb.qKey.isPressed);
                Or(Btn.Interact, kb.eKey.isPressed);
                Or(Btn.Melee, kb.fKey.isPressed);
                Or(Btn.DeadEye, kb.xKey.isPressed);
                Or(Btn.Aim, kb.zKey.isPressed);
                Or(Btn.Fire, kb.cKey.isPressed);
                AimKey = kb.zKey.isPressed;
                Or(Btn.Sense, kb.leftAltKey.isPressed);
                Or(Btn.Reload, kb.rKey.isPressed);
                Or(Btn.Wheel, kb.tabKey.isPressed);
                Or(Btn.Whistle, kb.hKey.isPressed);
                Or(Btn.View, kb.vKey.isPressed);
                Or(Btn.Satchel, kb.iKey.isPressed);
                Or(Btn.Map, kb.mKey.isPressed);
                Or(Btn.Pause, kb.escapeKey.isPressed);
                Or(Btn.Slot1, kb.digit1Key.isPressed);
                Or(Btn.Slot2, kb.digit2Key.isPressed);
                Or(Btn.Slot3, kb.digit3Key.isPressed);
                Or(Btn.Slot4, kb.digit4Key.isPressed);
                Or(Btn.Slot5, kb.digit5Key.isPressed);
                Or(Btn.Slot6, kb.digit6Key.isPressed);
                Or(Btn.Up, kb.upArrowKey.isPressed || kb.wKey.isPressed);
                Or(Btn.Down, kb.downArrowKey.isPressed || kb.sKey.isPressed);
                Or(Btn.Left, kb.leftArrowKey.isPressed || kb.aKey.isPressed);
                Or(Btn.Right, kb.rightArrowKey.isPressed || kb.dKey.isPressed);
                Or(Btn.Accept, kb.enterKey.isPressed || kb.spaceKey.isPressed || kb.eKey.isPressed);
                Or(Btn.Back, kb.escapeKey.isPressed || kb.backspaceKey.isPressed);
            }
            if (ms != null)
            {
                Vector2 d = ms.delta.ReadValue();
                Look += new Vector2(d.x, d.y) * MouseSensitivity;
                Or(Btn.Aim, ms.rightButton.isPressed);
                Or(Btn.Fire, ms.leftButton.isPressed);
                Or(Btn.DeadEye, ms.middleButton.isPressed);
                Vector2 pos = ms.position.ReadValue();
                PointerMoved = (pos - Pointer).sqrMagnitude > 0.5f;
                Pointer = pos;
            }
            if (gp != null)
            {
                Vector2 ls = gp.leftStick.ReadValue(), rs = gp.rightStick.ReadValue();
                if (ls.magnitude > 0.18f) { Move = Vector2.ClampMagnitude(ls, 1f); Gamepad = true; }
                if (rs.magnitude > 0.15f) { Look += new Vector2(rs.x, rs.y) * (StickSpeed * unscaledDt); Gamepad = true; }
                Or(Btn.Sprint, gp.buttonSouth.isPressed);
                Or(Btn.Jump, gp.buttonWest.isPressed);
                Or(Btn.Interact, gp.buttonNorth.isPressed);
                Or(Btn.Melee, gp.buttonEast.isPressed);
                Or(Btn.Cover, gp.rightShoulder.isPressed);
                Or(Btn.Wheel, gp.leftShoulder.isPressed);
                Or(Btn.Aim, gp.leftTrigger.ReadValue() > 0.4f);
                Or(Btn.Fire, gp.rightTrigger.ReadValue() > 0.4f);
                Or(Btn.DeadEye, gp.rightStickButton.isPressed && !gp.leftStickButton.isPressed);
                Or(Btn.Sense, gp.rightStickButton.isPressed && gp.leftStickButton.isPressed);
                Or(Btn.Crouch, gp.leftStickButton.isPressed && !gp.rightStickButton.isPressed);
                Or(Btn.Whistle, gp.dpad.up.isPressed);
                Or(Btn.Reload, gp.dpad.left.isPressed);
                Or(Btn.Satchel, gp.dpad.right.isPressed);
                Or(Btn.Map, gp.dpad.down.isPressed);
                Or(Btn.View, gp.selectButton.isPressed);
                Or(Btn.Pause, gp.startButton.isPressed);
                Or(Btn.Up, gp.dpad.up.isPressed || ls.y > 0.6f);
                Or(Btn.Down, gp.dpad.down.isPressed || ls.y < -0.6f);
                Or(Btn.Left, gp.dpad.left.isPressed || ls.x < -0.6f);
                Or(Btn.Right, gp.dpad.right.isPressed || ls.x > 0.6f);
                Or(Btn.Accept, gp.buttonSouth.isPressed);
                Or(Btn.Back, gp.buttonEast.isPressed);
            }
            if (kb != null && kb.anyKey.isPressed) Gamepad = false;
        }

        static void Or(Btn b, bool v) { if (v) held[(int)b] = true; }

        /// <summary>Text shown in prompts for a button, for the device in use.</summary>
        public static string Label(Btn b)
        {
            if (Gamepad)
            {
                switch (b)
                {
                    case Btn.Interact: return "Y";
                    case Btn.Melee: return "B";
                    case Btn.Jump: return "X";
                    case Btn.Sprint: return "A";
                    case Btn.Cover: return "RB";
                    case Btn.Wheel: return "LB";
                    case Btn.Aim: return "LT";
                    case Btn.Fire: return "RT";
                    case Btn.Accept: return "A";
                    case Btn.Back: return "B";
                }
            }
            switch (b)
            {
                case Btn.Interact: return "E";
                case Btn.Melee: return "F";
                case Btn.Jump: return "Пробел";
                case Btn.Sprint: return "Shift";
                case Btn.Cover: return "Q";
                case Btn.Wheel: return "Tab";
                case Btn.Aim: return "ПКМ/Z";
                case Btn.Fire: return "ЛКМ/C";
                case Btn.Reload: return "R";
                case Btn.Whistle: return "H";
                case Btn.Crouch: return "Ctrl";
                case Btn.Accept: return "Enter";
                case Btn.Back: return "Esc";
                case Btn.Satchel: return "I";
                case Btn.Map: return "M";
                case Btn.DeadEye: return "X";
                case Btn.Sense: return "Alt";
                default: return b.ToString();
            }
        }
    }
}
