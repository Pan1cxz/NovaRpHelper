using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NovaRpHelper.Core;
using NovaRpHelper.Models;

namespace NovaRpHelper.Modules
{
    /// <summary>
    /// Модуль биндов: глобальный перехват клавиш, матчинг хоткеев с модификаторами,
    /// подстановка {ID}, передача в InputEmulator. Единый whole-класс без дубликатов.
    /// </summary>
    public class BinderModule
    {
        private static readonly Lazy<BinderModule> _instance = new(() => new BinderModule());
        public static BinderModule Instance => _instance.Value;

        private List<BinderEntry> _activeBindings = new();
        private readonly HashSet<int> _pressedKeys = new();
        private readonly object _sync = new();
        private bool _isInitialized;

        // OEM-клавиши (WPF Key.ToString() -> "OemPlus" и т.п.)
        private const int VK_OEM_PLUS = 0xBB;
        private const int VK_OEM_COMMA = 0xBC;
        private const int VK_OEM_MINUS = 0xBD;
        private const int VK_OEM_PERIOD = 0xBE;
        private const int VK_OEM_1 = 0xBA;
        private const int VK_OEM_2 = 0xBF;
        private const int VK_OEM_3 = 0xC0;
        private const int VK_OEM_4 = 0xDB;
        private const int VK_OEM_5 = 0xDC;
        private const int VK_OEM_6 = 0xDD;
        private const int VK_OEM_7 = 0xDE;

        private BinderModule()
        {
            ConfigService.Instance.BindersChanged += OnBindersChanged;
        }

        public void Initialize()
        {
            if (_isInitialized) return;

            WinApiHelper.KeyDown += OnGlobalKeyDown;
            WinApiHelper.KeyUp += OnGlobalKeyUp;
            RebuildBindings();
            _isInitialized = true;
        }

        public void Shutdown()
        {
            WinApiHelper.KeyDown -= OnGlobalKeyDown;
            WinApiHelper.KeyUp -= OnGlobalKeyUp;
            lock (_sync)
            {
                _activeBindings.Clear();
                _pressedKeys.Clear();
            }
            _isInitialized = false;
        }

        private void OnBindersChanged(object? sender, EventArgs e)
        {
            RebuildBindings();
        }

        private void RebuildBindings()
        {
            var list = ConfigService.Instance.Binders
                .Where(b => b.Enabled && !string.IsNullOrWhiteSpace(b.HotKey))
                .ToList();

            lock (_sync)
            {
                _activeBindings = list;
            }
        }

        private void OnGlobalKeyDown(object? sender, WinApiHelper.KeyEventInfo e)
        {
            // Игнорируем авто-повтор при удержании клавиши
            lock (_sync)
            {
                if (!_pressedKeys.Add(e.VirtualKey))
                    return;
            }

            BinderEntry? matched;
            lock (_sync)
            {
                matched = _activeBindings.FirstOrDefault(b => IsHotKeyMatch(b.HotKey, e.VirtualKey));
            }

            if (matched != null)
            {
                e.Handled = true;
                var binder = matched;
                _ = Task.Run(() => ProcessBinder(binder));
            }
        }

        private void OnGlobalKeyUp(object? sender, WinApiHelper.KeyEventInfo e)
        {
            lock (_sync)
            {
                _pressedKeys.Remove(e.VirtualKey);
            }
        }

        private void ProcessBinder(BinderEntry binder)
        {
            try
            {
                string targetId = AppState.Instance.TargetId.Trim();

                string Substitute(string line)
                {
                    if (line.Contains("{ID}", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(targetId))
                            return line; // оставит {ID} -> ниже abort
                        return line.Replace("{ID}", targetId, StringComparison.OrdinalIgnoreCase);
                    }
                    return line;
                }

                if (binder.IsChain && binder.ChainLines != null && binder.ChainLines.Count > 0)
                {
                    var lines = binder.ChainLines.Select(Substitute).ToList();
                    if (lines.Any(l => l.Contains("{ID}", StringComparison.OrdinalIgnoreCase)))
                    {
                        System.Diagnostics.Debug.WriteLine("[BinderModule] Target ID пуст, цепочка отменена.");
                        return;
                    }
                    InputEmulator.Instance.EnqueueChain(lines, ConfigService.Instance.Settings.General.ChainDelayMs);
                }
                else
                {
                    string text = Substitute(binder.Text);
                    if (text.Contains("{ID}", StringComparison.OrdinalIgnoreCase))
                    {
                        System.Diagnostics.Debug.WriteLine("[BinderModule] Target ID пуст, бинд отменен.");
                        return;
                    }
                    if (!string.IsNullOrWhiteSpace(text))
                        InputEmulator.Instance.EnqueueMessage(text);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BinderModule] Ошибка обработки бинда: {ex.Message}");
            }
        }

        /// <summary>
        /// Строгий матчинг: основная клавиша + точное совпадение состояния модификаторов
        /// (чтобы бинд "T" не срабатывал при Ctrl+T).
        /// </summary>
        private bool IsHotKeyMatch(string hotKeyString, int pressedVk)
        {
            if (string.IsNullOrWhiteSpace(hotKeyString)) return false;

            var parts = hotKeyString.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0) return false;

            int expectedVk = GetVkCodeFromName(parts.Last());
            if (expectedVk == 0 || expectedVk != pressedVk) return false;

            bool needCtrl = parts.Any(p => p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase));
            bool needAlt = parts.Any(p => p.Equals("Alt", StringComparison.OrdinalIgnoreCase));
            bool needShift = parts.Any(p => p.Equals("Shift", StringComparison.OrdinalIgnoreCase));

