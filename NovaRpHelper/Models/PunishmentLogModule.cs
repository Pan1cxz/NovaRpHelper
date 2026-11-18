using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;

namespace NovaRpHelper.Modules
{
    /// <summary>
    /// Запись о выданном наказании (IC или OOC) с подписью выдавшего.
    /// </summary>
    public class PunishmentRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime When { get; set; } = DateTime.Now;
        public string TargetId { get; set; } = "—";
        public string Mode { get; set; } = "IC";
        public string Articles { get; set; } = string.Empty;
        public int Minutes { get; set; }
        public long Fine { get; set; }
        public string Text { get; set; } = string.Empty;
        public string IssuedBy { get; set; } = string.Empty;
    }

    /// <summary>
    /// Журнал выданных наказаний: история для отчётов и самопроверки администрации/лидеров.
    /// Хранение: %AppData%\NovaRpHelper\punishments.json
    /// </summary>
    public class PunishmentLogModule
    {
        private static readonly Lazy<PunishmentLogModule> _instance = new(() => new PunishmentLogModule());
        public static PunishmentLogModule Instance => _instance.Value;

        private readonly string _path;
        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public ObservableCollection<PunishmentRecord> Records { get; } = new();

        public ICommand ClearCommand { get; }

        private PunishmentLogModule()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NovaRpHelper");
            Directory.CreateDirectory(folder);
            _path = Path.Combine(folder, "punishments.json");

            ClearCommand = new RelayCommand(_ => Clear());

            Load();
        }

        public void Add(PunishmentRecord record)
        {
            Records.Insert(0, record);
            Save();
        }

        private void Clear()
        {
            Records.Clear();
            Save();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_path)) return;
                string json = File.ReadAllText(_path);
                var loaded = JsonSerializer.Deserialize<List<PunishmentRecord>>(json, _jsonOptions);
                if (loaded == null) return;
                foreach (var r in loaded)
                    Records.Add(r);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PunishmentLogModule] Load error: {ex.Message}");
            }
        }

        private void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(Records.ToList(), _jsonOptions);
                string tmp = _path + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(_path)) File.Delete(_path);
                File.Move(tmp, _path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PunishmentLogModule] Save error: {ex.Message}");
            }
        }
    }
}