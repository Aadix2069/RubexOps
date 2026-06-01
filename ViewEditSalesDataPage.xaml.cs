using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RubexOps
{
    public sealed class SalesDataRecord
    {
        public int RowNumber { get; set; }

        public string CustomerName { get; set; } = "";

        public string CustomerID { get; set; } = "";

        public string ContractID { get; set; } = "";

        public string contract_id
        {
            get => ContractID;
            set => ContractID = value ?? "";
        }

        public string ItemName { get; set; } = "";

        public string ItemCode { get; set; } = "";

        public string InvoiceNumber { get; set; } = "";

        public string SalesOrderDate { get; set; } = "";

        public string DispatchDate { get; set; } = "";

        public decimal? Weight { get; set; }

        public decimal? BaseRate { get; set; }

        public decimal? GstPercent { get; set; }

        public decimal? Tcs194QPercent { get; set; }

        public decimal? TaxableAmount { get; set; }

        public decimal? LoadingCharge { get; set; }

        public decimal? GstAmount { get; set; }

        public decimal? GrossAmount { get; set; }

        public decimal? TcsAmount { get; set; }

        public decimal? NetReceivable { get; set; }
    }

    public partial class ViewEditSalesDataPage : Page
    {
        private readonly List<SalesDataRecord> allSalesData =
            new List<SalesDataRecord>();

        private readonly List<SalesDataRecord> filteredSalesData =
            new List<SalesDataRecord>();

        public ViewEditSalesDataPage()
        {
            InitializeComponent();

            Loaded += ViewEditSalesDataPage_Loaded;
        }

        private async void ViewEditSalesDataPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            SetDateFilterState();

            await LoadSalesData();
        }

        private async Task LoadSalesData()
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
                    "read_sales_data.py"
                );

                if (!File.Exists(pythonScript))
                {
                    MessageBox.Show(
                        "Backend Python file not found.\n\n" +
                        pythonScript,
                        "File Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return;
                }

                string output =
                    await RunPythonScript(
                        pythonScript);

                if (output.TrimStart().StartsWith(
                        "ERROR",
                        StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        output,
                        "Backend Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return;
                }

                List<SalesDataRecord>? rows =
                    JsonSerializer.Deserialize<List<SalesDataRecord>>(
                        output,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                allSalesData.Clear();

                if (rows != null)
                {
                    allSalesData.AddRange(rows);
                }

                ApplyView();
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

        private void ApplyView()
        {
            if (SalesDataGrid == null)
            {
                return;
            }

            IEnumerable<SalesDataRecord> query =
                allSalesData;

            string searchText =
                SearchBox.Text.Trim();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                searchText = searchText.Trim();

                query =
                    query.Where(row =>
                        (!string.IsNullOrWhiteSpace(row.CustomerName) &&
                         row.CustomerName.Trim().StartsWith(
                             searchText,
                             StringComparison.OrdinalIgnoreCase)) ||

                        (!string.IsNullOrWhiteSpace(row.CustomerID) &&
                         row.CustomerID.Trim().StartsWith(
                             searchText,
                             StringComparison.OrdinalIgnoreCase)) ||

                        (!string.IsNullOrWhiteSpace(row.ContractID) &&
                         row.ContractID.Trim().StartsWith(
                             searchText,
                             StringComparison.OrdinalIgnoreCase)) ||

                        (!string.IsNullOrWhiteSpace(row.InvoiceNumber) &&
                         row.InvoiceNumber.Trim().StartsWith(
                             searchText,
                             StringComparison.OrdinalIgnoreCase)));
            }

            query =
                ApplyDateFilter(query);

            query =
                ApplySort(query);

            filteredSalesData.Clear();

            filteredSalesData.AddRange(query);

            SalesDataGrid.ItemsSource = null;

            SalesDataGrid.ItemsSource =
                filteredSalesData;

            ResultCountText.Text =
                $"{filteredSalesData.Count} record(s)";

            if (filteredSalesData.Count > 0)
            {
                SalesDataGrid.SelectedIndex = 0;
            }
            else
            {
                ClearSelectedDetails();
            }
        }

        private IEnumerable<SalesDataRecord> ApplyDateFilter(
            IEnumerable<SalesDataRecord> query)
        {
            string mode =
                GetSelectedTag(DateModeComboBox);

            if (mode == "AllTime")
            {
                return query;
            }

            if (mode == "ParticularDay")
            {
                if (FromDatePicker.SelectedDate == null)
                {
                    return query;
                }

                DateTime selectedDate =
                    FromDatePicker.SelectedDate.Value.Date;

                return query.Where(row =>
                    TryParseDate(
                        row.SalesOrderDate,
                        out DateTime soDate) &&
                    soDate.Date == selectedDate);
            }

            if (mode == "DateRange")
            {
                if (FromDatePicker.SelectedDate == null ||
                    ToDatePicker.SelectedDate == null)
                {
                    return query;
                }

                DateTime fromDate =
                    FromDatePicker.SelectedDate.Value.Date;

                DateTime toDate =
                    ToDatePicker.SelectedDate.Value.Date;

                if (fromDate > toDate)
                {
                    MessageBox.Show(
                        "From Date must be before or equal to To Date.",
                        "Date Filter",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return query;
                }

                return query.Where(row =>
                    TryParseDate(
                        row.SalesOrderDate,
                        out DateTime soDate) &&
                    soDate.Date >= fromDate &&
                    soDate.Date <= toDate);
            }

            return query;
        }

        private IEnumerable<SalesDataRecord> ApplySort(
            IEnumerable<SalesDataRecord> query)
        {
            string sort =
                GetSelectedTag(SortComboBox);

            switch (sort)
            {
                case "NameDesc":
                    return query
                        .OrderByDescending(row => row.CustomerName)
                        .ThenByDescending(row => row.SalesOrderDate);

                case "NetReceivableAsc":
                    return query
                        .OrderBy(row => row.NetReceivable ?? 0)
                        .ThenBy(row => row.CustomerName);

                case "NetReceivableDesc":
                    return query
                        .OrderByDescending(row => row.NetReceivable ?? 0)
                        .ThenBy(row => row.CustomerName);

                case "WeightAsc":
                    return query
                        .OrderBy(row => row.Weight ?? 0)
                        .ThenBy(row => row.CustomerName);

                case "WeightDesc":
                    return query
                        .OrderByDescending(row => row.Weight ?? 0)
                        .ThenBy(row => row.CustomerName);

                case "NameAsc":
                default:
                    return query
                        .OrderBy(row => row.CustomerName)
                        .ThenBy(row => row.SalesOrderDate);
            }
        }

        private void SalesDataGrid_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (SalesDataGrid.SelectedItem
                is SalesDataRecord selectedRow)
            {
                ShowSelectedDetails(selectedRow);
            }
        }

        private void ShowSelectedDetails(
            SalesDataRecord row)
        {
            SelectedCustomerNameText.Text =
                $"{row.CustomerName} ({row.CustomerID})";

            string contractPart =
                string.IsNullOrWhiteSpace(row.ContractID)
                    ? ""
                    : $" | Contract {row.ContractID}";

            SelectedInvoiceText.Text =
                $"Invoice {row.InvoiceNumber}{contractPart} | Sales Order {row.SalesOrderDate}";

            DetailDispatchDateText.Text =
                SafeText(row.DispatchDate);

            DetailWeightText.Text =
                FormatWeight(row.Weight);

            DetailLoadingChargeText.Text =
                FormatCurrency(row.LoadingCharge);

            DetailTaxableText.Text =
                FormatCurrency(row.TaxableAmount);

            DetailNetReceivableText.Text =
                FormatCurrency(row.NetReceivable);
        }

        private void ClearSelectedDetails()
        {
            SelectedCustomerNameText.Text =
                "No sales data found";

            SelectedInvoiceText.Text =
                "Change the search or date filters to view records.";

            DetailDispatchDateText.Text = "-";

            DetailWeightText.Text = "-";

            DetailLoadingChargeText.Text = "-";

            DetailTaxableText.Text = "-";

            DetailNetReceivableText.Text = "-";
        }

        private async void EditSales_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            if (button.Tag is not SalesDataRecord selectedRow)
            {
                return;
            }

            EditSalesDataWindow editWindow =
                new EditSalesDataWindow(
                    selectedRow)
                {
                    Owner =
                        Window.GetWindow(this)
                };

            bool? result =
                editWindow.ShowDialog();

            if (result == true)
            {
                await LoadSalesData();
            }
        }

        private void SearchBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            ApplyView();
        }

        private void DateModeComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            SetDateFilterState();

            ApplyView();
        }

        private void DatePicker_SelectedDateChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            ApplyView();
        }

        private void SortComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            ApplyView();
        }

        private void ClearFilters_Click(
            object sender,
            RoutedEventArgs e)
        {
            SearchBox.Clear();

            DateModeComboBox.SelectedIndex = 0;

            FromDatePicker.SelectedDate = null;

            ToDatePicker.SelectedDate = null;

            SortComboBox.SelectedIndex = 0;

            ApplyView();
        }

        private async void Refresh_Click(
            object sender,
            RoutedEventArgs e)
        {
            await LoadSalesData();
        }

        private void SetDateFilterState()
        {
            if (DateModeComboBox == null ||
                FromDatePicker == null ||
                ToDatePicker == null ||
                FromDateLabel == null ||
                ToDateLabel == null)
            {
                return;
            }

            string mode =
                GetSelectedTag(DateModeComboBox);

            bool dateRange =
                mode == "DateRange";

            bool particularDay =
                mode == "ParticularDay";

            FromDateLabel.Text =
                particularDay ? "Date" : "From Date";

            ToDateLabel.Text =
                "To Date";

            FromDatePicker.IsEnabled =
                dateRange || particularDay;

            ToDatePicker.IsEnabled =
                dateRange;

            if (mode == "AllTime")
            {
                FromDatePicker.SelectedDate = null;

                ToDatePicker.SelectedDate = null;
            }

            if (particularDay)
            {
                ToDatePicker.SelectedDate = null;
            }
        }

        private string GetSelectedTag(
            ComboBox comboBox)
        {
            if (comboBox.SelectedItem
                is ComboBoxItem selectedItem &&
                selectedItem.Tag != null)
            {
                return selectedItem.Tag.ToString() ?? "";
            }

            return "";
        }

        private bool TryParseDate(
            string text,
            out DateTime date)
        {
            return DateTime.TryParseExact(
                text,
                "dd-MM-yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date);
        }

        private string FormatDecimal(
            decimal? value)
        {
            return value.HasValue
                ? value.Value.ToString("0.##", CultureInfo.InvariantCulture)
                : "-";
        }

        private string FormatWeight(
            decimal? value)
        {
            return value.HasValue
                ? $"{value.Value.ToString("#,##0.##", CultureInfo.InvariantCulture)} Kg"
                : "-";
        }

        private string FormatCurrency(
            decimal? value)
        {
            return value.HasValue
                ? $"₹ {value.Value.ToString("#,##0.##", CultureInfo.InvariantCulture)}"
                : "-";
        }

        private string SafeText(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "-"
                : value;
        }

        private async Task<string> RunPythonScript(
            string pythonScript,
            params string[] arguments)
        {
            ProcessStartInfo start =
                new()
                {
                    FileName = "python",

                    UseShellExecute = false,

                    RedirectStandardOutput = true,

                    RedirectStandardError = true,

                    CreateNoWindow = true,

                    WorkingDirectory =
                        Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "backend")
                };

            start.ArgumentList.Add(pythonScript);

            foreach (string argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            string output = "";

            string error = "";

            int exitCode = 0;

            await Task.Run(() =>
            {
                using Process process =
                    Process.Start(start)
                    ?? throw new Exception(
                        "Failed to start backend process.");

                output =
                    process.StandardOutput.ReadToEnd();

                error =
                    process.StandardError.ReadToEnd();

                process.WaitForExit();

                exitCode =
                    process.ExitCode;
            });

            if (exitCode != 0)
            {
                string message =
                    !string.IsNullOrWhiteSpace(error)
                        ? error.Trim()
                        : output.Trim();

                if (string.IsNullOrWhiteSpace(message))
                {
                    message =
                        $"Backend process failed with exit code {exitCode}.";
                }

                throw new Exception(message);
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new Exception(error.Trim());
            }

            return output.Trim();
        }
    }
}