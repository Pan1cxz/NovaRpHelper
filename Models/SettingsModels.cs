using System;
using System.Collections.Generic;

namespace NovaRpHelper.Models
{
    public class AppSettings
    {
        public GeneralSettings General { get; set; } = new();
        public OcrZoneSettings OcrZone { get; set; } = new();
        public SnipeFilterSettings SnipeFilter { get; set; } = new();
        public AutoAdvertSettings AutoAdvert { get; set; } = new();
        public PayDaySettings PayDayTimer { get; set; } = new();
        public ApplicationSettings Application { get; set; } = new();
        public ProfileSettings Profile { get; set; } = new();
    }

    public class GeneralSettings
    {
        /// <summary>INSERT (0x2D) — клавиша-модификатор для активации click-through окна</summary>
        public int ModifierKey { get; set; }
        /// <summary>T (0x54) — клавиша открытия чата</summary>
        public int ChatKey { get; set; }
        public int EnterKey { get; set; }
        public int EscapeKey { get; set; }
        public int MinDelayMs { get; set; }
        public int MaxDelayMs { get; set; }
        public int ChainDelayMs { get; set; }
        public bool IgnoreWhenNotInFocus { get; set; }
    }

    public class OcrZoneSettings
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int ScanIntervalMs { get; set; }
        public bool Enabled { get; set; }
    }

    public class SnipeFilterSettings
    {
        public bool Enabled { get; set; }
        public List<string> Keywords { get; set; } = new();
        public bool PlaySound { get; set; }
        public bool CaseSensitive { get; set; }
    }

    public class AutoAdvertSettings
    {
        public bool Enabled { get; set; }
        public int IntervalSeconds { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool RandomizeEnding { get; set; }
    }

    public class PayDaySettings
    {
        public bool Enabled { get; set; }
        public int Minutes { get; set; }
    }

    public class ApplicationSettings
    {
        public double OverlayOpacity { get; set; }
        public string Theme { get; set; } = "DarkModern";
        public bool StartWithOverlayVisible { get; set; }
        public bool ShowOnlyInGame { get; set; }
        public double OverlayWidth { get; set; } = 420;
        public double OverlayHeight { get; set; } = 640;
        public double OverlayLeft { get; set; } = 100;
        public double OverlayTop { get; set; } = 100;
    }

    public class BinderEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string HotKey { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool IsChain { get; set; }
        public List<string> ChainLines { get; set; } = new();
        public bool UseTargetId { get; set; }
        public bool Enabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class CodeBookItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Category { get; set; } = string.Empty;
        public string Article { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int FineAmount { get; set; }
        public int JailMinutes { get; set; }
        public string Command { get; set; } = string.Empty;
    }

    public class StatisticsData
    {
        public DateTime SessionStart { get; set; } = DateTime.Now;
        public long TotalIncome { get; set; }
        public long TotalExpenses { get; set; }
        public int ArrestCount { get; set; }
        public int HealCount { get; set; }
        public int PayDayCount { get; set; }
        public Dictionary<string, int> CustomCounters { get; set; } = new();
    }


    public class ProfileSettings
    {
        public string Nick { get; set; } = string.Empty;
        /// <summary>Civilian | Admin | Gov | Crime</summary>
        public string Role { get; set; } = string.Empty;
        public string AdminRank { get; set; } = string.Empty;
        public string GovFaction { get; set; } = string.Empty;
        public string GovRank { get; set; } = string.Empty;
        public string CrimeOrg { get; set; } = string.Empty;
        public string CrimeRank { get; set; } = string.Empty;

        public bool IsRegistered =>
            !string.IsNullOrWhiteSpace(Nick) && !string.IsNullOrWhiteSpace(Role);

        public string Summary => Role switch
        {
            "Admin" => $"Администрация ({Nick})",
            "Gov" => $"{GovFaction} | {GovRank} ({Nick})",
            "Crime" => $"{CrimeOrg} | {CrimeRank} ({Nick})",
            _ => $"Гражданский ({Nick})"
        };
    }
}