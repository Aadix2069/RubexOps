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
    public partial class ViewSalesPage : Page
    {
        private List<SalesRecord> allRecords = new();

        public ViewSalesPage()
        {
            InitializeComponent();
            SortComboBox.SelectedIndex = 2;
            LoadSalesData();
        }

        private void LoadSalesData()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                MainGrid.IsEnabled = false;
                MainGrid.Opacity = 0.75;

                string pythonScript = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "backend",
                    "read_sales_data.py");

                if (!File.Exists(pythonScript))
                {
                    MessageBox.Show("Backend Python file not found.\n\n" + pythonScript, "File Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    WorkingDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backend")
                };

                using Process process = Process.Start(start) ?? throw new Exception("Failed to start backend process.");
                string json = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(error))
                {
                    MessageBox.Show(!string.IsNullOrWhiteSpace(error) ? error.Trim() : json.Trim(), "Backend Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                allRecords =
                    JsonSerializer.Deserialize<List<SalesRecord>>(
                        json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new();

                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                MainGrid.IsEnabled = true;
                MainGrid.Opacity = 1;
            }
        }

        private void FilterChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void DateFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadSalesData();
        }

        private void ApplyFilters()
        {
            if (SalesDataGrid == null)
            {
                return;
            }

            IEnumerable<SalesRecord> filtered = allRecords;
            string search = SearchBox?.Text?.Trim() ?? "";
            string itemSearch = ItemFilterBox?.Text?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(record =>
                    Contains(record.customer_name, search) ||
                    Contains(record.customer_id, search) ||
                    Contains(record.invoice_number, search));
            }

            if (!string.IsNullOrWhiteSpace(itemSearch))
            {
                filtered = filtered.Where(record =>
                    Contains(record.item_name, itemSearch) ||
                    Contains(record.item_code, itemSearch));
            }

            if (FromDatePicker?.SelectedDate != null)
            {
                filtered = filtered.Where(record =>
                    record.DispatchDateForSort >= FromDatePicker.SelectedDate.Value.Date);
            }

            if (ToDatePicker?.SelectedDate != null)
            {
                filtered = filtered.Where(record =>
                    record.DispatchDateForSort <= ToDatePicker.SelectedDate.Value.Date);
            }

            filtered = ApplySort(filtered);
            SalesDataGrid.ItemsSource = filtered.ToList();
        }

        private IEnumerable<SalesRecord> ApplySort(IEnumerable<SalesRecord> records)
        {
            string sort =
                (SortComboBox?.SelectedItem as ComboBoxItem)
                ?.Content
                ?.ToString() ?? "Dispatch Newest";

            return sort switch
            {
                "Name Ascending" => records.OrderBy(record => record.customer_name),
                "Name Descending" => records.OrderByDescending(record => record.customer_name),
                "Dispatch Oldest" => records.OrderBy(record => record.DispatchDateForSort),
                "Amount High to Low" => records.OrderByDescending(record => record.net_receivable),
                "Amount Low to High" => records.OrderBy(record => record.net_receivable),
                _ => records.OrderByDescending(record => record.DispatchDateForSort)
            };
        }

        private void SalesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SalesDataGrid.SelectedItem is not SalesRecord record)
            {
                SummaryTitleText.Text = "No record selected";
                SummarySubtitleText.Text = "Select a sales row to view details";
                SummaryDispatchText.Text = "-";
                SummaryWeightText.Text = "-";
                SummaryTaxableText.Text = "-";
                SummaryNetText.Text = "-";
                return;
            }

            SummaryTitleText.Text = $"{record.customer_name} ({record.customer_id})";
            SummarySubtitleText.Text = $"{record.invoice_number} | {record.item_code}";
            SummaryDispatchText.Text = record.dispatch;
            SummaryWeightText.Text = record.WeightDisplay;
            SummaryTaxableText.Text = record.TaxableAmountDisplay;
            SummaryNetText.Text = record.NetReceivableDisplay;
        }

        private static bool Contains(string? source, string value)
        {
            return !string.IsNullOrWhiteSpace(source) &&
                   source.Contains(value, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class SalesRecord
    {
        public int row { get; set; }
        public string? customer_name { get; set; }
        public string? customer_id { get; set; }
        public string? item_name { get; set; }
        public string? item_code { get; set; }
        public string? invoice_number { get; set; }
        public string? sales_order { get; set; }
        public string? dispatch { get; set; }
        public double weight { get; set; }
        public double base_rate { get; set; }
        public double gst_percent { get; set; }
        public double tcs_percent { get; set; }
        public double taxable_amount { get; set; }
        public double gst_amount { get; set; }
        public double total { get; set; }
        public double loading_charge { get; set; }
        public double tcs_amount { get; set; }
        public double net_receivable { get; set; }

        public string WeightDisplay => FormatNumber(weight);
        public string BaseRateDisplay => $"Rs. {FormatNumber(base_rate)}";
        public string TaxableAmountDisplay => $"Rs. {FormatNumber(taxable_amount)}";
        public string NetReceivableDisplay => $"Rs. {FormatNumber(net_receivable)}";

        public DateTime DispatchDateForSort
        {
            get
            {
                if (DateTime.TryParseExact(
                        dispatch,
                        "dd-MM-yyyy",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime parsed))
                {
                    return parsed.Date;
                }

                return DateTime.MinValue;
            }
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}

