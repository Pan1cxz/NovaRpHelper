using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NovaRpHelper.Models;

namespace NovaRpHelper.Core
{
    /// <summary>
    /// Сервис загрузки/сохранения настроек в JSON (%AppData%\NovaRpHelper\).
    /// Первичная загрузка в конструкторе — СИНХРОННЫЙ IO (без sync-over-async, без deadlock на UI-потоке).
    /// Runtime-методы — async с ConfigureAwait(false), чтобы не захватывать UI-контекст.
    /// Атомарная запись через .tmp. Внутренние Save без повторного захвата лока (без deadlock).
    /// </summary>
    public class ConfigService
    {
        private static readonly Lazy<ConfigService> _instance = new(() => new ConfigService());
        public static ConfigService Instance => _instance.Value;

        private readonly string _configFolder;
        private readonly string _configPath;
        private readonly string _bindersPath;
        private readonly string _codebookPath;
        private readonly string _statisticsPath;

        private readonly SemaphoreSlim _configLock = new(1, 1);
        private readonly SemaphoreSlim _bindersLock = new(1, 1);
        private readonly SemaphoreSlim _codebookLock = new(1, 1);
        private readonly SemaphoreSlim _statsLock = new(1, 1);

        private readonly JsonSerializerOptions _jsonOptions;

        private AppSettings _settings;
        private List<BinderEntry> _binders;
        private List<CodeBookItem> _codebook;
        private StatisticsData _statistics;

        public AppSettings Settings => _settings;
        public IReadOnlyList<BinderEntry> Binders => _binders;
        public IReadOnlyList<CodeBookItem> Codebook => _codebook;
        public StatisticsData Statistics => _statistics;

        public event EventHandler? SettingsChanged;
        public event EventHandler? BindersChanged;
        public event EventHandler? CodebookChanged;
        public event EventHandler? StatisticsChanged;

        private ConfigService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _configFolder = Path.Combine(appData, "NovaRpHelper");
            Directory.CreateDirectory(_configFolder);

            _configPath = Path.Combine(_configFolder, "config.json");
            _bindersPath = Path.Combine(_configFolder, "binders.json");
            _codebookPath = Path.Combine(_configFolder, "codebook.json");
            _statisticsPath = Path.Combine(_configFolder, "statistics.json");

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Converters = { new JsonStringEnumConverter() }
            };

            _settings = CreateDefaultSettings();
            _binders = new List<BinderEntry>();
            _codebook = new List<CodeBookItem>();
            _statistics = new StatisticsData();

