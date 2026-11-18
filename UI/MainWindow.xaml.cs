using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using NovaRpHelper.Core;
using NovaRpHelper.Modules;
using NovaRpHelper.UI.ViewModels;

namespace NovaRpHelper.UI
{
    public partial class MainWindow : Window
    {
        private MainViewModel ViewModel => (MainViewModel)DataContext;
        private IntPtr _windowHandle;
        private bool _isClickThrough = true;
        private bool _isPinned = true;

        private int _insertHotkeyId = -1;
        private HwndSource? _hwndSource;
        private DispatcherTimer? _focusTimer;
        private bool _lastGameFocus;
        private bool _manualOverride;
        private int _lastToggleTick;
        private DispatcherTimer? _geometrySaveTimer;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _windowHandle = new WindowInteropHelper(this).Handle;

            WinApiHelper.SetWindowToolWindow(_windowHandle);

            // Fallback-переключатель: RegisterHotKey + WM_HOTKEY (не зависит от LL-хука)
            _hwndSource = HwndSource.FromHwnd(_windowHandle);
            _hwndSource?.AddHook(WindowWndProc);
            _insertHotkeyId = WinApiHelper.RegisterGlobalHotKey(
                _windowHandle, WinApiHelper.MOD_NOREPEAT, (uint)WinApiHelper.VK.INSERT);

            // Основной переключатель: LL-хук
            WinApiHelper.KeyDown += OnGlobalKeyDown;

            // Авто-режим: на рабочем столе оверлей кликабельный, поверх игры — сквозь-клики
            _lastGameFocus = WinApiHelper.IsGameInFocus();
            SetClickThrough(_lastGameFocus);

            _focusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            _focusTimer.Tick += FocusTimer_Tick;
            _focusTimer.Start();

            ViewModel.RequestSendText += (s, text) => InputEmulator.Instance.EnqueueMessage(text);
            ViewModel.RequestSendChain += (s, lines) => InputEmulator.Instance.EnqueueChain(lines, ConfigService.Instance.Settings.General.ChainDelayMs);
            ViewModel.RequestCopyToClipboard += (s, text) =>
            {
                try { Clipboard.SetText(text); } catch { }
            };

            // Бейдж роли из профиля
            RoleBadge.Text = ConfigService.Instance.Settings.Profile.Summary;
            ApplySavedGeometry();
            SizeChanged += MainWindow_GeometryChanged;
            LocationChanged += MainWindow_GeometryChanged;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveGeometry();

            _focusTimer?.Stop();
            WinApiHelper.KeyDown -= OnGlobalKeyDown;

            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(WindowWndProc);
                _hwndSource = null;
            }

