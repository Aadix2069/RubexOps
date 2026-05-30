using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RubexOps
{
    public partial class ViewEditContractsPage : Page
    {
        private List<PurchaseContract> allContracts =
            new();

        private readonly string pythonExe =
            "python";

        public ViewEditContractsPage()
        {
            InitializeComponent();

            EnsureBackendFolderExists();

            SortComboBox.SelectedIndex = 0;

            FilterComboBox.SelectedIndex = -1;

            LoadContracts();
        }

        private void EnsureBackendFolderExists()
        {
            try
            {
                string backendFolder =
                    Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "backend"
                    );

                if (!Directory.Exists(backendFolder))
                {
                    Directory.CreateDirectory(backendFolder);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Backend Folder Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void LoadContracts()
        {
            try
            {
                Mouse.OverrideCursor =
                    Cursors.Wait;

                MainGrid.IsEnabled = false;

                MainGrid.Opacity = 0.75;

                string pythonScript = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "backend",
                    "read_purchase_contract.py"
                );

                if (!File.Exists(pythonScript))
                {
                    MessageBox.Show(
                        "Backend Python file not found.\n\n" +
                        pythonScript,
                        "File Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

                    return;
                }

                ProcessStartInfo start =
                    new()
                    {
                        FileName = pythonExe,

                        Arguments =
                            $"\"{pythonScript}\"",

                        UseShellExecute = false,

                        RedirectStandardOutput = true,

                        RedirectStandardError = true,

                        CreateNoWindow = true,

                        WorkingDirectory =
                            Path.Combine(
                                AppDomain.CurrentDomain.BaseDirectory,
                                "backend")
                    };

                using Process process =
                    Process.Start(start)
                    ?? throw new Exception(
                        "Failed to start backend process."
                    );

                string json =
                    process.StandardOutput.ReadToEnd();

                string error =
                    process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string message =
                        !string.IsNullOrWhiteSpace(error)
                            ? error.Trim()
                            : json.Trim();

                    if (string.IsNullOrWhiteSpace(message))
                    {
                        message =
                            $"Backend process failed with exit code {process.ExitCode}.";
                    }

                    MessageBox.Show(
                        message,
                        "Backend Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

                    return;
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    MessageBox.Show(
                        error,
                        "Backend Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

                    return;
                }

                if (json.TrimStart().StartsWith(
                        "ERROR",
                        StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        json,
                        "Backend Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

                    return;
                }

                JsonSerializerOptions options =
                    new()
                    {
                        PropertyNameCaseInsensitive = true
                    };

                allContracts =
                    JsonSerializer.Deserialize<List<PurchaseContract>>(
                        json,
                        options
                    ) ?? new();

                ApplySortingAndSearch();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Application Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                Mouse.OverrideCursor = null;

                MainGrid.IsEnabled = true;

                MainGrid.Opacity = 1;
            }
        }

        private void SearchBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            ApplySortingAndSearch();
        }

        private void FilterComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            ApplySortingAndSearch();
        }

        private void SortComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            ApplySortingAndSearch();
        }

        private void ApplySortingAndSearch()
        {
            if (ContractsItemsControl == null)
            {
                return;
            }

            IEnumerable<PurchaseContract> filtered =
                allContracts;

            string search =
                SearchBox?.Text?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(c =>
                    (!string.IsNullOrWhiteSpace(c.vendor_name) &&
                     c.vendor_name.Contains(
                         search,
                         StringComparison.OrdinalIgnoreCase))

                    ||

                    (!string.IsNullOrWhiteSpace(c.vendor_id) &&
                     c.vendor_id.Contains(
                         search,
                         StringComparison.OrdinalIgnoreCase)));
            }

            string filter =
                (FilterComboBox?.SelectedItem as ComboBoxItem)
                ?.Content
                ?.ToString()
                ?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(filter))
            {
                filtered =
                    filtered.Where(c => !c.IsExpiredStatus);
            }
            else if (!filter.Equals(
                         "All",
                         StringComparison.OrdinalIgnoreCase))
            {
                filtered =
                    filtered.Where(c =>
                        c.DisplayStatus.Equals(
                            filter,
                            StringComparison.OrdinalIgnoreCase));
            }

            filtered =
                ApplySort(filtered);

            ContractsItemsControl.ItemsSource =
                filtered.ToList();
        }

        private IEnumerable<PurchaseContract> ApplySort(
            IEnumerable<PurchaseContract> contracts)
        {
            string sort =
                (SortComboBox?.SelectedItem as ComboBoxItem)
                ?.Content
                ?.ToString()
                ?.Trim() ?? "Name Ascending";

            return sort switch
            {
                "Name Descending" =>
                    contracts
                        .OrderByDescending(c => c.vendor_name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(c => c.vendor_id ?? string.Empty, StringComparer.OrdinalIgnoreCase),

                "Agreed Qty Ascending" =>
                    contracts
                        .OrderBy(c => c.agreed_qty ?? 0)
                        .ThenBy(c => c.vendor_name ?? string.Empty, StringComparer.OrdinalIgnoreCase),

                "Agreed Qty Descending" =>
                    contracts
                        .OrderByDescending(c => c.agreed_qty ?? 0)
                        .ThenBy(c => c.vendor_name ?? string.Empty, StringComparer.OrdinalIgnoreCase),

                _ =>
                    contracts
                        .OrderBy(c => c.vendor_name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(c => c.StartDateSort)
            };
        }

        private void RenewContract_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                RenewContractWindow window =
                    new RenewContractWindow(allContracts);

                bool? result =
                    window.ShowDialog();

                if (result == true)
                {
                    LoadContracts();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Renew Contract Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void EditButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                Button button =
                    (Button)sender;

                PurchaseContract? contract =
                    button.DataContext as PurchaseContract;

                if (contract == null)
                {
                    return;
                }

                if (!contract.CanEdit)
                {
                    MessageBox.Show(
                        "Expired historical contracts are read-only.",
                        "Contract Locked",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );

                    return;
                }

                EditContractWindow window =
                    new(contract);

                bool? result =
                    window.ShowDialog();

                if (result == true)
                {
                    LoadContracts();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Application Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
    }

    public partial class PurchaseContract
    {
        public int row { get; set; }

        public string? vendor_name { get; set; }

        public string? vendor_id { get; set; }

        public string? contract_id { get; set; }

        public string? item_name { get; set; }

        public string? item_code { get; set; }

        public string? status { get; set; }

        public string? dynamic_status { get; set; }

        public string? status_color { get; set; }

        public string? start_date { get; set; }

        public string? end_date { get; set; }

        public string? days_remaining { get; set; }

        public string? deliveries_so_far { get; set; }

        public string? recent_delivery { get; set; }

        public double? base_price { get; set; }

        public double? agreed_qty { get; set; }

        public double? delivered_qty { get; set; }

        public double? qty_delivered_so_far { get; set; }

        public double? remaining_qty { get; set; }

        public double? completion_percent { get; set; }

        public string? breach { get; set; }

        public string? breach_responsibility { get; set; }

        public double? penalty_percent { get; set; }

        public double? revised_rate { get; set; }

        public double? penalty_value { get; set; }

        public double? remedy_days { get; set; }

        public string? remedy_deadline { get; set; }

        public string? remedy_status { get; set; }

        public string? renewal_reference { get; set; }

        public bool can_renew { get; set; }

        public bool can_edit { get; set; } = true;

        public bool is_expired { get; set; }

        public bool is_breach_detected { get; set; }

        public bool show_responsibility { get; set; }

        public bool show_breach_panel { get; set; }

        public string DisplayStatus
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(dynamic_status))
                {
                    return dynamic_status;
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    return status;
                }

                return "Unknown";
            }
        }

        public bool IsExpiredStatus
        {
            get
            {
                return is_expired ||
                       DisplayStatus.Equals(
                           "Expired",
                           StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool CanEdit
        {
            get
            {
                return can_edit && !IsExpiredStatus;
            }
        }

        public string EditToolTip
        {
            get
            {
                return CanEdit
                    ? "Edit contract"
                    : "Expired historical contracts are read-only";
            }
        }

        public double CardOpacity
        {
            get
            {
                return IsExpiredStatus ? 0.72 : 1.0;
            }
        }

        public string StatusBackground
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(status_color))
                {
                    return status_color;
                }

                return DisplayStatus switch
                {
                    "Active" => "#10B981",
                    "Upcoming" => "#3B82F6",
                    "Completed" => "#8B5CF6",
                    "Violated" => "#EF4444",
                    "Expired" => "#F59E0B",
                    _ => "#9CA3AF"
                };
            }
        }

        public int StatusPriority
        {
            get
            {
                return DisplayStatus switch
                {
                    "Active" => 0,
                    "Upcoming" => 1,
                    "Violated" => 2,
                    "Completed" => 3,
                    "Expired" => 4,
                    _ => 5
                };
            }
        }

        public DateTime StartDateSort
        {
            get
            {
                if (DateTime.TryParseExact(
                        start_date,
                        "dd-MM-yyyy",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime parsedDate))
                {
                    return parsedDate;
                }

                return DateTime.MinValue;
            }
        }

        public string ContractIdDisplay =>
            string.IsNullOrWhiteSpace(contract_id)
                ? "-"
                : contract_id;

        public string ItemCodeDisplay =>
            string.IsNullOrWhiteSpace(item_code)
                ? "NRFC"
                : item_code;

        public string BasePriceDisplay =>
            $"Rs. {FormatNumber(base_price)}";

        public string AgreedQtyDisplay =>
            FormatNumber(agreed_qty);

        public string DeliveredQtyDisplay =>
            FormatNumber(delivered_qty);

        public string PurchasedQtySoFarDisplay =>
            FormatNumber(qty_delivered_so_far ?? delivered_qty);

        public string QtyDeliveredSoFarDisplay =>
            FormatNumber(qty_delivered_so_far ?? delivered_qty);

        public string DeliveriesSoFarDisplay =>
            string.IsNullOrWhiteSpace(deliveries_so_far)
                ? "0"
                : deliveries_so_far;

        public string RemainingQtyDisplay =>
            FormatNumber(remaining_qty);

        public string RevisedRateDisplay =>
            revised_rate.HasValue
                ? $"Rs. {FormatNumber(revised_rate)}"
                : "Not calculated";

        public string BreachDisplay
        {
            get
            {
                return IsBreachDetected ? "YES" : "NO";
            }
        }

        public bool IsBreachDetected
        {
            get
            {
                if (is_breach_detected)
                {
                    return true;
                }

                string text =
                    breach?.Trim() ?? "";

                return text.Equals("YES", StringComparison.OrdinalIgnoreCase) ||
                       text.Equals("Y", StringComparison.OrdinalIgnoreCase) ||
                       text.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
            }
        }

        public Visibility BreachPanelVisibility
        {
            get
            {
                return IsBreachDetected || show_breach_panel
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        public double SafeCompletionPercent
        {
            get
            {
                double value =
                    completion_percent ?? 0;

                if (value < 0)
                {
                    return 0;
                }

                if (value > 100)
                {
                    return 100;
                }

                return value;
            }
        }

        public string CompletionDisplay =>
            $"{SafeCompletionPercent:0.##}%";

        public string DaysRemainingDisplay
        {
            get
            {
                if (string.IsNullOrWhiteSpace(days_remaining))
                {
                    return "0 days";
                }

                string text =
                    days_remaining.Trim();

                if (text.Contains(
                        "day",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }

                return $"{text} days";
            }
        }

        private static string FormatNumber(
            double? value)
        {
            if (!value.HasValue)
            {
                return "0";
            }

            return value.Value.ToString(
                "0.##",
                CultureInfo.InvariantCulture);
        }
    }
}