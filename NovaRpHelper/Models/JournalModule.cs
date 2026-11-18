using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;

namespace NovaRpHelper.Modules
{
    public class NoteItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class LedgerEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public long Amount { get; set; }
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class CheckItem : INotifyPropertyChanged
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Text { get; set; } = string.Empty;

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);
    }

    /// <summary>
    /// Личный журнал игрока: заметки, ручная бухгалтерия, чек-лист смены.
    /// Не даёт игровых преимуществ: не читает чат, не автоматизирует действия.
    /// Хранение: %AppData%\NovaRpHelper\journal.json
    /// </summary>
    public class JournalModule : INotifyPropertyChanged
    {
        private static readonly Lazy<JournalModule> _instance = new(() => new JournalModule());
        public static JournalModule Instance => _instance.Value;

        private readonly string _path;
        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public ObservableCollection<NoteItem> Notes { get; } = new();
        public ObservableCollection<LedgerEntry> Ledger { get; } = new();
        public ObservableCollection<CheckItem> Checklist { get; } = new();

        private string _newNote = string.Empty;
        public string NewNote
        {
            get => _newNote;
            set { _newNote = value; OnPropertyChanged(); }
        }

        private string _ledgerAmount = string.Empty;
        public string LedgerAmount
        {
            get => _ledgerAmount;
            set { _ledgerAmount = value; OnPropertyChanged(); }
        }

        private string _ledgerComment = string.Empty;
        public string LedgerComment
        {
            get => _ledgerComment;
            set { _ledgerComment = value; OnPropertyChanged(); }
        }

        private string _newCheck = string.Empty;
        public string NewCheck
        {
            get => _newCheck;
            set { _newCheck = value; OnPropertyChanged(); }
        }

        public long TotalBalance => Ledger.Sum(x => x.Amount);

        public ICommand AddNoteCommand { get; }
        public ICommand DeleteNoteCommand { get; }
        public ICommand AddIncomeCommand { get; }
        public ICommand AddExpenseCommand { get; }
        public ICommand DeleteLedgerCommand { get; }
        public ICommand AddCheckCommand { get; }
        public ICommand DeleteCheckCommand { get; }
        public ICommand ResetChecksCommand { get; }

        private JournalModule()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NovaRpHelper");
            Directory.CreateDirectory(folder);
            _path = Path.Combine(folder, "journal.json");

            AddNoteCommand = new RelayCommand(_ => AddNote());
            DeleteNoteCommand = new RelayCommand(p => DeleteNote(p));
            AddIncomeCommand = new RelayCommand(_ => AddLedger(true));
            AddExpenseCommand = new RelayCommand(_ => AddLedger(false));
            DeleteLedgerCommand = new RelayCommand(p => DeleteLedger(p));
            AddCheckCommand = new RelayCommand(_ => AddCheck());
            DeleteCheckCommand = new RelayCommand(p => DeleteCheck(p));
            ResetChecksCommand = new RelayCommand(_ => ResetChecks());

            Load();

            if (Checklist.Count == 0)
            {
                foreach (var text in new[] { "Маска", "Бронежилет", "Патроны", "Аптечка", "Вода", "Рация", "Документы", "Форма по уставу" })
                    Checklist.Add(new CheckItem { Text = text });
                Save();
            }
        }

        #region ==================== Mutations ====================

        private void AddNote()
        {
            if (string.IsNullOrWhiteSpace(NewNote)) return;
            Notes.Insert(0, new NoteItem { Text = NewNote.Trim() });
            NewNote = string.Empty;
            Save();
        }

        private void DeleteNote(object? p)
        {
            if (p is NoteItem n && Notes.Remove(n))
                Save();
        }

        private void AddLedger(bool income)
        {
            string raw = LedgerAmount.Replace(" ", "").Replace("$", "");
            if (!long.TryParse(raw, out long sum) || sum <= 0) return;

            Ledger.Insert(0, new LedgerEntry
            {
                Amount = income ? sum : -sum,
                Comment = string.IsNullOrWhiteSpace(LedgerComment) ? (income ? "Приход" : "Расход") : LedgerComment.Trim()
            });

            LedgerAmount = string.Empty;
            LedgerComment = string.Empty;
            OnPropertyChanged(nameof(TotalBalance));
            Save();
        }

        private void DeleteLedger(object? p)
        {
            if (p is LedgerEntry e && Ledger.Remove(e))
            {
                OnPropertyChanged(nameof(TotalBalance));
                Save();
            }
        }

        private void AddCheck()
        {
            if (string.IsNullOrWhiteSpace(NewCheck)) return;
            var item = new CheckItem { Text = NewCheck.Trim() };
            Hook(item);
            Checklist.Add(item);
            NewCheck = string.Empty;
            Save();
        }

        private void DeleteCheck(object? p)
        {
            if (p is CheckItem c && Checklist.Remove(c))
                Save();
        }

        private void ResetChecks()
        {
            foreach (var c in Checklist)
                c.IsChecked = false;
            Save();
        }

        private void Hook(CheckItem item)
        {
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CheckItem.IsChecked))
                    Save();
            };
        }

        #endregion

        #region ==================== Persistence ====================

        private class JournalData
        {
            public List<NoteItem> Notes { get; set; } = new();
            public List<LedgerEntry> Ledger { get; set; } = new();
            public List<CheckItem> Checklist { get; set; } = new();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_path)) return;
                string json = File.ReadAllText(_path);
                var data = JsonSerializer.Deserialize<JournalData>(json, _jsonOptions);
                if (data == null) return;

                foreach (var n in data.Notes) Notes.Add(n);
                foreach (var l in data.Ledger) Ledger.Add(l);
                foreach (var c in data.Checklist)
                {
                    Hook(c);
                    Checklist.Add(c);
                }

                OnPropertyChanged(nameof(TotalBalance));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[JournalModule] Load error: {ex.Message}");
            }
        }

        private void Save()
        {
            try
            {
                var data = new JournalData
                {
                    Notes = Notes.ToList(),
                    Ledger = Ledger.ToList(),
                    Checklist = Checklist.ToList()
                };
                string json = JsonSerializer.Serialize(data, _jsonOptions);
                string tmp = _path + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(_path)) File.Delete(_path);
                File.Move(tmp, _path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[JournalModule] Save error: {ex.Message}");
            }
        }

        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}