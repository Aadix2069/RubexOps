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
    // =====================================================
    // DATA MODEL
    // =====================================================

    public sealed class PurchaseDataRecord
    {
        public int RowNumber { get; set; }

        public string VendorName { get; set; } = "";

        public string VendorID { get; set; } = "";

        public string ItemName { get; set; } = "";

        public string ItemCode { get; set; } = "";

        public string InvoiceNumber { get; set; } = "";

        public string PurchaseOrderDate { get; set; } = "";

        public string DeliveryDate { get; set; } = "";

        public decimal? InvoiceWeight { get; set; }

        public decimal? BeforeUnloading { get; set; }

        public decimal? CarrierWeight { get; set; }

        public decimal? ReceivedWeight { get; set; }

        public int? NoOfBags { get; set; }

        public decimal? NetWeight { get; set; }

        public decimal? CalculatedDrc { get; set; }

        public decimal? DrcWeight { get; set; }

        public decimal? BaseRate { get; set; }

        public decimal? AdjustedRate { get; set; }

        public decimal? GstPercent { get; set; }

        public decimal? Tds194QPercent { get; set; }

        public decimal? TaxableAmount { get; set; }

        public decimal? UnloadingCharge { get; set; }

        public decimal? GstAmount { get; set; }

        public decimal? GrossAmount { get; set; }

        public decimal? TdsAmount { get; set; }

        public decimal? NetPayable { get; set; }
    }



    public partial class ViewEditPurchaseDataPage : Page
    {
        // =====================================================
        // FIELDS
        // =====================================================

        private readonly List<PurchaseDataRecord> allPurchaseData =
            new List<PurchaseDataRecord>();

        private readonly List<PurchaseDataRecord> filteredPurchaseData =
            new List<PurchaseDataRecord>();



        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        public ViewEditPurchaseDataPage()
        {
            InitializeComponent();

            Loaded += ViewEditPurchaseDataPage_Loaded;
        }



        // =====================================================
        // PAGE LOAD
        // =====================================================

        private async void ViewEditPurchaseDataPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            SetDateFilterState();

            await LoadPurchaseData();
        }



        // =====================================================
        // LOAD PURCHASE DATA
        // =====================================================

        private async Task LoadPurchaseData()
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
                    "read_purchase_data.py"
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
                        pythonScript,
                        "");

                if (output.Contains("ERROR"))
                {
                    MessageBox.Show(
                        output,
                        "Backend Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return;
                }

                List<PurchaseDataRecord> rows =
                    JsonSerializer.Deserialize<List<PurchaseDataRecord>>(
                        output,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                allPurchaseData.Clear();

                if (rows != null)
                {
                    allPurchaseData.AddRange(rows);
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



        // =====================================================
        // APPLY SEARCH FILTER SORT
        // =====================================================

        private void ApplyView()
        {
            if (PurchaseDataGrid == null)
            {
                return;
            }

            IEnumerable<PurchaseDataRecord> query =
                allPurchaseData;

            string searchText =
                SearchBox.Text.Trim();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                query =
                    query.Where(row =>
                        row.VendorName.IndexOf(
                            searchText,
                            StringComparison.OrdinalIgnoreCase) >= 0 ||
                        row.VendorID.IndexOf(
                            searchText,
                            StringComparison.OrdinalIgnoreCase) >= 0);
            }

            query =
                ApplyDateFilter(query);

            query =
                ApplySort(query);

            filteredPurchaseData.Clear();

            filteredPurchaseData.AddRange(query);

            PurchaseDataGrid.ItemsSource = null;

            PurchaseDataGrid.ItemsSource =
                filteredPurchaseData;

            ResultCountText.Text =
                $"{filteredPurchaseData.Count} record(s)";

            if (filteredPurchaseData.Count > 0)
            {
                PurchaseDataGrid.SelectedIndex = 0;
            }
            else
            {
                ClearSelectedDetails();
            }
        }



        // =====================================================
        // DATE FILTER
        // =====================================================

        private IEnumerable<PurchaseDataRecord> ApplyDateFilter(
            IEnumerable<PurchaseDataRecord> query)
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
                        row.PurchaseOrderDate,
                        out DateTime poDate) &&
                    poDate.Date == selectedDate);
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
                        row.PurchaseOrderDate,
                        out DateTime poDate) &&
                    poDate.Date >= fromDate &&
                    poDate.Date <= toDate);
            }

            return query;
        }



        // =====================================================
        // SORT
        // =====================================================

        private IEnumerable<PurchaseDataRecord> ApplySort(
            IEnumerable<PurchaseDataRecord> query)
        {
            string sort =
                GetSelectedTag(SortComboBox);

            switch (sort)
            {
                case "NameDesc":
                    return query
                        .OrderByDescending(row => row.VendorName)
                        .ThenByDescending(row => row.PurchaseOrderDate);

                case "NetPayableAsc":
                    return query
                        .OrderBy(row => row.NetPayable ?? 0)
                        .ThenBy(row => row.VendorName);

                case "NetPayableDesc":
                    return query
                        .OrderByDescending(row => row.NetPayable ?? 0)
                        .ThenBy(row => row.VendorName);

                case "DrcAsc":
                    return query
                        .OrderBy(row => row.CalculatedDrc ?? 0)
                        .ThenBy(row => row.VendorName);

                case "DrcDesc":
                    return query
                        .OrderByDescending(row => row.CalculatedDrc ?? 0)
                        .ThenBy(row => row.VendorName);

                case "NameAsc":
                default:
                    return query
                        .OrderBy(row => row.VendorName)
                        .ThenBy(row => row.PurchaseOrderDate);
            }
        }



        // =====================================================
        // SELECTED DETAILS
        // =====================================================

        private void PurchaseDataGrid_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (PurchaseDataGrid.SelectedItem
                is PurchaseDataRecord selectedRow)
            {
                ShowSelectedDetails(selectedRow);
            }
        }



        private void ShowSelectedDetails(
            PurchaseDataRecord row)
        {
            SelectedVendorNameText.Text =
                $"{row.VendorName} ({row.VendorID})";

            SelectedInvoiceText.Text =
                $"Invoice {row.InvoiceNumber} | Purchase Order {row.PurchaseOrderDate}";

            DetailDeliveryDateText.Text =
                SafeText(row.DeliveryDate);

            DetailNetWeightText.Text =
                FormatDecimal(row.NetWeight);

            DetailDrcText.Text =
                row.CalculatedDrc.HasValue
                    ? $"{FormatDecimal(row.CalculatedDrc)}%"
                    : "-";

            DetailTaxableText.Text =
                FormatDecimal(row.TaxableAmount);

            DetailNetPayableText.Text =
                FormatDecimal(row.NetPayable);
        }



        private void ClearSelectedDetails()
        {
            SelectedVendorNameText.Text =
                "No purchase data found";

            SelectedInvoiceText.Text =
                "Change the search or date filters to view records.";

            DetailDeliveryDateText.Text = "-";

            DetailNetWeightText.Text = "-";

            DetailDrcText.Text = "-";

            DetailTaxableText.Text = "-";

            DetailNetPayableText.Text = "-";
        }



        // =====================================================
        // EDIT PURCHASE
        // =====================================================

        private async void EditPurchase_Click(
            object sender,
            RoutedEventArgs e)
        {
            Button button =
                sender as Button;

            if (button == null)
            {
                return;
            }

            PurchaseDataRecord selectedRow =
                button.Tag as PurchaseDataRecord;

            if (selectedRow == null)
            {
                return;
            }

            EditPurchaseDataWindow editWindow =
                new EditPurchaseDataWindow(
                    selectedRow)
                {
                    Owner =
                        Window.GetWindow(this)
                };

            bool? result =
                editWindow.ShowDialog();

            if (result == true)
            {
                await LoadPurchaseData();
            }
        }



        // =====================================================
        // FILTER EVENTS
        // =====================================================

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
            await LoadPurchaseData();
        }



        // =====================================================
        // DATE UI STATE
        // =====================================================

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



        // =====================================================
        // HELPERS
        // =====================================================

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



        private string SafeText(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "-"
                : value;
        }



        // =====================================================
        // PYTHON RUNNER
        // =====================================================

        private async Task<string> RunPythonScript(
            string pythonScript,
            string arguments)
        {
            string pythonExe = "python";

            ProcessStartInfo start =
                new ProcessStartInfo
                {
                    FileName = pythonExe,

                    Arguments =
                        $"\"{pythonScript}\" {arguments}",

                    UseShellExecute = false,

                    RedirectStandardOutput = true,

                    RedirectStandardError = true,

                    CreateNoWindow = true,

                    WorkingDirectory =
                        Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "backend")
                };

            string output = "";

            string error = "";

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
            });

            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new Exception(error);
            }

            return output.Trim();
        }
    }
}