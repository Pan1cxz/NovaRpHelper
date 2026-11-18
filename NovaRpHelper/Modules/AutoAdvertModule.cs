using System;
using System.Threading;
using System.Threading.Tasks;
using NovaRpHelper.Core;

namespace NovaRpHelper.Modules
{
    /// <summary>
    /// Модуль автоматической отправки рекламного текста в чат по таймеру.
    /// Реализует рандомизацию окончания строки для обхода анти-спама.
    /// </summary>
    public class AutoAdvertModule
    {
        private static readonly Lazy<AutoAdvertModule> _instance = new(() => new AutoAdvertModule());
        public static AutoAdvertModule Instance => _instance.Value;

        private CancellationTokenSource? _cts;
        private Task? _task;
        private bool _isRunning;
        private int _intervalSeconds = 180;
        private readonly Random _random = new();

        // Набор смайлов/символов для рандомизации
        private static readonly string[] _randomEndings = new[]
        {
            " ✨", " ⭐", " 🔥", " 💎", " 🎯", " 🚀", " 💫", " 🌟",
            " •", " ·", " ★", " ♪", " ❤", " ✓", " ✔", " ☑",
            " !", " !!", " ...", " ~", " ^_^", " :)", " ;)"
        };

        private AutoAdvertModule() { }

        public void Initialize()
        {
            var settings = ConfigService.Instance.Settings.AutoAdvert;
            _intervalSeconds = settings.IntervalSeconds;

            if (settings.Enabled)
                Start();
        }

        public void Shutdown()
        {
            Stop();
        }

        public void Start()
        {
            if (_isRunning) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();
            _task = Task.Run(async () => await AdvertLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _cts?.Cancel();
            try { _task?.Wait(1000); } catch { }
            _cts?.Dispose();
            _cts = null;
        }

        public void UpdateInterval(int seconds)
        {
            _intervalSeconds = Math.Max(30, seconds); // Минимум 30 сек для безопасности
        }

        private async Task AdvertLoopAsync(CancellationToken token)
        {
            // Начальная задержка перед первой отправкой (случайная 5-15 сек)
            await Task.Delay(_random.Next(5000, 15000), token);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var settings = ConfigService.Instance.Settings.AutoAdvert;
                    if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Text))
                    {
                        await Task.Delay(5000, token);
                        continue;
                    }

                    // Проверяем, что игра в фокусе
                    if (settings.Text.StartsWith("/ad ") || settings.Text.StartsWith("/me ") || settings.Text.StartsWith("/do "))
                    {
                        if (!WinApiHelper.IsGameInFocus())
                        {
                            await Task.Delay(5000, token);
                            continue;
                        }
                    }

                    string textToSend = settings.Text;

                    // Рандомизация окончания для обхода анти-спама
                    if (settings.RandomizeEnding)
                    {
                        string ending = _randomEndings[_random.Next(_randomEndings.Length)];
                        textToSend = textToSend.TrimEnd() + ending;
                    }

                    // Добавляем небольшую случайную задержку перед отправкой
                    await Task.Delay(_random.Next(500, 2000), token);

                    InputEmulator.Instance.EnqueueMessage(textToSend);

                    // Ждем интервал + рандомный джиттер (±10%)
                    int jitter = (int)(_intervalSeconds * 0.1);
                    int delay = (_intervalSeconds * 1000) + _random.Next(-jitter, jitter);
                    await Task.Delay(Math.Max(5000, delay), token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AutoAdvertModule] Ошибка: {ex.Message}");
                    await Task.Delay(5000, token);
                }
            }
        }
    }
}