using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace NovaRpHelper.Modules
{
    /// <summary>
    /// Модуль таймеров обратного отсчета.
    /// Поддерживает запуск/остановку нескольких таймеров одновременно,
    /// звуковое оповещение при истечении и обновление UI через INotifyPropertyChanged.
    /// </summary>
    public class TimerModule
    {
        private static readonly Lazy<TimerModule> _instance = new(() => new TimerModule());
        public static TimerModule Instance => _instance.Value;

        private readonly ConcurrentDictionary<Guid, TimerInfo> _timers = new();
        private DispatcherTimer? _uiUpdateTimer;
        private bool _isInitialized;

        public event EventHandler? TimersChanged;

        private TimerModule() { }

        public void Initialize()
        {
            if (_isInitialized) return;

            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _uiUpdateTimer.Tick += (s, e) => UpdateAllTimers();
            _uiUpdateTimer.Start();

            _isInitialized = true;
        }

        public void Shutdown()
        {
            _uiUpdateTimer?.Stop();
            _timers.Clear();
            _isInitialized = false;
        }

        public Guid StartTimer(string name, int seconds)
        {
            var timer = new TimerInfo
            {
                Id = Guid.NewGuid(),
                Name = name,
                TotalSeconds = seconds,
                RemainingSeconds = seconds,
                StartedAt = DateTime.Now
            };

            _timers[timer.Id] = timer;
            TimersChanged?.Invoke(this, EventArgs.Empty);
            return timer.Id;
        }

        public void StopTimer(Guid id)
        {
            _timers.TryRemove(id, out _);
            TimersChanged?.Invoke(this, EventArgs.Empty);
        }

        public IReadOnlyList<TimerInfo> GetActiveTimers()
        {
            return _timers.Values.ToList();
        }

        private void UpdateAllTimers()
        {
            bool anyChanged = false;
            var expired = new List<Guid>();

            foreach (var timer in _timers.Values)
            {
                var elapsed = (DateTime.Now - timer.StartedAt).TotalSeconds;
                int remaining = (int)(timer.TotalSeconds - elapsed);

                if (remaining <= 0)
                {
                    expired.Add(timer.Id);
                    timer.RemainingSeconds = 0;
                    timer.RemainingText = "00:00 ⏰";
                    timer.IsExpired = true;

                    // Звуковое оповещение
                    try { System.Media.SystemSounds.Exclamation.Play(); } catch { }
                }
                else
                {
                    timer.RemainingSeconds = remaining;
                    timer.RemainingText = FormatTime(remaining);
                    timer.IsExpired = false;
                }

                anyChanged = true;
            }

            foreach (var id in expired)
            {
                _timers.TryRemove(id, out _);
            }

            if (anyChanged || expired.Count > 0)
            {
                TimersChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private string FormatTime(int seconds)
        {
            int m = seconds / 60;
            int s = seconds % 60;
            return $"{m:D2}:{s:D2}";
        }

        /// <summary>
        /// Информация о таймере с поддержкой INotifyPropertyChanged для UI.
        /// </summary>
        public class TimerInfo : INotifyPropertyChanged
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int TotalSeconds { get; set; }
            public DateTime StartedAt { get; set; }

            private int _remainingSeconds;
            public int RemainingSeconds
            {
                get => _remainingSeconds;
                set { _remainingSeconds = value; OnPropertyChanged(); }
            }

            private string _remainingText = "00:00";
            public string RemainingText
            {
                get => _remainingText;
                set { _remainingText = value; OnPropertyChanged(); }
            }

            private bool _isExpired;
            public bool IsExpired
            {
                get => _isExpired;
                set { _isExpired = value; OnPropertyChanged(); }
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}