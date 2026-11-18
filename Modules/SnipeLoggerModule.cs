using System;
using System.Collections.ObjectModel;
using System.Media;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NovaRpHelper.Core;

namespace NovaRpHelper.Modules
{
    /// <summary>
    /// OCR-Снайпер и Логгер: фильтр ключевых слов, SystemSounds.Beep,
    /// парсинг статистики сессии (доход/расход/аресты/лечение/PayDay).
    /// </summary>
    public class SnipeLoggerModule
    {
        private static readonly Lazy<SnipeLoggerModule> _instance = new(() => new SnipeLoggerModule());
        public static SnipeLoggerModule Instance => _instance.Value;

        public ObservableCollection<string> SnipeLog { get; } = new();
        private const int MaxLogSize = 50;

        private static readonly Regex IncomeRegex = new(@"(?:заработал|получил|начислено).*?(\d[\d\s]*)", RegexOptions.IgnoreCase);
        private static readonly Regex ExpenseRegex = new(@"(?:потратил|оплатил|штраф).*?(\d[\d\s]*)", RegexOptions.IgnoreCase);
        private static readonly Regex ArrestRegex = new(@"(?:арестован|заключен под стражу)", RegexOptions.IgnoreCase);
        private static readonly Regex HealRegex = new(@"(?:вылечен|использовал аптечку|восстановил здоровье)", RegexOptions.IgnoreCase);

        private SnipeLoggerModule()
        {
            ChatOcrEngine.Instance.OnNewChatLine += OnNewChatLineDetected;
        }

        private void OnNewChatLineDetected(object? sender, string line)
        {
            var settings = ConfigService.Instance.Settings.SnipeFilter;

            if (settings.Enabled && settings.Keywords != null && settings.Keywords.Count > 0)
            {
                StringComparison comparison = settings.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                bool isMatch = false;

                foreach (var keyword in settings.Keywords)
                {
                    if (!string.IsNullOrWhiteSpace(keyword) && line.Contains(keyword, comparison))
                    {
                        isMatch = true;
                        break;
                    }
                }

                if (isMatch)
                {
                    AddToSnipeLog($"[СНАЙПЕР] {line}");

                    if (settings.PlaySound)
                    {
                        _ = Task.Run(() =>
                        {
                            try { SystemSounds.Beep.Play(); } catch { }
                        });
                    }
                }
            }

            ParseStatistics(line);
        }

        private void AddToSnipeLog(string message)
        {
            string timestampedMsg = $"[{DateTime.Now:HH:mm:ss}] {message}";

            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                SnipeLog.Insert(0, timestampedMsg);
                while (SnipeLog.Count > MaxLogSize)
                    SnipeLog.RemoveAt(SnipeLog.Count - 1);
            });
        }

        private void ParseStatistics(string line)
        {
            var incomeMatch = IncomeRegex.Match(line);
            if (incomeMatch.Success && long.TryParse(incomeMatch.Groups[1].Value.Replace(" ", ""), out long income))
            {
                _ = ConfigService.Instance.UpdateStatisticsAsync(stats =>
                {
                    stats.TotalIncome += income;
                    stats.CustomCounters["LastIncome"] = (int)income;
                });
            }

            var expenseMatch = ExpenseRegex.Match(line);
            if (expenseMatch.Success && long.TryParse(expenseMatch.Groups[1].Value.Replace(" ", ""), out long expense))
            {
                _ = ConfigService.Instance.UpdateStatisticsAsync(stats => stats.TotalExpenses += expense);
            }

            if (ArrestRegex.IsMatch(line))
            {
                _ = ConfigService.Instance.UpdateStatisticsAsync(stats => stats.ArrestCount++);
            }

            if (HealRegex.IsMatch(line))
            {
                _ = ConfigService.Instance.UpdateStatisticsAsync(stats => stats.HealCount++);
            }

            if (line.Contains("PayDay", StringComparison.OrdinalIgnoreCase) || line.Contains("Зарплата", StringComparison.OrdinalIgnoreCase))
            {
                _ = ConfigService.Instance.UpdateStatisticsAsync(stats => stats.PayDayCount++);
            }
        }

        public void ClearLog()
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => SnipeLog.Clear());
        }
    }
}