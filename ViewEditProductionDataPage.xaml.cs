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
    public partial class ViewEditProductionDataPage : Page
    {
        // =====================================================
        // MODELS
        // =====================================================

        private sealed class ProductionRecordsResponse
        {
            public List<ProductionRecord> records { get; set; } = new();

            public ProductionSummary summary { get; set; } = new();
        }

        private sealed class ProductionSummary
        {
            public double total_production_today { get; set; }

            public double total_output { get; set; }

            public double total_production_loss { get; set; }

            public double average_drc_variance { get; set; }

            public int active_batches { get; set; }

            public string highest_loss_batch { get; set; } = "";
        }



        // =====================================================
        // FIELDS
        // =====================================================

        private List<ProductionRecord> allRecords = new();



        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        public ViewEditProductionDataPage()
        {
            InitializeComponent();

            SortComboBox.SelectedIndex = 0;
            LoadProductionRecords();
        }



        // =====================================================
        // LOAD RECORDS
        // =====================================================

        private void LoadProductionRecords()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                MainGrid.IsEnabled = false;
                MainGrid.Opacity = 0.75;

                string pythonScript = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "backend",
                    "read_production_records.py");

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

                ProductionRecordsResponse response =
                    JsonSerializer.Deserialize<ProductionRecordsResponse>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        })
                    ?? new ProductionRecordsResponse();

                allRecords = response.records ?? new List<ProductionRecord>();

                ApplyFilters();
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

        private void FilterChanged(
            object sender,
            TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void DateFilterChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SortComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void RefreshButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            LoadProductionRecords();
        }



        // =====================================================
        // APPLY FILTERS
        // =====================================================

        private void ApplyFilters()
        {
            if (ProductionDataGrid == null)
            {
                return;
            }

            IEnumerable<ProductionRecord> filtered = allRecords;

            string search = SearchBox?.Text?.Trim() ?? "";
            string supplier = SupplierFilterBox?.Text?.Trim() ?? "";
            string material = MaterialFilterBox?.Text?.Trim() ?? "";
            string product = ProductFilterBox?.Text?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(record =>
                    Contains(record.batch_id, search) ||
                    Contains(record.supplier_name, search) ||
                    Contains(record.supplier_id, search) ||
                    Contains(record.invoice_number, search) ||
                    Contains(record.raw_material, search) ||
                    Contains(record.finished_product, search));
            }

            if (!string.IsNullOrWhiteSpace(supplier))
            {
                filtered = filtered.Where(record =>
                    Contains(record.supplier_name, supplier) ||
                    Contains(record.supplier_id, supplier));
            }

            if (!string.IsNullOrWhiteSpace(material))
            {
                filtered = filtered.Where(record =>
                    Contains(record.raw_material, material) ||
                    Contains(record.raw_material_code, material));
            }

            if (!string.IsNullOrWhiteSpace(product))
            {
                filtered = filtered.Where(record =>
                    Contains(record.finished_product, product) ||
                    Contains(record.finished_product_code, product));
            }

            if (FromDatePicker?.SelectedDate != null)
            {
                filtered = filtered.Where(record =>
                    record.ProductionDateForSort >= FromDatePicker.SelectedDate.Value.Date);
            }

            if (ToDatePicker?.SelectedDate != null)
            {
                filtered = filtered.Where(record =>
                    record.ProductionDateForSort <= ToDatePicker.SelectedDate.Value.Date);
            }

            filtered = ApplySort(filtered);

            List<ProductionRecord> filteredList = filtered.ToList();
            ProductionDataGrid.ItemsSource = filteredList;
            UpdateAnalytics(filteredList);
        }



        // =====================================================
        // SORT
        // =====================================================

        private IEnumerable<ProductionRecord> ApplySort(
            IEnumerable<ProductionRecord> records)
        {
            string sort =
                (SortComboBox?.SelectedItem as ComboBoxItem)
                ?.Content
                ?.ToString() ?? "Production Date Newest";

            return sort switch
            {
                "Production Date Oldest" =>
                    records.OrderBy(record => record.ProductionDateForSort),

                "Batch ID Ascending" =>
                    records.OrderBy(record => record.batch_id),

                "Batch ID Descending" =>
                    records.OrderByDescending(record => record.batch_id),

                "Loss High to Low" =>
                    records.OrderByDescending(record => record.production_loss),

                "DRC Variance High to Low" =>
                    records.OrderByDescending(record => Math.Abs(record.drc_variance)),

                "Output High to Low" =>
                    records.OrderByDescending(record => record.output_weight),

                _ =>
                    records.OrderByDescending(record => record.ProductionDateForSort)
            };
        }



        // =====================================================
        // ANALYTICS
        // =====================================================

        private void UpdateAnalytics(
            List<ProductionRecord> records)
        {
            DateTime today = DateTime.Today;

            double todayOutput =
                records
                    .Where(record => record.ProductionDateForSort.Date == today)
                    .Sum(record => record.output_weight);

            double totalOutput =
                records.Sum(record => record.output_weight);

            double totalLoss =
                records.Sum(record => record.production_loss);

            double averageVariance =
                records.Count > 0
                    ? records.Average(record => record.drc_variance)
                    : 0;

            ProductionRecord highestLoss =
                records
                    .OrderByDescending(record => record.production_loss)
                    .FirstOrDefault();

            TodayOutputText.Text = FormatNumber(todayOutput);
            TotalOutputText.Text = FormatNumber(totalOutput);
            TotalLossText.Text = FormatNumber(totalLoss);
            AverageVarianceText.Text = $"{FormatNumber(averageVariance)}%";
            ActiveBatchesText.Text = records.Count.ToString(CultureInfo.InvariantCulture);
            HighestLossBatchText.Text = highestLoss?.batch_id ?? "-";
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
                    button.DataContext is not ProductionRecord record)
                {
                    return;
                }

                EditProductionRecordWindow window =
                    new EditProductionRecordWindow(record);

                bool? result = window.ShowDialog();

                if (result == true)
                {
                    LoadProductionRecords();
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

        private void ProductionDataGrid_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            // RowDetailsVisibilityMode handles the expandable operational row.
        }



        // =====================================================
        // HELPERS
        // =====================================================

        private static bool Contains(
            string source,
            string value)
        {
            return !string.IsNullOrWhiteSpace(source) &&
                   source.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatNumber(
            double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }



    // =====================================================
    // PRODUCTION RECORD MODEL
    // =====================================================

    public class ProductionRecord
    {
        public int row { get; set; }

        public string batch_id { get; set; } = "";

        public string production_date { get; set; } = "";

        public string supplier_name { get; set; } = "";

        public string supplier_id { get; set; } = "";

        public string invoice_number { get; set; } = "";

        public string raw_material { get; set; } = "";

        public string raw_material_code { get; set; } = "";

        public string finished_product { get; set; } = "";

        public string finished_product_code { get; set; } = "";

        public double quantity_available { get; set; }

        public double input_weight { get; set; }

        public double output_weight { get; set; }

        public double remaining_quantity { get; set; }

        public double production_loss { get; set; }

        public double initial_drc { get; set; }

        public double actual_drc { get; set; }

        public double drc_variance { get; set; }

        public double loss_percent { get; set; }

        public bool is_high_loss { get; set; }

        public bool is_high_drc_variance { get; set; }

        public string InputDisplay => FormatNumber(input_weight);

        public string OutputDisplay => FormatNumber(output_weight);

        public string LossDisplay => FormatNumber(production_loss);

        public string VarianceDisplay => $"{FormatNumber(drc_variance)}%";

        public string AvailabilityDisplay =>
            $"{FormatNumber(quantity_available)} / {FormatNumber(remaining_quantity)}";

        public string DrcDisplay =>
            $"{FormatNumber(initial_drc)}% / {FormatNumber(actual_drc)}%";

        public DateTime ProductionDateForSort
        {
            get
            {
                if (DateTime.TryParseExact(
                        production_date,
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

        private static string FormatNumber(
            double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}