            if (_insertHotkeyId > 0)
            {
                WinApiHelper.UnregisterGlobalHotKey(_windowHandle, _insertHotkeyId);
                _insertHotkeyId = -1;
            }
        }

        #region ==================== Режим Click-Through ====================

        private void FocusTimer_Tick(object? sender, EventArgs e)
        {
            bool inGame = WinApiHelper.IsGameInFocus();
            if (inGame != _lastGameFocus)
            {
                _lastGameFocus = inGame;
                _manualOverride = false;
                SetClickThrough(inGame);
            }
        }

        private void OnGlobalKeyDown(object? sender, WinApiHelper.KeyEventInfo e)
        {
            if (e.VirtualKey == ConfigService.Instance.Settings.General.ModifierKey && e.IsKeyDown)
            {
                Dispatcher.Invoke(RequestToggleClickThrough);
            }
        }

        private IntPtr WindowWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WinApiHelper.WM_HOTKEY && wParam.ToInt32() == _insertHotkeyId)
            {
                RequestToggleClickThrough();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void RequestToggleClickThrough()
        {
            int now = Environment.TickCount;
            if (unchecked(now - _lastToggleTick) < 300)
                return;
            _lastToggleTick = now;

            _manualOverride = true;
            if (!IsVisible) Show();
            ToggleClickThrough();
        }

        private void ToggleClickThrough() => SetClickThrough(!_isClickThrough);

        private void SetClickThrough(bool clickThrough)
        {
            _isClickThrough = clickThrough;
            WinApiHelper.SetWindowClickThrough(_windowHandle, clickThrough);
            IsHitTestVisible = !clickThrough;

            if (!clickThrough)
                Activate();

            ViewModel.UpdateClickThroughStatus(clickThrough);
        }

        #endregion

        #region ==================== Заголовок ====================

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                bool wasClickThrough = _isClickThrough;
                if (wasClickThrough) SetClickThrough(false);
                try { DragMove(); } catch { }
                if (wasClickThrough && !_manualOverride) SetClickThrough(true);
            }
        }

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            _isPinned = !_isPinned;
            Topmost = _isPinned;
        }

        private void ToggleVisibility_Click(object sender, RoutedEventArgs e)
        {
            if (Visibility == Visibility.Visible) Hide();
            else { Show(); Activate(); }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        #endregion

        #region ==================== Биндер ====================

        private void HotKeyCapture_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            var textBox = (TextBox)sender;

            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
                return;

            string hotKey = "";
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) hotKey += "Ctrl+";
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) hotKey += "Alt+";
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) hotKey += "Shift+";

            hotKey += key.ToString().ToUpper();
            textBox.Text = hotKey;
        }

        private void AddBinder_Click(object sender, RoutedEventArgs e) => ViewModel.AddBinder();

        private void TestBinder_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).DataContext is Models.BinderEntry binder)
                ViewModel.TestBinder(binder);
        }

        private void DeleteBinder_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).DataContext is Models.BinderEntry binder)
                ViewModel.DeleteBinder(binder);
        }

        #endregion

        #region ==================== Кодексы и наказания ====================

        private void FilterCodebook_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.FilterCodebook(((Button)sender).Tag?.ToString() ?? "");
        }

        private void CopyCommand_Click(object sender, RoutedEventArgs e) => ViewModel.CopyGeneratedCommand();

        private void SendCommand_Click(object sender, RoutedEventArgs e) => ViewModel.SendGeneratedCommand();

        private void ResetCodebookSelection_Click(object sender, RoutedEventArgs e) => ViewModel.ResetCodebookSelection();

        #endregion

        #region ==================== Таймеры / Авто-пиар ====================

        private void StartTimer_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            if (int.TryParse(button.Tag?.ToString(), out int seconds))
            {
                string name = button.Content.ToString() ?? "Таймер";
                TimerModule.Instance.StartTimer(name, seconds);
                ViewModel.RefreshTimers();
            }
        }

        private void StopTimer_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).DataContext is TimerModule.TimerInfo timer)
            {
                TimerModule.Instance.StopTimer(timer.Id);
                ViewModel.RefreshTimers();
            }
        }

        private void AutoAdvert_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            if (ViewModel.AutoAdvertEnabled) AutoAdvertModule.Instance.Start();
            else AutoAdvertModule.Instance.Stop();
        }

        #endregion
        #region ==================== Геометрия окна ====================

        private void ApplySavedGeometry()
        {
            var app = ConfigService.Instance.Settings.Application;
            Width = app.OverlayWidth > 200 ? app.OverlayWidth : 420;
            Height = app.OverlayHeight > 300 ? app.OverlayHeight : 640;
            Left = app.OverlayLeft;
            Top = app.OverlayTop;
        }

        private void MainWindow_GeometryChanged(object? sender, EventArgs e)
        {
            if (_geometrySaveTimer == null)
            {
                _geometrySaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
                _geometrySaveTimer.Tick += (s, args) =>
                {
                    _geometrySaveTimer.Stop();
                    SaveGeometry();
                };
            }
            _geometrySaveTimer.Stop();
            _geometrySaveTimer.Start();
        }

        private void SaveGeometry()
        {
            _ = ConfigService.Instance.UpdateSettingsAsync(s =>
            {
                s.Application.OverlayWidth = Width;
                s.Application.OverlayHeight = Height;
                s.Application.OverlayLeft = Left;
                s.Application.OverlayTop = Top;
            });
        }

        #endregion
    }
}