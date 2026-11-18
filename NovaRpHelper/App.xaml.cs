using System;
using System.Windows;
using NovaRpHelper.Core;
using NovaRpHelper.Modules;
using NovaRpHelper.UI;

namespace NovaRpHelper
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                MessageBox.Show($"Критическая ошибка:\n{args.ExceptionObject}",
                    "NovaRpHelper", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"Ошибка UI:\n{args.Exception.Message}",
                    "NovaRpHelper", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            await ConfigService.Instance.LoadAllAsync();

            // Регистрация при входе: пока профиль не заполнен — показываем онбординг.
            if (!ConfigService.Instance.Settings.Profile.IsRegistered)
            {
                var onboarding = new OnboardingWindow();
                bool? ok = onboarding.ShowDialog();
                if (ok != true)
                {
                    Shutdown(0);
                    return;
                }
            }

            WinApiHelper.InstallKeyboardHook();
     

            BinderModule.Instance.Initialize();
            TimerModule.Instance.Initialize();
            AutoAdvertModule.Instance.Initialize();
            CodeBookModule.Instance.Initialize();

            var main = new MainWindow();
            main.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                TimerModule.Instance.Shutdown();
                AutoAdvertModule.Instance.Shutdown();
                BinderModule.Instance.Shutdown();

                WinApiHelper.UninstallKeyboardHook();
          

                InputEmulator.Instance.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при выходе: {ex.Message}");
            }

            base.OnExit(e);
        }
    }
}