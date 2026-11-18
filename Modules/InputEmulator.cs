using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NovaRpHelper.Core;

namespace NovaRpHelper.Modules
{
    /// <summary>
    /// Эмуляция ввода строго через SendInput. Единая последовательная очередь
    /// (одиночные сообщения и RP-цепочки не пересекаются), антифлуд-задержки 40–90 мс.
    /// Буфер обмена трогается только через UI-поток (STA), иначе WPF Clipboard бросает исключение.
    /// </summary>
    public class InputEmulator
    {
        private static readonly Lazy<InputEmulator> _instance = new(() => new InputEmulator());
        public static InputEmulator Instance => _instance.Value;

        private readonly struct QueueItem
        {
            public QueueItem(string text, int delayAfterMs)
            {
                Text = text;
                DelayAfterMs = delayAfterMs;
            }
            public string Text { get; }
            public int DelayAfterMs { get; }
        }

        private readonly ConcurrentQueue<QueueItem> _queue = new();
        private readonly Random _random = new();
        private int _processingFlag;
        private CancellationTokenSource? _cts;

        private InputEmulator() { }

        public void EnqueueMessage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _queue.Enqueue(new QueueItem(text, 250 + _random.Next(50, 150)));
            EnsureProcessing();
        }

        public void EnqueueChain(IEnumerable<string> lines, int delayBetweenLinesMs)
        {
            if (lines == null) return;
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                _queue.Enqueue(new QueueItem(line, delayBetweenLinesMs + _random.Next(100, 300)));
            }
            EnsureProcessing();
        }

        public void ClearQueue()
        {
            while (_queue.TryDequeue(out _)) { }
        }

        private void EnsureProcessing()
        {
            if (Interlocked.CompareExchange(ref _processingFlag, 1, 0) != 0)
                return;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            Task.Run(async () => await ProcessQueueAsync(token));
        }

        private async Task ProcessQueueAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (_queue.TryDequeue(out var item))
                    {
                        await SendStringInternalAsync(item.Text);
                        await Task.Delay(item.DelayAfterMs, token);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // ожидаемо при остановке
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InputEmulator] Ошибка очереди: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _processingFlag, 0);
                if (!_queue.IsEmpty)
                    EnsureProcessing();
            }
        }

        /// <summary>
        /// T -> задержка -> Clipboard -> задержка -> Ctrl+V -> задержка -> Enter.
        /// </summary>
        private async Task SendStringInternalAsync(string text)
        {
            var settings = ConfigService.Instance.Settings.General;

            if (settings.IgnoreWhenNotInFocus && !WinApiHelper.IsGameInFocus())
            {
                System.Diagnostics.Debug.WriteLine("[InputEmulator] Игра не в фокусе, отправка отменена.");
                return;
            }

            string? oldClipboard = TryGetClipboardText();

            WinApiHelper.SendKeyPress((ushort)settings.ChatKey);
            await Task.Delay(GetRandomDelay(settings.MinDelayMs, settings.MaxDelayMs));

            if (!TrySetClipboardText(text))
            {
                System.Diagnostics.Debug.WriteLine("[InputEmulator] Не удалось записать в буфер обмена.");
                return;
            }
            await Task.Delay(GetRandomDelay(settings.MinDelayMs, settings.MaxDelayMs));

            WinApiHelper.SendKeyWithModifier((ushort)WinApiHelper.VK.V, (ushort)WinApiHelper.VK.LCONTROL);
            await Task.Delay(GetRandomDelay(settings.MinDelayMs, settings.MaxDelayMs));

            WinApiHelper.SendKeyPress((ushort)settings.EnterKey);
            await Task.Delay(100 + _random.Next(20, 50));

            // Best-effort восстановление буфера пользователя
            if (oldClipboard != null)
                TrySetClipboardText(oldClipboard);
        }

        private static string? TryGetClipboardText()
        {
            try
            {
                var app = System.Windows.Application.Current;
                if (app != null)
                    return app.Dispatcher.Invoke(() => System.Windows.Clipboard.GetText());
                return System.Windows.Clipboard.GetText();
            }
            catch
            {
                return null;
            }
        }

        private static bool TrySetClipboardText(string text)
        {
            try
            {
                var app = System.Windows.Application.Current;
                if (app != null)
                {
                    app.Dispatcher.Invoke(() => System.Windows.Clipboard.SetText(text));
                    return true;
                }
                return WinApiHelper.SetClipboardText(text);
            }
            catch
            {
                return false;
            }
        }

        private int GetRandomDelay(int min, int max)
        {
            if (min >= max) return min;
            return _random.Next(min, max + 1);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            ClearQueue();
        }
    }
}