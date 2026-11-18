using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using NovaRpHelper.Core;
using NovaRpHelper.Models;
using NovaRpHelper.Modules;

namespace NovaRpHelper.UI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event EventHandler<string>? RequestSendText;
        public event EventHandler<string>? RequestCopyToClipboard;
        public event EventHandler<IEnumerable<string>>? RequestSendChain;

        #region ==================== Общее состояние ====================

        private string _targetId = string.Empty;
        public string TargetId
        {
            get => _targetId;
            set
            {
                if (_targetId != value)
                {
                    _targetId = value;
                    AppState.Instance.TargetId = value;
                    OnPropertyChanged();
                    RecalculateCodebookTotals();
                }
            }
        }

        private SolidColorBrush _statusColor = new SolidColorBrush(Color.FromRgb(76, 175, 80));
        public SolidColorBrush StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
        }

        private string _statusText = "Готов к работе";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        private string _clickThroughStatus = "🔒 Click-Through: ON";
        public string ClickThroughStatus
        {
            get => _clickThroughStatus;
            set { _clickThroughStatus = value; OnPropertyChanged(); }
        }

        public void UpdateClickThroughStatus(bool isClickThrough)
        {
            ClickThroughStatus = isClickThrough ? "🔒 Click-Through: ON" : "🔓 Click-Through: OFF";
            StatusColor = isClickThrough
                ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
                : new SolidColorBrush(Color.FromRgb(255, 152, 0));
        }

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set { _selectedTabIndex = value; OnPropertyChanged(); }
        }

        #endregion

        #region ==================== Биндер ====================

        private string _newBinderName = string.Empty;
        public string NewBinderName
        {
            get => _newBinderName;
            set { _newBinderName = value; OnPropertyChanged(); }
        }

        private string _newBinderHotKey = string.Empty;
        public string NewBinderHotKey
        {
            get => _newBinderHotKey;
            set { _newBinderHotKey = value; OnPropertyChanged(); }
        }

        private string _newBinderText = string.Empty;
        public string NewBinderText
        {
            get => _newBinderText;
            set { _newBinderText = value; OnPropertyChanged(); }
        }

        private bool _isChainMode;
        public bool IsChainMode
        {
            get => _isChainMode;
            set { _isChainMode = value; OnPropertyChanged(); }
        }

        private bool _useTargetId;
        public bool UseTargetId
        {
            get => _useTargetId;
            set { _useTargetId = value; OnPropertyChanged(); }
        }

        public ObservableCollection<BinderEntry> Binders { get; } = new();

        public BinderEntry? SelectedBinder { get; set; }

        public void RefreshBinders()
        {
            Binders.Clear();
            foreach (var b in ConfigService.Instance.Binders)
                Binders.Add(b);
        }

        public void AddBinder()
        {
            if (string.IsNullOrWhiteSpace(NewBinderName) || string.IsNullOrWhiteSpace(NewBinderHotKey))
                return;

            var entry = new BinderEntry
            {
                Name = NewBinderName,
                HotKey = NewBinderHotKey,
                Text = NewBinderText,
                IsChain = IsChainMode,
                UseTargetId = UseTargetId,
                ChainLines = IsChainMode
                    ? NewBinderText.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList()
                    : new List<string>(),
                Enabled = true
            };

            _ = ConfigService.Instance.AddBinderAsync(entry);

            NewBinderName = string.Empty;
            NewBinderHotKey = string.Empty;
            NewBinderText = string.Empty;
            IsChainMode = false;
            UseTargetId = false;
        }

        public void TestBinder(BinderEntry binder)
        {
            string text = binder.Text;
            if (!string.IsNullOrWhiteSpace(TargetId))
                text = text.Replace("{ID}", TargetId, StringComparison.OrdinalIgnoreCase);

            if (binder.IsChain && binder.ChainLines?.Count > 0)
            {
                var lines = binder.ChainLines
                    .Select(l => string.IsNullOrWhiteSpace(TargetId)
                        ? l
                        : l.Replace("{ID}", TargetId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                InputEmulator.Instance.EnqueueChain(lines, ConfigService.Instance.Settings.General.ChainDelayMs);
            }
            else if (!string.IsNullOrWhiteSpace(text))
            {
                RequestSendText?.Invoke(this, text);
            }
        }

        public void DeleteBinder(BinderEntry binder)
        {
            _ = ConfigService.Instance.RemoveBinderAsync(binder.Id);
        }

        #endregion

        #region ==================== Кодексы и наказания ====================

        private string _codebookSearch = string.Empty;
        public string CodebookSearch
        {
            get => _codebookSearch;
            set
            {
                _codebookSearch = value;
                OnPropertyChanged();
                ApplyCodebookFilter();
            }
        }

        private string _codebookCategoryFilter = string.Empty;

        public ObservableCollection<CodeBookItemViewModel> FilteredCodebook { get; } = new();

        private int _totalJailMinutes;
        public int TotalJailMinutes
        {
            get => _totalJailMinutes;
            set { _totalJailMinutes = value; OnPropertyChanged(); }
        }

        private int _totalFineAmount;
        public int TotalFineAmount
        {
            get => _totalFineAmount;
            set { _totalFineAmount = value; OnPropertyChanged(); }
        }

        private string _generatedCommand = string.Empty;
        public string GeneratedCommand
        {
            get => _generatedCommand;
            set { _generatedCommand = value; OnPropertyChanged(); }
        }

        private bool _isIcMode = true;
        public bool IsIcMode
        {
            get => _isIcMode;
            set
            {
                if (_isIcMode != value)
                {
                    _isIcMode = value;
                    OnPropertyChanged();
                    RecalculateCodebookTotals();
                }
            }
        }

        private string _overrideMinutes = string.Empty;
        public string OverrideMinutes
        {
            get => _overrideMinutes;
            set
            {
                _overrideMinutes = value;
                OnPropertyChanged();
                RecalculateCodebookTotals();
            }
        }

        private string _overrideFine = string.Empty;
        public string OverrideFine
        {
            get => _overrideFine;
            set
            {
                _overrideFine = value;
                OnPropertyChanged();
                RecalculateCodebookTotals();
            }
        }

        public ObservableCollection<PunishmentRecord> PunishmentLog => PunishmentLogModule.Instance.Records;

        public ICommand ClearPunishmentLogCommand => PunishmentLogModule.Instance.ClearCommand;

        public ICommand RecordPunishmentCommand { get; }

        public void RefreshCodebook()
        {
            FilteredCodebook.Clear();
            foreach (var item in ConfigService.Instance.Codebook)
            {
                var vm = new CodeBookItemViewModel(item);
                vm.PropertyChanged += CodebookItem_PropertyChanged;
                FilteredCodebook.Add(vm);
            }
            ApplyCodebookFilter();
        }

        private void CodebookItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CodeBookItemViewModel.IsSelected))
                RecalculateCodebookTotals();
        }

        public void FilterCodebook(string category)
        {
            _codebookCategoryFilter = category;
            ApplyCodebookFilter();
        }

        private static string NormalizeSearch(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            string s = raw.Trim().ToLowerInvariant();
            s = s.Replace(",", ".");
            s = s.Replace("§", "");
            s = System.Text.RegularExpressions.Regex.Replace(s, @"^пп?\.\s*", "");
            s = s.Replace(" ", "");
            return s;
        }

        private void ApplyCodebookFilter()
        {
            string search = NormalizeSearch(CodebookSearch);
            bool hasSearch = search.Length > 0;
            string category = _codebookCategoryFilter;

            foreach (var item in FilteredCodebook)
            {
                bool visible = true;

                if (hasSearch)
                {
                    // Поиск игнорирует категорию и ищет по всему справочнику
                    string article = NormalizeSearch(item.Article);
                    string title = (item.Title ?? "").ToLowerInvariant();
                    string desc = (item.Description ?? "").ToLowerInvariant();
                    string cat = (item.Category ?? "").ToLowerInvariant();

                    visible = article.Contains(search) || title.Contains(search) ||
                              desc.Contains(search) || cat.Contains(search);
                }
                else if (!string.IsNullOrEmpty(category))
                {
                    visible = item.Category.Equals(category, StringComparison.OrdinalIgnoreCase);
                }

                item.IsVisible = visible;
            }
        }

        /// <summary>
        /// Протокол наказания: суммы статей + ручные переопределения + режим IC/OOC.
        /// IC  -> /jail, /fine, /warn с подстановкой Target ID (не для категории "Уставы").
        /// OOC -> текстовая формулировка наказания для администрации/внутренних взысканий.
        /// </summary>
        private void RecalculateCodebookTotals()
        {
            var selected = FilteredCodebook.Where(i => i.IsSelected).ToList();

            int sumMinutes = selected.Sum(i => i.JailMinutes);
            long sumFine = selected.Sum(i => (long)i.FineAmount);

            int minutes = int.TryParse(OverrideMinutes?.Trim(), out int om) && om >= 0 ? om : sumMinutes;
            long fine = long.TryParse(OverrideFine?.Trim().Replace(" ", ""), out long of) && of >= 0 ? of : sumFine;

            TotalJailMinutes = minutes;
            TotalFineAmount = (int)Math.Min(fine, int.MaxValue);

            if (selected.Count == 0)
            {
                GeneratedCommand = string.Empty;
                return;
            }

            string targetId = string.IsNullOrWhiteSpace(TargetId) ? "{ID}" : TargetId;
            string articles = string.Join(", ", selected.Select(i => $"{i.Category} {i.Article}"));

            bool hasInternal = selected.Any(i => i.Category.Equals("Уставы", StringComparison.OrdinalIgnoreCase));

            if (IsIcMode && !hasInternal)
            {
                if (minutes > 0)
                    GeneratedCommand = $"/jail {targetId} {minutes} {fine} {articles}";
                else if (fine > 0)
                    GeneratedCommand = $"/fine {targetId} {fine} {articles}";
                else
                    GeneratedCommand = $"/warn {targetId} {articles}";
            }
            else
            {
                string punishment = minutes > 0
                    ? $"JAIL {minutes} мин"
                    : (fine > 0 ? $"штраф {fine} $" : "WARN / наказание по регламенту статьи");
                GeneratedCommand = $"Наказание для {targetId}: {punishment}. Статьи: {articles}. Основание: свод правил NOVA RP.";
            }
        }

        private void RecordPunishment()
        {
            var selected = FilteredCodebook.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0 || string.IsNullOrWhiteSpace(GeneratedCommand))
                return;

            PunishmentLogModule.Instance.Add(new PunishmentRecord
            {
                TargetId = string.IsNullOrWhiteSpace(TargetId) ? "—" : TargetId,
                Mode = IsIcMode ? "IC" : "OOC",
                Articles = string.Join(", ", selected.Select(i => $"{i.Category} {i.Article}")),
                Minutes = TotalJailMinutes,
                Fine = TotalFineAmount,
                Text = GeneratedCommand,
                IssuedBy = ConfigService.Instance.Settings.Profile.Summary
            });
        }

        public void CopyGeneratedCommand()
        {
            if (!string.IsNullOrWhiteSpace(GeneratedCommand))
                RequestCopyToClipboard?.Invoke(this, GeneratedCommand);
        }

        public void SendGeneratedCommand()
        {
            if (string.IsNullOrWhiteSpace(GeneratedCommand)) return;

            if (GeneratedCommand.Contains('\n'))
            {
                var lines = GeneratedCommand
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .ToList();
                RequestSendChain?.Invoke(this, lines);
            }
            else
            {
                RequestSendText?.Invoke(this, GeneratedCommand);
            }
        }

        public void ResetCodebookSelection()
        {
            foreach (var item in FilteredCodebook)
                item.IsSelected = false;
        }

        #endregion

        #region ==================== Таймеры и авто-пиар ====================

        public ObservableCollection<TimerModule.TimerInfo> ActiveTimers { get; } = new();

        public void RefreshTimers()
        {
            ActiveTimers.Clear();
            foreach (var t in TimerModule.Instance.GetActiveTimers())
                ActiveTimers.Add(t);
        }

        private bool _autoAdvertEnabled;
        public bool AutoAdvertEnabled
        {
            get => _autoAdvertEnabled;
            set
            {
                _autoAdvertEnabled = value;
                OnPropertyChanged();
                _ = ConfigService.Instance.UpdateSettingsAsync(s => s.AutoAdvert.Enabled = value);
            }
        }

        private int _autoAdvertInterval = 180;
        public int AutoAdvertInterval
        {
            get => _autoAdvertInterval;
            set
            {
                _autoAdvertInterval = value;
                OnPropertyChanged();
                _ = ConfigService.Instance.UpdateSettingsAsync(s => s.AutoAdvert.IntervalSeconds = value);
                AutoAdvertModule.Instance.UpdateInterval(value);
            }
        }

        private string _autoAdvertText = string.Empty;
        public string AutoAdvertText
        {
            get => _autoAdvertText;
            set
            {
                _autoAdvertText = value;
                OnPropertyChanged();
                _ = ConfigService.Instance.UpdateSettingsAsync(s => s.AutoAdvert.Text = value);
            }
        }

        private bool _autoAdvertRandomize = true;
        public bool AutoAdvertRandomize
        {
            get => _autoAdvertRandomize;
            set
            {
                _autoAdvertRandomize = value;
                OnPropertyChanged();
                _ = ConfigService.Instance.UpdateSettingsAsync(s => s.AutoAdvert.RandomizeEnding = value);
            }
        }

        #endregion

        #region ==================== Совместимость ====================

        /// <summary>Статистика OCR больше не ведётся (снайпер удалён); метод оставлен для code-behind.</summary>
        public void RefreshStatistics()
        {
        }

        #endregion

        #region ==================== Constructor / события / INPC ====================

        public MainViewModel()
        {
            RecordPunishmentCommand = new RelayCommand(_ => RecordPunishment());

            LoadInitialData();
            SubscribeToEvents();
        }

        private void LoadInitialData()
        {
            var settings = ConfigService.Instance.Settings;

            _autoAdvertEnabled = settings.AutoAdvert.Enabled;
            _autoAdvertInterval = settings.AutoAdvert.IntervalSeconds;
            _autoAdvertText = settings.AutoAdvert.Text;
            _autoAdvertRandomize = settings.AutoAdvert.RandomizeEnding;

            RefreshBinders();
            RefreshCodebook();
            RefreshTimers();

            // Режим наказаний по умолчанию из профиля роли
            IsIcMode = settings.Profile.Role is "Gov" or "Crime";
        }

        private int _codebookRefreshPending;
        private int _bindersRefreshPending;

        private void ScheduleCodebookRefresh()
        {
            if (System.Threading.Interlocked.CompareExchange(ref _codebookRefreshPending, 1, 0) != 0)
                return;

            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                System.Threading.Interlocked.Exchange(ref _codebookRefreshPending, 0);
                RefreshCodebook();
            }));
        }

        private void ScheduleBindersRefresh()
        {
            if (System.Threading.Interlocked.CompareExchange(ref _bindersRefreshPending, 1, 0) != 0)
                return;

            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                System.Threading.Interlocked.Exchange(ref _bindersRefreshPending, 0);
                RefreshBinders();
            }));
        }

        private void SubscribeToEvents()
        {
            ConfigService.Instance.BindersChanged += (s, e) => ScheduleBindersRefresh();
            ConfigService.Instance.CodebookChanged += (s, e) => ScheduleCodebookRefresh();
            TimerModule.Instance.TimersChanged += (s, e) =>
                System.Windows.Application.Current?.Dispatcher.Invoke(RefreshTimers);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// Обёртка статьи кодекса для UI: выделение в протокол и видимость при фильтре.
    /// </summary>
    public class CodeBookItemViewModel : INotifyPropertyChanged
    {
        private readonly CodeBookItem _item;
        private bool _isSelected;
        private bool _isVisible = true;

        public CodeBookItemViewModel(CodeBookItem item)
        {
            _item = item;
        }

        public Guid Id => _item.Id;
        public string Category => _item.Category;
        public string Article => _item.Article;
        public string Title => _item.Title;
        public string Description => _item.Description;
        public int FineAmount => _item.FineAmount;
        public int JailMinutes => _item.JailMinutes;
        public string Command => _item.Command;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}