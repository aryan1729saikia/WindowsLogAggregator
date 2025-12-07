using System;
using System.Collections.ObjectModel;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LogViewerUI.ViewModels;
using System.Collections.Generic;

namespace LogViewerUI.Views
{
    public partial class DashboardPage : UserControl
    {
        private readonly DashboardViewModel _viewModel = new();
        private ObservableCollection<EventLogItem> _securityLogs = new();
        private ObservableCollection<EventLogItem> _firewallLogs = new();
        private ObservableCollection<EventLogItem> _dnsLogs = new();
        private ObservableCollection<EventLogItem> _applicationLogs = new();
        private ObservableCollection<EventLogItem> _systemLogs = new();
        private string _selectedFilter = "All";

        public DashboardPage()
        {
            InitializeComponent();
            DataContext = _viewModel;
            
            // Bind each grid to its log collection
            SecurityGrid.ItemsSource = _securityLogs;
            FirewallGrid.ItemsSource = _firewallLogs;
            DnsGrid.ItemsSource = _dnsLogs;
            ApplicationGrid.ItemsSource = _applicationLogs;
            SystemGrid.ItemsSource = _systemLogs;

            // Load all tabs on startup
            // Delay to ensure UI is fully initialized
            Dispatcher.BeginInvoke(() => LoadAllLogs(), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// Load all log types
        /// </summary>
        private void LoadAllLogs()
        {
            StatusText.Text = "Loading all logs...";
            _ = Task.Run(async () =>
            {
                // Clear logs
                await Dispatcher.BeginInvoke(() =>
                {
                    _securityLogs.Clear();
                    _firewallLogs.Clear();
                    _dnsLogs.Clear();
                    _applicationLogs.Clear();
                    _systemLogs.Clear();
                });

                // Load all tabs in parallel
                var securityTask = FetchLogsAsync("Security", _securityLogs);
                var firewallTask = FetchLogsAsync("Microsoft-Windows-Windows Firewall With Advanced Security/Firewall", _firewallLogs);
                var dnsTask = FetchLogsAsync("Microsoft-Windows-DNS-Client/Operational", _dnsLogs);
                var appTask = FetchLogsAsync("Application", _applicationLogs);
                var sysTask = FetchLogsAsync("System", _systemLogs);

                await Task.WhenAll(securityTask, firewallTask, dnsTask, appTask, sysTask);

                await Dispatcher.BeginInvoke(() =>
                {
                    UpdateStats();
                    ApplyFilter();
                    StatusText.Text = "All logs loaded successfully";
                });
            });
        }


        /// <summary>
        /// Fetch logs for a specific log type
        /// </summary>
        private async Task FetchLogsAsync(string logType, ObservableCollection<EventLogItem> targetCollection)
        {
            const int MAX_LOGS = 100;

            await Task.Run(() =>
            {
                try
                {
                    string query = "*";
                    using var reader = new EventLogReader(new EventLogQuery(logType, PathType.LogName, query)
                    {
                        ReverseDirection = true  // Newest first
                    });

                    EventRecord record;
                    int count = 0;

                    while ((record = reader.ReadEvent()) != null && count < MAX_LOGS)
                    {
                        try
                        {
                            string level = record.LevelDisplayName;
                            if (string.IsNullOrEmpty(level))
                                level = "Information";
                            else
                                level = level.Split(' ').FirstOrDefault() ?? "Information";

                            string message = record.FormatDescription() ?? "";
                            if (message.Length > 200)
                                message = message.Substring(0, 200) + "...";

                            var logItem = new EventLogItem
                            {
                                Timestamp = record.TimeCreated ?? DateTime.Now,
                                EventId = record.Id,
                                Level = level,
                                Source = record.ProviderName ?? "Unknown",
                                Computer = Environment.MachineName,
                                User = "N/A",
                                Message = message,
                                LogType = logType
                            };

                            Dispatcher.BeginInvoke(() => targetCollection.Add(logItem));
                            count++;

                            if (count % 20 == 0)
                                Thread.Sleep(1);
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
                catch (EventLogException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"EventLog error: {ex.Message}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Update stats for all tabs
        /// </summary>
        private void UpdateStats()
        {
            UpdateTabStats("Security", _securityLogs, SecurityCountText, SecurityErrorCount, SecurityWarningCount, SecurityInfoCount);
            UpdateTabStats("Firewall", _firewallLogs, FirewallCountText, FirewallErrorCount, FirewallWarningCount, FirewallInfoCount);
            UpdateTabStats("DNS", _dnsLogs, DnsCountText, DnsErrorCount, DnsWarningCount, DnsInfoCount);
            UpdateTabStats("Application", _applicationLogs, ApplicationCountText, ApplicationErrorCount, ApplicationWarningCount, ApplicationInfoCount);
            UpdateTabStats("System", _systemLogs, SystemCountText, SystemErrorCount, SystemWarningCount, SystemInfoCount);
        }

        private void UpdateTabStats(string name, ObservableCollection<EventLogItem> logs, TextBlock countText, TextBlock errorCount, TextBlock warningCount, TextBlock infoCount)
        {
            var errors = logs.Count(l => l.Level.Contains("Error"));
            var warnings = logs.Count(l => l.Level.Contains("Warning"));
            var infos = logs.Count - errors - warnings;

            countText.Text = $"{logs.Count} events loaded";
            errorCount.Text = errors.ToString();
            warningCount.Text = warnings.ToString();
            infoCount.Text = infos.ToString();
        }

        /// <summary>
        /// Filter logs based on selected level
        /// </summary>
        private void FilterLogs_Changed(object sender, RoutedEventArgs e)
        {
            if (AllRadio.IsChecked == true)
                _selectedFilter = "All";
            else if (ErrorRadio.IsChecked == true)
                _selectedFilter = "Error";
            else if (WarningRadio.IsChecked == true)
                _selectedFilter = "Warning";
            else if (InfoRadio.IsChecked == true)
                _selectedFilter = "Information";

            ApplyFilter();
        }

        /// <summary>
        /// Apply selected filter to all collections
        /// </summary>
        private void ApplyFilter()
        {
            if (SecurityGrid != null)   FilterCollection(_securityLogs, SecurityGrid);
            if (FirewallGrid != null)   FilterCollection(_firewallLogs, FirewallGrid);
            if (DnsGrid != null)   FilterCollection(_dnsLogs, DnsGrid);
            if (ApplicationGrid != null)   FilterCollection(_applicationLogs, ApplicationGrid);
            if (SystemGrid != null)   FilterCollection(_systemLogs, SystemGrid);
        }

        private void FilterCollection(ObservableCollection<EventLogItem> collection, DataGrid grid)
        {
            if (grid == null)   return;
            if (_selectedFilter == "All")
            {
                grid.Items.Filter = null;
            }
            else
            {
                grid.Items.Filter = (item) =>
                {
                    if (item is EventLogItem logItem)
                        return logItem.Level.Contains(_selectedFilter);
                    return true;
                };
            }
            grid.Items.Refresh();
        }

        /// <summary>
        /// Refresh all logs
        /// </summary>
        private void RefreshAll_Click(object sender, RoutedEventArgs e)
        {
            LoadAllLogs();
        }

        /// <summary>
        /// Export current visible logs to CSV
        /// </summary>
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var allLogs = new List<EventLogItem>();
                allLogs.AddRange(_securityLogs);
                allLogs.AddRange(_firewallLogs);
                allLogs.AddRange(_dnsLogs);
                allLogs.AddRange(_applicationLogs);
                allLogs.AddRange(_systemLogs);

                var csv = "Timestamp,EventID,Level,Source,Computer,User,Message\n";
                foreach (var log in allLogs)
                {
                    csv += $"\"{log.Timestamp:yyyy-MM-dd HH:mm:ss}\",{log.EventId},\"{log.Level}\",\"{log.Source}\",\"{log.Computer}\",\"{log.User}\",\"{log.Message.Replace("\"", "\"\"")}\"\n";
                }

                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var fileName = $"logs_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = System.IO.Path.Combine(desktopPath, fileName);

                System.IO.File.WriteAllText(filePath, csv);
                StatusText.Text = $"✅ Exported {allLogs.Count} events to {fileName}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ Export failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Clear all logs
        /// </summary>
        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            _securityLogs.Clear();
            _firewallLogs.Clear();
            _dnsLogs.Clear();
            _applicationLogs.Clear();
            _systemLogs.Clear();
            StatusText.Text = "All logs cleared";
        }
    }
}
