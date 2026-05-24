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
    public partial class ViewSalesContractsPage : Page
    {
        // =====================================================
        // MASTER LIST
        // =====================================================

        private List<SalesContract> allContracts = new();



        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        public ViewSalesContractsPage()
        {
            InitializeComponent();

            SortComboBox.SelectedIndex = 0;
            FilterComboBox.SelectedIndex = -1;

            LoadContracts();
        }



        // =====================================================
        // LOAD CONTRACTS
        // =====================================================

        private void LoadContracts()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                MainGrid.IsEnabled = false;
                MainGrid.Opacity = 0.75;

                string pythonScript = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "backend",
                    "read_sales_contract.py");

                if (!File.Exists(pythonScript))
                {
                    MessageBox.Show(
                        "Backend Python file not found.\n\n" + pythonScript,
                        "File Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                ProcessStartInfo start = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{pythonScript}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "backend")
                };

                using Process process =
                    Process.Start(start)
                    ?? throw new Exception("Failed to start backend process.");

                string json = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(error))
                {
                    MessageBox.Show(
                        !string.IsNullOrWhiteSpace(error) ? error.Trim() : json.Trim(),
                        "Backend Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                JsonSerializerOptions options = new()
                {
                    PropertyNameCaseInsensitive = true
                };

                allContracts =
                    JsonSerializer.Deserialize<List<SalesContract>>(
                        json,
                        options) ?? new();

                ApplySortingAndSearch();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Application Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                MainGrid.IsEnabled = true;
                MainGrid.Opacity = 1;
            }
        }



        // =====================================================
        // FILTER EVENTS
        // =====================================================

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
            ApplySortingAndSearch();

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            ApplySortingAndSearch();

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            ApplySortingAndSearch();



        // =====================================================
        // APPLY SEARCH + FILTER + SORT
        // =====================================================

        private void ApplySortingAndSearch()
        {
            if (ContractsItemsControl == null)
            {
                return;
            }

            IEnumerable<SalesContract> filtered = allContracts;
            string search = SearchBox?.Text?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(c =>
                    (!string.IsNullOrWhiteSpace(c.customer_name) &&
                     c.customer_name.Contains(search, StringComparison.OrdinalIgnoreCase))
                    ||
                    (!string.IsNullOrWhiteSpace(c.customer_id) &&
                     c.customer_id.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            string filter =
                (FilterComboBox?.SelectedItem as ComboBoxItem)
                ?.Content
                ?.ToString()
                ?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(filter))
            {
                filtered = filtered.Where(c => !c.IsExpiredStatus);
            }
            else if (!filter.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(c =>
                    c.DisplayStatus.Equals(filter, StringComparison.OrdinalIgnoreCase));
            }

            filtered = ApplySort(filtered);
            ContractsItemsControl.ItemsSource = filtered.ToList();
        }



        // =====================================================
        // SORT
        // =====================================================

        private IEnumerable<SalesContract> ApplySort(
            IEnumerable<SalesContract> contracts)
        {
            string sort =
                (SortComboBox?.SelectedItem as ComboBoxItem)
                ?.Content
                ?.ToString()
                ?.Trim() ?? "Name Ascending";

            return sort switch
            {
                "Name Descending" =>
                    contracts.OrderBy(c => c.IsExpiredStatus)
                             .ThenBy(c => c.StatusPriority)
                             .ThenByDescending(c => c.customer_name),

                "Quantity Ascending" =>
                    contracts.OrderBy(c => c.IsExpiredStatus)
                             .ThenBy(c => c.StatusPriority)
                             .ThenBy(c => c.agreed_qty ?? 0),

                "Quantity Descending" =>
                    contracts.OrderBy(c => c.IsExpiredStatus)
                             .ThenBy(c => c.StatusPriority)
                             .ThenByDescending(c => c.agreed_qty ?? 0),

                "Base Rate Ascending" =>
                    contracts.OrderBy(c => c.IsExpiredStatus)
                             .ThenBy(c => c.StatusPriority)
                             .ThenBy(c => c.base_price ?? 0),

                "Base Rate Descending" =>
                    contracts.OrderBy(c => c.IsExpiredStatus)
                             .ThenBy(c => c.StatusPriority)
                             .ThenByDescending(c => c.base_price ?? 0),

                _ =>
                    contracts.OrderBy(c => c.IsExpiredStatus)
                             .ThenBy(c => c.StatusPriority)
                             .ThenBy(c => c.customer_name)
            };
        }



        // =====================================================
        // RENEW
        // =====================================================

        private void RenewContract_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                RenewSalesContractWindow window =
                    new RenewSalesContractWindow(allContracts);

                bool? result = window.ShowDialog();

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



        // =====================================================
        // EDIT
        // =====================================================

        private void EditButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                if (sender is not Button button ||
                    button.DataContext is not SalesContract contract)
                {
                    return;
                }

                if (!contract.CanEdit)
                {
                    MessageBox.Show(
                        "Expired historical sales contracts are read-only.",
                        "Contract Locked",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                EditSalesContractWindow window =
                    new EditSalesContractWindow(contract);

                bool? result = window.ShowDialog();

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
                    MessageBoxImage.Error);
            }
        }
    }



    // =====================================================
    // SALES CONTRACT MODEL
    // =====================================================

    public class SalesContract
    {
        public int row { get; set; }
        public string? customer_name { get; set; }
        public string? customer_id { get; set; }
        public string? item_name { get; set; }
        public string? item_code { get; set; }
        public string? status { get; set; }
        public string? dynamic_status { get; set; }
        public string? status_color { get; set; }
        public string? start_date { get; set; }
        public string? end_date { get; set; }
        public string? days_remaining { get; set; }
        public string? sales_so_far { get; set; }
        public double? base_price { get; set; }
        public double? agreed_qty { get; set; }
        public double? sold_qty { get; set; }
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
        public bool show_breach_panel { get; set; }

        public string DisplayStatus =>
            !string.IsNullOrWhiteSpace(dynamic_status)
                ? dynamic_status
                : status ?? "Unknown";

        public bool IsExpiredStatus =>
            is_expired ||
            DisplayStatus.Equals("Expired", StringComparison.OrdinalIgnoreCase);

        public bool CanEdit => can_edit && !IsExpiredStatus;

        public string EditToolTip =>
            CanEdit ? "Edit sales contract" : "Expired historical contracts are read-only";

        public double CardOpacity => IsExpiredStatus ? 0.72 : 1.0;

        public string StatusBackground =>
            !string.IsNullOrWhiteSpace(status_color)
                ? status_color
                : DisplayStatus switch
                {
                    "Active" => "#10B981",
                    "Upcoming" => "#3B82F6",
                    "Completed" => "#8B5CF6",
                    "Violated" => "#EF4444",
                    "Expired" => "#F59E0B",
                    _ => "#9CA3AF"
                };

        public int StatusPriority =>
            DisplayStatus switch
            {
                "Active" => 0,
                "Upcoming" => 1,
                "Violated" => 2,
                "Completed" => 3,
                "Expired" => 4,
                _ => 5
            };

        public bool IsBreachDetected =>
            is_breach_detected ||
            (breach ?? "").Equals("YES", StringComparison.OrdinalIgnoreCase) ||
            (breach ?? "").Equals("TRUE", StringComparison.OrdinalIgnoreCase);

        public Visibility BreachPanelVisibility =>
            IsBreachDetected || show_breach_panel
                ? Visibility.Visible
                : Visibility.Collapsed;

        public double SafeCompletionPercent
        {
            get
            {
                double value = completion_percent ?? 0;

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

        public string CompletionDisplay => $"{SafeCompletionPercent:0.##}%";
        public string BasePriceDisplay => $"Rs. {FormatNumber(base_price)}";
        public string AgreedQtyDisplay => FormatNumber(agreed_qty);
        public string SoldQtyDisplay => FormatNumber(sold_qty);
        public string RemainingQtyDisplay => FormatNumber(remaining_qty);
        public string RevisedRateDisplay => revised_rate.HasValue ? $"Rs. {FormatNumber(revised_rate)}" : "Not calculated";
        public string BreachDisplay => IsBreachDetected ? "YES" : "NO";

        public string DaysRemainingDisplay =>
            string.IsNullOrWhiteSpace(days_remaining)
                ? "Days remaining unavailable"
                : $"{days_remaining} days remaining";

        public string RenewDisplay =>
            $"{customer_name} - {customer_id} | Row {row} | {DisplayStatus}";

        private static string FormatNumber(double? value)
        {
            return !value.HasValue
                ? "0"
                : value.Value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