            bool ctrlDown = WinApiHelper.IsKeyPressed(WinApiHelper.VK.LCONTROL) || WinApiHelper.IsKeyPressed(WinApiHelper.VK.RCONTROL);
            bool altDown = WinApiHelper.IsKeyPressed(WinApiHelper.VK.LMENU) || WinApiHelper.IsKeyPressed(WinApiHelper.VK.RMENU);
            bool shiftDown = WinApiHelper.IsKeyPressed(WinApiHelper.VK.LSHIFT) || WinApiHelper.IsKeyPressed(WinApiHelper.VK.RSHIFT);

            return needCtrl == ctrlDown && needAlt == altDown && needShift == shiftDown;
        }

        private int GetVkCodeFromName(string keyName)
        {
            return keyName.ToUpperInvariant() switch
            {
                "F1" => WinApiHelper.VK.F1,
                "F2" => WinApiHelper.VK.F2,
                "F3" => WinApiHelper.VK.F3,
                "F4" => WinApiHelper.VK.F4,
                "F5" => WinApiHelper.VK.F5,
                "F6" => WinApiHelper.VK.F6,
                "F7" => WinApiHelper.VK.F7,
                "F8" => WinApiHelper.VK.F8,
                "F9" => WinApiHelper.VK.F9,
                "F10" => WinApiHelper.VK.F10,
                "F11" => WinApiHelper.VK.F11,
                "F12" => WinApiHelper.VK.F12,
                "0" or "D0" => WinApiHelper.VK.D0,
                "1" or "D1" => WinApiHelper.VK.D1,
                "2" or "D2" => WinApiHelper.VK.D2,
                "3" or "D3" => WinApiHelper.VK.D3,
                "4" or "D4" => WinApiHelper.VK.D4,
                "5" or "D5" => WinApiHelper.VK.D5,
                "6" or "D6" => WinApiHelper.VK.D6,
                "7" or "D7" => WinApiHelper.VK.D7,
                "8" or "D8" => WinApiHelper.VK.D8,
                "9" or "D9" => WinApiHelper.VK.D9,
                "A" => WinApiHelper.VK.A,
                "B" => WinApiHelper.VK.B,
                "C" => WinApiHelper.VK.C,
                "D" => WinApiHelper.VK.D,
                "E" => WinApiHelper.VK.E,
                "F" => WinApiHelper.VK.F,
                "G" => WinApiHelper.VK.G,
                "H" => WinApiHelper.VK.H,
                "I" => WinApiHelper.VK.I,
                "J" => WinApiHelper.VK.J,
                "K" => WinApiHelper.VK.K,
                "L" => WinApiHelper.VK.L,
                "M" => WinApiHelper.VK.M,
                "N" => WinApiHelper.VK.N,
                "O" => WinApiHelper.VK.O,
                "P" => WinApiHelper.VK.P,
                "Q" => WinApiHelper.VK.Q,
                "R" => WinApiHelper.VK.R,
                "S" => WinApiHelper.VK.S,
                "T" => WinApiHelper.VK.T,
                "U" => WinApiHelper.VK.U,
                "V" => WinApiHelper.VK.V,
                "W" => WinApiHelper.VK.W,
                "X" => WinApiHelper.VK.X,
                "Y" => WinApiHelper.VK.Y,
                "Z" => WinApiHelper.VK.Z,
                "NUMPAD0" => WinApiHelper.VK.NUMPAD0,
                "NUMPAD1" => WinApiHelper.VK.NUMPAD1,
                "NUMPAD2" => WinApiHelper.VK.NUMPAD2,
                "NUMPAD3" => WinApiHelper.VK.NUMPAD3,
                "NUMPAD4" => WinApiHelper.VK.NUMPAD4,
                "NUMPAD5" => WinApiHelper.VK.NUMPAD5,
                "NUMPAD6" => WinApiHelper.VK.NUMPAD6,
                "NUMPAD7" => WinApiHelper.VK.NUMPAD7,
                "NUMPAD8" => WinApiHelper.VK.NUMPAD8,
                "NUMPAD9" => WinApiHelper.VK.NUMPAD9,
                "INSERT" => WinApiHelper.VK.INSERT,
                "DELETE" => WinApiHelper.VK.DELETE,
                "HOME" => WinApiHelper.VK.HOME,
                "END" => WinApiHelper.VK.END,
                "PAGEUP" => WinApiHelper.VK.PRIOR,
                "PAGEDOWN" => WinApiHelper.VK.NEXT,
                "SPACE" => WinApiHelper.VK.SPACE,
                "ENTER" or "RETURN" => WinApiHelper.VK.ENTER,
                "TAB" => WinApiHelper.VK.TAB,
                "ESC" or "ESCAPE" => WinApiHelper.VK.ESCAPE,
                "BACK" or "BACKSPACE" => WinApiHelper.VK.BACK,
                "OEMPLUS" => VK_OEM_PLUS,
                "OEMCOMMA" => VK_OEM_COMMA,
                "OEMMINUS" => VK_OEM_MINUS,
                "OEMPERIOD" => VK_OEM_PERIOD,
                "OEM1" => VK_OEM_1,
                "OEMQUESTION" => VK_OEM_2,
                "OEMTILDE" => VK_OEM_3,
                "OEMOPENBRACKETS" => VK_OEM_4,
                "OEMPIPE" => VK_OEM_5,
                "OEMCLOSEBRACKETS" => VK_OEM_6,
                "OEMQUOTES" => VK_OEM_7,
                _ => 0
            };
        }
    }
}