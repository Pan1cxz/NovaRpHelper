using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NovaRpHelper.Core
{
    /// <summary>
    /// Централизованный хелпер для всех WinAPI вызовов:
    /// - Глобальные хуки клавиатуры/мыши (WH_KEYBOARD_LL)
    /// - Эмуляция ввода (SendInput)
    /// - Управление прозрачностью окна (WS_EX_TRANSPARENT / click-through)
    /// - HotKey регистрация
    /// </summary>
    public static class WinApiHelper
    {
        #region ==================== Constants ====================

        // Window styles
        public const int GWL_EXSTYLE = -20;
        public const int GWL_STYLE = -16;

        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_TOPMOST = 0x00000008;
        public const int WS_EX_APPWINDOW = 0x00040000;

        // Hook types
        public const int WH_KEYBOARD_LL = 13;
        public const int WH_MOUSE_LL = 14;

        // Keyboard messages
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;

        // Mouse messages
        public const int WM_MOUSEMOVE = 0x0200;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_RBUTTONUP = 0x0205;

        // HotKey messages
        public const int WM_HOTKEY = 0x0312;

        // SendInput constants
        public const int INPUT_MOUSE = 0;
        public const int INPUT_KEYBOARD = 1;
        public const int INPUT_HARDWARE = 2;

        // Keyboard event flags
        public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint KEYEVENTF_UNICODE = 0x0004;
        public const uint KEYEVENTF_SCANCODE = 0x0008;

        // Mouse event flags
        public const uint MOUSEEVENTF_MOVE = 0x0001;
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        // HotKey modifiers
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        #endregion

        #region ==================== Virtual Keys ====================

        public static class VK
        {
            public const int LBUTTON = 0x01;
            public const int RBUTTON = 0x02;
            public const int CANCEL = 0x03;
            public const int MBUTTON = 0x04;

            public const int BACK = 0x08;
            public const int TAB = 0x09;
            public const int RETURN = 0x0D;
            public const int ENTER = 0x0D;
            public const int SHIFT = 0x10;
            public const int CONTROL = 0x11;
            public const int MENU = 0x12; // ALT
            public const int PAUSE = 0x13;
            public const int CAPITAL = 0x14;
            public const int ESCAPE = 0x1B;
            public const int SPACE = 0x20;
            public const int PRIOR = 0x21; // Page Up
            public const int NEXT = 0x22;  // Page Down
            public const int END = 0x23;
            public const int HOME = 0x24;
            public const int LEFT = 0x25;
            public const int UP = 0x26;
            public const int RIGHT = 0x27;
            public const int DOWN = 0x28;
            public const int SELECT = 0x29;
            public const int PRINT = 0x2A;
            public const int EXECUTE = 0x2B;
            public const int SNAPSHOT = 0x2C;
            public const int INSERT = 0x2D;
            public const int DELETE = 0x2E;
            public const int HELP = 0x2F;

            // Digits
            public const int D0 = 0x30;
            public const int D1 = 0x31;
            public const int D2 = 0x32;
            public const int D3 = 0x33;
            public const int D4 = 0x34;
            public const int D5 = 0x35;
            public const int D6 = 0x36;
            public const int D7 = 0x37;
            public const int D8 = 0x38;
            public const int D9 = 0x39;

            // Letters
            public const int A = 0x41;
            public const int B = 0x42;
            public const int C = 0x43;
            public const int D = 0x44;
            public const int E = 0x45;
            public const int F = 0x46;
            public const int G = 0x47;
            public const int H = 0x48;
            public const int I = 0x49;
            public const int J = 0x4A;
            public const int K = 0x4B;
            public const int L = 0x4C;
            public const int M = 0x4D;
            public const int N = 0x4E;
            public const int O = 0x4F;
            public const int P = 0x50;
            public const int Q = 0x51;
            public const int R = 0x52;
            public const int S = 0x53;
            public const int T = 0x54;
            public const int U = 0x55;
            public const int V = 0x56;
            public const int W = 0x57;
            public const int X = 0x58;
            public const int Y = 0x59;
            public const int Z = 0x5A;

            public const int LWIN = 0x5B;
            public const int RWIN = 0x5C;

            // Numpad
            public const int NUMPAD0 = 0x60;
            public const int NUMPAD1 = 0x61;
            public const int NUMPAD2 = 0x62;
            public const int NUMPAD3 = 0x63;
            public const int NUMPAD4 = 0x64;
            public const int NUMPAD5 = 0x65;
            public const int NUMPAD6 = 0x66;
            public const int NUMPAD7 = 0x67;
            public const int NUMPAD8 = 0x68;
            public const int NUMPAD9 = 0x69;
            public const int MULTIPLY = 0x6A;
            public const int ADD = 0x6B;
            public const int SEPARATOR = 0x6C;
            public const int SUBTRACT = 0x6D;
            public const int DECIMAL = 0x6E;
            public const int DIVIDE = 0x6F;

            // F-keys
            public const int F1 = 0x70;
            public const int F2 = 0x71;
            public const int F3 = 0x72;
            public const int F4 = 0x73;
            public const int F5 = 0x74;
            public const int F6 = 0x75;
            public const int F7 = 0x76;
            public const int F8 = 0x77;
            public const int F9 = 0x78;
            public const int F10 = 0x79;
            public const int F11 = 0x7A;
            public const int F12 = 0x7B;

            public const int NUMLOCK = 0x90;
            public const int SCROLL = 0x91;

            public const int LSHIFT = 0xA0;
            public const int RSHIFT = 0xA1;
            public const int LCONTROL = 0xA2;
            public const int RCONTROL = 0xA3;
            public const int LMENU = 0xA4;
            public const int RMENU = 0xA5;
        }

        #endregion

        #region ==================== Structures ====================

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public int mouseData;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        // NOTE: размер структуры на x64 = 40 байт, union начинается с offset 8
        [StructLayout(LayoutKind.Explicit)]
        public struct INPUT
        {
            [FieldOffset(0)] public int type;

            [FieldOffset(8)] public MOUSEINPUT mi;
            [FieldOffset(8)] public KEYBDINPUT ki;
            [FieldOffset(8)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        #endregion

        #region ==================== Delegates ====================

        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
        public delegate IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled);

        #endregion

        #region ==================== P/Invoke ====================

        // Hooks
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);

        // Window manipulation
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        // HotKey
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // SendInput
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        // Keyboard state
        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        public static extern short GetKeyState(int vKey);

        // Cursor
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCursorPos(int X, int Y);

        // Messages
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        // Clipboard
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetClipboardData(uint uFormat);

        #endregion

        #region ==================== Events ====================

        public class KeyEventInfo
        {
            public int VirtualKey { get; set; }
            public bool IsKeyDown { get; set; }
            public bool Handled { get; set; }
        }

        public class MouseEventInfo
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Message { get; set; }
            public bool Handled { get; set; }
        }

        public static event EventHandler<KeyEventInfo>? KeyPressed;
        public static event EventHandler<KeyEventInfo>? KeyDown;
        public static event EventHandler<KeyEventInfo>? KeyUp;
        public static event EventHandler<MouseEventInfo>? MouseAction;

        #endregion

        #region ==================== Hook Management ====================

        private static IntPtr _keyboardHook = IntPtr.Zero;
        private static IntPtr _mouseHook = IntPtr.Zero;
        private static HookProc? _keyboardProc;
        private static HookProc? _mouseProc;
        private static int _activeHotKeyId = 0;

        /// <summary>
        /// Инициализация глобального хука клавиатуры (WH_KEYBOARD_LL)
        /// </summary>
        public static bool InstallKeyboardHook()
        {
            if (_keyboardHook != IntPtr.Zero)
                return true;

            _keyboardProc = KeyboardHookCallback;
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;

            _keyboardHook = SetWindowsHookEx(
                WH_KEYBOARD_LL,
                _keyboardProc,
                GetModuleHandle(curModule?.ModuleName),
                0);

            return _keyboardHook != IntPtr.Zero;
        }

        /// <summary>
        /// Инициализация глобального хука мыши
        /// </summary>
        public static bool InstallMouseHook()
        {
            if (_mouseHook != IntPtr.Zero)
                return true;

            _mouseProc = MouseHookCallback;
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;

            _mouseHook = SetWindowsHookEx(
                WH_MOUSE_LL,
                _mouseProc,
                GetModuleHandle(curModule?.ModuleName),
                0);

            return _mouseHook != IntPtr.Zero;
        }

        public static void UninstallKeyboardHook()
        {
            if (_keyboardHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = IntPtr.Zero;
            }
        }

        public static void UninstallMouseHook()
        {
            if (_mouseHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }
        }

        private static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var kbStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                int msg = wParam.ToInt32();
                bool isKeyDown = (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN);
                bool isKeyUp = (msg == WM_KEYUP || msg == WM_SYSKEYUP);

                var info = new KeyEventInfo
                {
                    VirtualKey = kbStruct.vkCode,
                    IsKeyDown = isKeyDown
                };

                if (isKeyDown)
                {
                    KeyDown?.Invoke(null, info);
                    KeyPressed?.Invoke(null, info);
                }
                else if (isKeyUp)
                {
                    KeyUp?.Invoke(null, info);
                }

                if (info.Handled)
                    return new IntPtr(1);
            }

            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var mStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var info = new MouseEventInfo
                {
                    X = mStruct.pt.x,
                    Y = mStruct.pt.y,
                    Message = wParam.ToInt32()
                };

                MouseAction?.Invoke(null, info);

                if (info.Handled)
                    return new IntPtr(1);
            }

            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        #endregion

        #region ==================== Window Click-Through ====================

        /// <summary>
        /// Включает режим "прозрачности кликов" (клики проходят сквозь окно).
        /// Используется для оверлея в обычном режиме.
        /// </summary>
        public static void SetWindowClickThrough(IntPtr hWnd, bool clickThrough)
        {
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);

            if (clickThrough)
                exStyle |= WS_EX_TRANSPARENT;
            else
                exStyle &= ~WS_EX_TRANSPARENT;

            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle);
        }

        /// <summary>
        /// Устанавливает стиль окна как "tool window" (без иконки в таскбаре).
        /// </summary>
        public static void SetWindowToolWindow(IntPtr hWnd)
        {
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            exStyle = (exStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle);
        }

        /// <summary>
        /// Возвращает HWND из WPF Window для передачи в WinAPI.
        /// </summary>
        public static IntPtr GetWindowHandle(Window window)
        {
            var helper = new WindowInteropHelper(window);
            return helper.Handle;
        }

        #endregion

        #region ==================== SendInput Emulation ====================

        /// <summary>
        /// Отправляет нажатие одной клавиши через SendInput.
        /// </summary>
        public static void SendKeyDown(ushort vk)
        {
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].ki.wVk = vk;
            inputs[0].ki.wScan = 0;
            inputs[0].ki.dwFlags = 0;
            inputs[0].ki.time = 0;
            inputs[0].ki.dwExtraInfo = IntPtr.Zero;

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Отправляет отпускание клавиши через SendInput.
        /// </summary>
        public static void SendKeyUp(ushort vk)
        {
            var inputs = new INPUT[1];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].ki.wVk = vk;
            inputs[0].ki.wScan = 0;
            inputs[0].ki.dwFlags = KEYEVENTF_KEYUP;
            inputs[0].ki.time = 0;
            inputs[0].ki.dwExtraInfo = IntPtr.Zero;

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Полное нажатие (down + up) одной клавиши.
        /// </summary>
        public static void SendKeyPress(ushort vk, int delayMs = 0)
        {
            var inputs = new INPUT[2];

            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].ki.wVk = vk;
            inputs[0].ki.wScan = 0;
            inputs[0].ki.dwFlags = 0;
            inputs[0].ki.time = 0;
            inputs[0].ki.dwExtraInfo = IntPtr.Zero;

            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].ki.wVk = vk;
            inputs[1].ki.wScan = 0;
            inputs[1].ki.dwFlags = KEYEVENTF_KEYUP;
            inputs[1].ki.time = 0;
            inputs[1].ki.dwExtraInfo = IntPtr.Zero;

            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));

            if (delayMs > 0)
                System.Threading.Thread.Sleep(delayMs);
        }

        /// <summary>
        /// Нажатие клавиши с модификатором (например, Ctrl+V).
        /// </summary>
        public static void SendKeyWithModifier(ushort vk, ushort modifierKey, int delayMs = 0)
        {
            var inputs = new INPUT[4];
            int idx = 0;
            int inputSize = Marshal.SizeOf(typeof(INPUT));

            // modifier down
            inputs[idx].type = INPUT_KEYBOARD;
            inputs[idx].ki.wVk = modifierKey;
            inputs[idx].ki.dwFlags = 0;
            idx++;

            // key down
            inputs[idx].type = INPUT_KEYBOARD;
            inputs[idx].ki.wVk = vk;
            inputs[idx].ki.dwFlags = 0;
            idx++;

            // key up
            inputs[idx].type = INPUT_KEYBOARD;
            inputs[idx].ki.wVk = vk;
            inputs[idx].ki.dwFlags = KEYEVENTF_KEYUP;
            idx++;

            // modifier up
            inputs[idx].type = INPUT_KEYBOARD;
            inputs[idx].ki.wVk = modifierKey;
            inputs[idx].ki.dwFlags = KEYEVENTF_KEYUP;

            SendInput((uint)inputs.Length, inputs, inputSize);

            if (delayMs > 0)
                System.Threading.Thread.Sleep(delayMs);
        }

        /// <summary>
        /// Эмулирует движение мыши к абсолютной позиции.
        /// </summary>
        public static void MoveMouseTo(int x, int y, int screenWidth, int screenHeight)
        {
            int absX = (x * 65536) / screenWidth;
            int absY = (y * 65536) / screenHeight;

            var inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].mi.dx = absX;
            inputs[0].mi.dy = absY;
            inputs[0].mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE;

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Эмулирует левый клик мыши.
        /// </summary>
        public static void LeftClick()
        {
            var inputs = new INPUT[2];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;

            inputs[1].type = INPUT_MOUSE;
            inputs[1].mi.dwFlags = MOUSEEVENTF_LEFTUP;

            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        #endregion

        #region ==================== HotKey Registration ====================

        public static int RegisterGlobalHotKey(IntPtr hWnd, uint modifiers, uint vk)
        {
            int id = System.Threading.Interlocked.Increment(ref _activeHotKeyId);
            if (RegisterHotKey(hWnd, id, modifiers, vk))
                return id;
            return -1;
        }

        public static void UnregisterGlobalHotKey(IntPtr hWnd, int id)
        {
            if (id > 0)
                UnregisterHotKey(hWnd, id);
        }

        #endregion

        #region ==================== Foreground Window ====================

        /// <summary>
        /// Возвращает название активного окна (для проверки, запущена ли GTA5).
        /// </summary>
        public static string GetForegroundWindowTitle()
        {
            IntPtr hWnd = GetForegroundWindow();
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        /// <summary>
        /// Возвращает имя процесса активного окна.
        /// </summary>
        public static string GetForegroundProcessName()
        {
            IntPtr hWnd = GetForegroundWindow();
            GetWindowThreadProcessId(hWnd, out uint pid);
            try
            {
                var proc = Process.GetProcessById((int)pid);
                return proc.ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Проверяет, находится ли игра (GTA5 / PlayGTAV) в фокусе.
        /// </summary>
        public static bool IsGameInFocus(string processName = "GTA5")
        {
            string current = GetForegroundProcessName();
            return string.Equals(current, processName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(current, "PlayGTAV", StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region ==================== Utility ====================

        /// <summary>
        /// Проверяет, нажата ли сейчас указанная клавиша (async).
        /// </summary>
        public static bool IsKeyPressed(int vk)
        {
            return (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        /// <summary>
        /// Копирует текст в буфер обмена через WinAPI (на случай если WPF Clipboard недоступен).
        /// </summary>
        public static bool SetClipboardText(string text)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}