            LoadAllSync();
        }

        #region ==================== Sync IO helpers (для конструктора) ====================

        private static string? TryReadFile(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConfigService.TryReadFile({path}): {ex.Message}");
                return null;
            }
        }

        private static void WriteAtomicSync(string path, string json)
        {
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tempPath, path);
        }

        /// <summary>
        /// Полностью синхронная первичная загрузка. Никаких async/await -> нет deadlock на UI-потоке.
        /// </summary>
        private void LoadAllSync()
        {
            // settings
            string? json = TryReadFile(_configPath);
            if (json != null)
            {
                try
                {
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
                    if (loaded != null)
                        _settings = MergeWithDefaults(loaded);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ConfigService.LoadAllSync settings: {ex.Message}");
                    _settings = CreateDefaultSettings();
                }
            }
            else
            {
                _settings = CreateDefaultSettings();
                try
                {
                    WriteAtomicSync(_configPath, JsonSerializer.Serialize(_settings, _jsonOptions));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ConfigService.LoadAllSync write defaults: {ex.Message}");
                }
            }

            // binders
            json = TryReadFile(_bindersPath);
            if (json != null)
            {
                try
                {
                    var loaded = JsonSerializer.Deserialize<List<BinderEntry>>(json, _jsonOptions);
                    if (loaded != null)
                        _binders = loaded;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ConfigService.LoadAllSync binders: {ex.Message}");
                }
            }

            // codebook
            json = TryReadFile(_codebookPath);
            if (json != null)
            {
                try
                {
                    var loaded = JsonSerializer.Deserialize<List<CodeBookItem>>(json, _jsonOptions);
                    if (loaded != null)
                        _codebook = loaded;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ConfigService.LoadAllSync codebook: {ex.Message}");
                }
            }

            // statistics
            json = TryReadFile(_statisticsPath);
            if (json != null)
            {
                try
                {
                    var loaded = JsonSerializer.Deserialize<StatisticsData>(json, _jsonOptions);
                    if (loaded != null)
                        _statistics = loaded;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ConfigService.LoadAllSync statistics: {ex.Message}");
                }
            }
        }

        #endregion

        #region ==================== Async Load (runtime, ConfigureAwait(false)) ====================

        public async Task LoadAllAsync()
        {
            await LoadSettingsAsync().ConfigureAwait(false);
            await LoadBindersAsync().ConfigureAwait(false);
            await LoadCodebookAsync().ConfigureAwait(false);
            await LoadStatisticsAsync().ConfigureAwait(false);
        }

        public async Task LoadSettingsAsync()
        {
            await _configLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = await File.ReadAllTextAsync(_configPath).ConfigureAwait(false);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
                    if (loaded != null)
                    {
                        _settings = MergeWithDefaults(loaded);
                        SettingsChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
                else
                {
                    _settings = CreateDefaultSettings();
                    await SaveSettingsInternalAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConfigService.LoadSettingsAsync: {ex.Message}");
                _settings = CreateDefaultSettings();
            }
            finally
            {
                _configLock.Release();
            }
        }

        public async Task LoadBindersAsync()
        {
            await _bindersLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (File.Exists(_bindersPath))
                {
                    string json = await File.ReadAllTextAsync(_bindersPath).ConfigureAwait(false);
                    var loaded = JsonSerializer.Deserialize<List<BinderEntry>>(json, _jsonOptions);
                    if (loaded != null)
                    {
                        _binders = loaded;
                        BindersChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConfigService.LoadBindersAsync: {ex.Message}");
                _binders = new List<BinderEntry>();
            }
            finally
            {
                _bindersLock.Release();
            }
        }

        public async Task LoadCodebookAsync()
        {
            await _codebookLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (File.Exists(_codebookPath))
                {
                    string json = await File.ReadAllTextAsync(_codebookPath).ConfigureAwait(false);
                    var loaded = JsonSerializer.Deserialize<List<CodeBookItem>>(json, _jsonOptions);
                    if (loaded != null)
                    {
                        _codebook = loaded;
                        CodebookChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConfigService.LoadCodebookAsync: {ex.Message}");
                _codebook = new List<CodeBookItem>();
            }
            finally
            {
                _codebookLock.Release();
            }
        }

        public async Task LoadStatisticsAsync()
        {
            await _statsLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (File.Exists(_statisticsPath))
                {
                    string json = await File.ReadAllTextAsync(_statisticsPath).ConfigureAwait(false);
                    var loaded = JsonSerializer.Deserialize<StatisticsData>(json, _jsonOptions);
                    if (loaded != null)
                    {
                        _statistics = loaded;
                        StatisticsChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConfigService.LoadStatisticsAsync: {ex.Message}");
                _statistics = new StatisticsData();
            }
            finally
            {
                _statsLock.Release();
            }
        }

        #endregion

        #region ==================== Async Save (public, with lock) ====================

        public async Task SaveSettingsAsync()
        {
            await _configLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await SaveSettingsInternalAsync().ConfigureAwait(false);
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _configLock.Release();
            }
        }

        public async Task SaveBindersAsync()
        {
            await _bindersLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await SaveBindersInternalAsync().ConfigureAwait(false);
                BindersChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _bindersLock.Release();
            }
        }

        public async Task SaveCodebookAsync()
        {
            await _codebookLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await SaveCodebookInternalAsync().ConfigureAwait(false);
                CodebookChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _codebookLock.Release();
            }
        }

        public async Task SaveStatisticsAsync()
        {
            await _statsLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await SaveStatisticsInternalAsync().ConfigureAwait(false);
                StatisticsChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _statsLock.Release();
            }
        }

        #endregion

        #region ==================== Internal Save (NO lock) ====================

        private static async Task WriteAtomicAsync(string path, string json)
        {
            string tempPath = path + ".tmp";
            await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tempPath, path);
        }

        private async Task SaveSettingsInternalAsync()
        {
            await WriteAtomicAsync(_configPath, JsonSerializer.Serialize(_settings, _jsonOptions)).ConfigureAwait(false);
        }

        private async Task SaveBindersInternalAsync()
        {
            await WriteAtomicAsync(_bindersPath, JsonSerializer.Serialize(_binders, _jsonOptions)).ConfigureAwait(false);
        }

        private async Task SaveCodebookInternalAsync()
        {
            await WriteAtomicAsync(_codebookPath, JsonSerializer.Serialize(_codebook, _jsonOptions)).ConfigureAwait(false);
        }

        private async Task SaveStatisticsInternalAsync()
        {
            await WriteAtomicAsync(_statisticsPath, JsonSerializer.Serialize(_statistics, _jsonOptions)).ConfigureAwait(false);
        }

        #endregion

        #region ==================== Mutation API ====================

        public async Task UpdateSettingsAsync(Action<AppSettings> updater)
        {
            await _configLock.WaitAsync().ConfigureAwait(false);
            try
            {
                updater(_settings);
                await SaveSettingsInternalAsync().ConfigureAwait(false);
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _configLock.Release();
            }
        }

        public async Task AddBinderAsync(BinderEntry entry)
        {
            await _bindersLock.WaitAsync().ConfigureAwait(false);
            try
            {
                entry.Id = Guid.NewGuid();
                _binders.Add(entry);
                await SaveBindersInternalAsync().ConfigureAwait(false);
                BindersChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _bindersLock.Release();
            }
        }

        public async Task UpdateBinderAsync(Guid id, Action<BinderEntry> updater)
        {
            await _bindersLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var entry = _binders.FirstOrDefault(b => b.Id == id);
                if (entry != null)
                {
                    updater(entry);
                    await SaveBindersInternalAsync().ConfigureAwait(false);
                    BindersChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            finally
            {
                _bindersLock.Release();
            }
        }

        public async Task RemoveBinderAsync(Guid id)
        {
            await _bindersLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _binders.RemoveAll(b => b.Id == id);
                await SaveBindersInternalAsync().ConfigureAwait(false);
                BindersChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _bindersLock.Release();
            }
        }

        public async Task AddCodebookItemAsync(CodeBookItem item)
        {
            await _codebookLock.WaitAsync().ConfigureAwait(false);
            try
            {
                item.Id = Guid.NewGuid();
                _codebook.Add(item);
                await SaveCodebookInternalAsync().ConfigureAwait(false);
                CodebookChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _codebookLock.Release();
            }
        }

        public async Task AddCodebookItemsAsync(IEnumerable<CodeBookItem> items)
        {
            await _codebookLock.WaitAsync().ConfigureAwait(false);
            try
            {
                bool any = false;
                foreach (var item in items)
                {
                    item.Id = Guid.NewGuid();
                    _codebook.Add(item);
                    any = true;
                }

                if (any)
                {
                    await SaveCodebookInternalAsync().ConfigureAwait(false);
                    CodebookChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            finally
            {
                _codebookLock.Release();
            }
        }

        public async Task UpdateStatisticsAsync(Action<StatisticsData> updater)
        {
            await _statsLock.WaitAsync().ConfigureAwait(false);
            try
            {
                updater(_statistics);
                await SaveStatisticsInternalAsync().ConfigureAwait(false);
                StatisticsChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _statsLock.Release();
            }
        }

        public async Task ResetStatisticsAsync()
        {
            await _statsLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _statistics = new StatisticsData { SessionStart = DateTime.Now };
                await SaveStatisticsInternalAsync().ConfigureAwait(false);
                StatisticsChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _statsLock.Release();
            }
        }

        public string GetConfigFolderPath() => _configFolder;

        #endregion

        #region ==================== Defaults ====================

        private AppSettings CreateDefaultSettings()
        {
            return new AppSettings
            {
                General = new GeneralSettings
                {
                    ModifierKey = WinApiHelper.VK.INSERT,
                    ChatKey = WinApiHelper.VK.T,
                    EnterKey = WinApiHelper.VK.ENTER,
                    EscapeKey = WinApiHelper.VK.ESCAPE,
                    MinDelayMs = 40,
                    MaxDelayMs = 90,
                    ChainDelayMs = 1500,
                    IgnoreWhenNotInFocus = true
                },
                OcrZone = new OcrZoneSettings
                {
                    X = 50,
                    Y = 800,
                    Width = 600,
                    Height = 180,
                    ScanIntervalMs = 350,
                    Enabled = false
                },
                SnipeFilter = new SnipeFilterSettings
                {
                    Enabled = false,
                    Keywords = new List<string> { "BMW", "дом", "вызов", "скорая" },
                    PlaySound = true,
                    CaseSensitive = false
                },
                AutoAdvert = new AutoAdvertSettings
                {
                    Enabled = false,
                    IntervalSeconds = 180,
                    Text = "/ad [Мой текст]",
                    RandomizeEnding = true
                },
                PayDayTimer = new PayDaySettings
                {
                    Enabled = true,
                    Minutes = 60
                },
                Application = new ApplicationSettings
                {
                    OverlayOpacity = 0.85,
                    Theme = "DarkModern",
                    StartWithOverlayVisible = true,
                    ShowOnlyInGame = true,
                    OverlayWidth = 420,
                    OverlayHeight = 640,
                    OverlayLeft = 100,
                    OverlayTop = 100
                }
            };
        }

        private AppSettings MergeWithDefaults(AppSettings loaded)
        {
            var defaults = CreateDefaultSettings();

            loaded.General ??= defaults.General;
            loaded.OcrZone ??= defaults.OcrZone;
            loaded.SnipeFilter ??= defaults.SnipeFilter;
            loaded.AutoAdvert ??= defaults.AutoAdvert;
            loaded.PayDayTimer ??= defaults.PayDayTimer;
            loaded.Application ??= defaults.Application;
            loaded.Profile ??= defaults.Profile;

            loaded.SnipeFilter.Keywords ??= new List<string>();

            return loaded;
        }

        #endregion
    }
}