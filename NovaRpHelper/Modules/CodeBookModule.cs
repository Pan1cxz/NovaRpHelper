using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NovaRpHelper.Core;
using NovaRpHelper.Models;

namespace NovaRpHelper.Modules
{
    /// <summary>
    /// Оркестратор справочника: собирает сиды и дозаписывает отсутствующие записи
    /// ОДНИМ пакетом (одно событие CodebookChanged), с отложенным стартом.
    /// </summary>
    public class CodeBookModule
    {
        private static readonly Lazy<CodeBookModule> _instance = new(() => new CodeBookModule());
        public static CodeBookModule Instance => _instance.Value;

        private bool _isInitialized;

        private CodeBookModule() { }

        public void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(2000);
                    await MergeSeedAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CodeBookModule] Init error: {ex.Message}");
                }
            });
        }

        private async Task MergeSeedAsync()
        {
            var existing = new HashSet<string>(
                ConfigService.Instance.Codebook.Select(c => c.Category + "|" + c.Article),
                StringComparer.OrdinalIgnoreCase);

            var missing = new List<CodeBookItem>();

            foreach (var item in BuildSeed())
            {
                string key = item.Category + "|" + item.Article;
                if (existing.Contains(key)) continue;
                existing.Add(key);
                missing.Add(item);
            }

            if (missing.Count > 0)
            {
                await ConfigService.Instance.AddCodebookItemsAsync(missing);
            }
        }

        private static List<CodeBookItem> BuildSeed()
        {
            var list = CodeBookSeedCore.Build();
            list.AddRange(CodeBookSeedEvents.Build());
            list.AddRange(CodeBookSeedCharters.Build());
            list.AddRange(CodeBookSeedChartersExtra.Build());
            return list;
        }
    }
}