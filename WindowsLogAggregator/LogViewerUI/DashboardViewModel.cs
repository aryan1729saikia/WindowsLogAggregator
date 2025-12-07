using System;
using System.Collections.ObjectModel;

namespace LogViewerUI.ViewModels
{
    public class DashboardViewModel
    {
        public ObservableCollection<EventLogItem> CurrentLogs { get; } = new();
        public ObservableCollection<LogTypeMetrics> LogMetrics { get; } = new();
        public string SelectedLogType { get; set; } = "Security";
    }

    public class EventLogItem
    {
        public DateTime Timestamp { get; set; }
        public int EventId { get; set; }
        public string Level { get; set; } = "";
        public string Source { get; set; } = "";
        public string Computer { get; set; } = "";
        public string User { get; set; } = "";
        public string Message { get; set; } = "";
        public string LogType { get; set; } = "";
    }

    public class LogTypeMetrics
    {
        public string LogType { get; set; } = "";
        public int TotalCount { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}