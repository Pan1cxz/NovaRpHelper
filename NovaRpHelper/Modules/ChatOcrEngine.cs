using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using NovaRpHelper.Core;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace NovaRpHelper.Modules
{
    /// <summary>
    /// OCR игрового чата через Windows.Media.Ocr (ru-RU).
    /// Захват экрана -> бинаризация (LockBits) -> SoftwareBitmap -> распознавание -> фильтр дубликатов.
    /// </summary>
    public class ChatOcrEngine
    {
        private static readonly Lazy<ChatOcrEngine> _instance = new(() => new ChatOcrEngine());
        public static ChatOcrEngine Instance => _instance.Value;

        private OcrEngine? _ocrEngine;
        private CancellationTokenSource? _cts;
        private Task? _scanTask;
        private bool _isRunning;

        private readonly Queue<string> _recentLines = new();
        private const int MaxRecentLines = 30;
        private readonly object _lockObj = new();

        public event EventHandler<string>? OnNewChatLine;

        private ChatOcrEngine()
        {
            InitializeOcrEngine();
        }

        private void InitializeOcrEngine()
        {
            try
            {
                var language = new Language("ru-RU");
                if (OcrEngine.IsLanguageSupported(language))
                {
                    _ocrEngine = OcrEngine.TryCreateFromLanguage(language);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[ChatOcrEngine] ru-RU не установлен, fallback на язык профиля.");
                    _ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ChatOcrEngine] Ошибка инициализации OCR: {ex.Message}");
            }
        }

        public void Start()
        {
            if (_isRunning || _ocrEngine == null) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();
            _scanTask = Task.Run(async () => await ScanLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _cts?.Cancel();
            try { _scanTask?.Wait(1000); }
            catch { }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task ScanLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var settings = ConfigService.Instance.Settings.OcrZone;
                    var engine = _ocrEngine;
                    if (engine == null || !settings.Enabled || settings.Width <= 0 || settings.Height <= 0)
                    {
                        await Task.Delay(1000, token);
                        continue;
                    }

                    using var bitmap = CaptureScreenRegion(settings.X, settings.Y, settings.Width, settings.Height);
                    if (bitmap == null)
                    {
                        await Task.Delay(settings.ScanIntervalMs, token);
                        continue;
                    }

                    using var processedBitmap = BinarizeBitmap(bitmap);
                    using var softwareBitmap = await ConvertToSoftwareBitmapAsync(processedBitmap);
                    if (softwareBitmap == null)
                    {
                        await Task.Delay(settings.ScanIntervalMs, token);
                        continue;
                    }

                    var ocrResult = await engine.RecognizeAsync(softwareBitmap);
                    string recognizedText = ocrResult.Text.Trim();

                    if (!string.IsNullOrWhiteSpace(recognizedText))
                        ProcessRecognizedText(recognizedText);

                    await Task.Delay(settings.ScanIntervalMs, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ChatOcrEngine] Ошибка сканирования: {ex.Message}");
                    try { await Task.Delay(1000, token); } catch { break; }
                }
            }
        }

        private Bitmap? CaptureScreenRegion(int x, int y, int width, int height)
        {
            try
            {
                var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Бинаризация: светлый текст игры -> черный на белом фоне (оптимально для OCR).
        /// Быстрый путь через LockBits + Marshal.Copy, без GetPixel.
        /// </summary>
        private Bitmap BinarizeBitmap(Bitmap source)
        {
            var result = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);

            var rect = new Rectangle(0, 0, source.Width, source.Height);
            var sourceData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var resultData = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            try
            {
                int srcBytes = Math.Abs(sourceData.Stride) * source.Height;
                int dstBytes = Math.Abs(resultData.Stride) * result.Height;
                var sourceBytes = new byte[srcBytes];
                var resultBytes = new byte[dstBytes];

                Marshal.Copy(sourceData.Scan0, sourceBytes, 0, srcBytes);

                const int threshold = 140;

                for (int y = 0; y < source.Height; y++)
                {
                    int srcRow = y * sourceData.Stride;
                    int dstRow = y * resultData.Stride;

                    for (int x = 0; x < source.Width; x++)
                    {
                        int si = srcRow + x * 4;
                        int di = dstRow + x * 3;

                        byte b = sourceBytes[si];
                        byte g = sourceBytes[si + 1];
                        byte r = sourceBytes[si + 2];

                        int gray = (int)(0.299 * r + 0.587 * g + 0.114 * b);
                        byte value = (byte)(gray > threshold ? 0 : 255);

                        resultBytes[di] = value;
                        resultBytes[di + 1] = value;
                        resultBytes[di + 2] = value;
                    }
                }

                Marshal.Copy(resultBytes, 0, resultData.Scan0, dstBytes);
            }
            finally
            {
                source.UnlockBits(sourceData);
                result.UnlockBits(resultData);
            }

            return result;
        }

        private async Task<SoftwareBitmap?> ConvertToSoftwareBitmapAsync(Bitmap bitmap)
        {
            try
            {
                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Bmp);
                ms.Seek(0, SeekOrigin.Begin);

                using var randomAccessStream = new InMemoryRandomAccessStream();
                await randomAccessStream.WriteAsync(ms.GetWindowsRuntimeBuffer());
                randomAccessStream.Seek(0);

                var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
                return await decoder.GetSoftwareBitmapAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ChatOcrEngine] Ошибка конвертации Bitmap: {ex.Message}");
                return null;
            }
        }

        private void ProcessRecognizedText(string text)
        {
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(l => l.Trim())
                            .Where(l => l.Length > 2);

            lock (_lockObj)
            {
                foreach (var line in lines)
                {
                    if (_recentLines.Contains(line))
                        continue;

                    _recentLines.Enqueue(line);
                    if (_recentLines.Count > MaxRecentLines)
                        _recentLines.Dequeue();

                    OnNewChatLine?.Invoke(this, line);
                }
            }
        }

        public void ClearCache()
        {
            lock (_lockObj)
            {
                _recentLines.Clear();
            }
        }
    }
}