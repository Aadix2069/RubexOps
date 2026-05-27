using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RubexOps
{
    public partial class EnterPurchaseDataPage : Page
    {
        // =====================================================
        // DATA MODELS
        // =====================================================

        private sealed class ContractOption
        {
            public string VendorName { get; set; } = "";
            public string VendorID { get; set; } = "";
            public string ItemCode { get; set; } = "";
            public string BaseRate { get; set; } = "";
            public string StartDate { get; set; } = "";
            public string EndDate { get; set; } = "";
        }



        // =====================================================
        // FIELDS
        // =====================================================

        private readonly List<ContractOption> contractOptions =
            new List<ContractOption>();

        private ContractOption? selectedContract = null;

        private bool isSelectingVendor;



        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        public EnterPurchaseDataPage()
        {
            InitializeComponent();
            Loaded += EnterPurchaseDataPage_Loaded;
        }



        // =====================================================
        // PAGE LOAD
        // =====================================================

        private async void EnterPurchaseDataPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            await LoadActiveContracts();
            UpdateSearchPlaceholder();
        }



        // =====================================================
        // LOAD ACTIVE CONTRACTS
        // =====================================================

        private async Task LoadActiveContracts()
        {
            try
            {
                string pythonScript = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "backend",
                    "read_purchase_vendors.py"
                );

                if (!File.Exists(pythonScript))
                {
                    MessageBox.Show(
                        "Vendor suggestion backend file not found.\n\n" +
                        pythonScript,
                        "File Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                string output = await RunPythonScript(pythonScript, "");

                List<ContractOption>? contracts =
                    JsonSerializer.Deserialize<List<ContractOption>>(
                        output,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                contractOptions.Clear();

                if (contracts != null)
                    contractOptions.AddRange(contracts);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Contract Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }



        // =====================================================
        // SEARCH PLACEHOLDER VISIBILITY
        // =====================================================

        private void UpdateSearchPlaceholder()
        {
            if (SearchPlaceholder == null) return;

            SearchPlaceholder.Visibility =
                string.IsNullOrEmpty(VendorSearchBox.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }



        // =====================================================
        // VENDOR SEARCH
        // =====================================================

        private void VendorSearchBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            UpdateSearchPlaceholder();

            if (isSelectingVendor)
                return;

            // If user edits search box after a selection, clear autofill
            if (selectedContract != null)
                ClearAutoFill();

            string searchText = VendorSearchBox.Text.Trim();

            if (searchText.Length == 0)
            {
                VendorSuggestionPopup.IsOpen = false;
                return;
            }

            List<ContractOption> matches =
                contractOptions.FindAll(c =>
                    c.VendorName.IndexOf(
                        searchText,
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    c.VendorID.IndexOf(
                        searchText,
                        StringComparison.OrdinalIgnoreCase) >= 0);

            VendorSuggestionList.ItemsSource = matches;
            VendorSuggestionPopup.IsOpen = matches.Count > 0;
        }



        // =====================================================
        // SELECT CONTRACT FROM DROPDOWN
        // =====================================================

        private void VendorSuggestionList_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            ContractOption? contract =
                VendorSuggestionList.SelectedItem as ContractOption;

            if (contract == null)
                return;

            isSelectingVendor = true;

            selectedContract = contract;

            VendorSearchBox.Text =
                $"{contract.VendorName}  —  {contract.VendorID}";

            // Auto-fill locked fields
            VendorNameBox.Text = contract.VendorName;
            VendorIDBox.Text = contract.VendorID;
            ItemCodeBox.Text = contract.ItemCode;
            BaseRateBox.Text = contract.BaseRate;

            // Show contract period badge
            if (!string.IsNullOrWhiteSpace(contract.StartDate) &&
                !string.IsNullOrWhiteSpace(contract.EndDate))
            {
                ContractPeriodText.Text =
                    $"Contract Period:  {contract.StartDate}  →  {contract.EndDate}";
                ContractPeriodBadge.Visibility = Visibility.Visible;
            }

            VendorSuggestionPopup.IsOpen = false;
            isSelectingVendor = false;

            // Move focus to first input field
            InvoiceNumberBox.Focus();

            UpdateSearchPlaceholder();
        }



        // =====================================================
        // CLEAR AUTO-FILL (when user clears search)
        // =====================================================

        private void ClearAutoFill()
        {
            selectedContract = null;

            VendorNameBox.Text = "";
            VendorIDBox.Text = "";
            ItemCodeBox.Text = "";
            BaseRateBox.Text = "";

            ContractPeriodBadge.Visibility = Visibility.Collapsed;
        }



        // =====================================================
        // PURCHASE ORDER DATE CHANGED — contract range hint
        // =====================================================

        private void PurchaseOrderDatePicker_SelectedDateChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            // Live warning: if a contract is selected and date is outside range
            if (selectedContract == null)
                return;

            if (PurchaseOrderDatePicker.SelectedDate == null)
                return;

            if (!TryParseContractDates(
                    selectedContract,
                    out DateTime contractStart,
                    out DateTime contractEnd))
                return;

            DateTime poDate = PurchaseOrderDatePicker.SelectedDate.Value;

            if (poDate < contractStart || poDate > contractEnd)
            {
                ContractPeriodBadge.Background =
                    new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(254, 242, 242));
                ContractPeriodBadge.BorderBrush =
                    new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(252, 165, 165));
                ContractPeriodText.Foreground =
                    new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(185, 28, 28));
                ContractPeriodText.Text =
                    $"⚠  Date outside contract period:  {selectedContract.StartDate}  →  {selectedContract.EndDate}";
            }
            else
            {
                ContractPeriodBadge.Background =
                    new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(240, 253, 254));
                ContractPeriodBadge.BorderBrush =
                    new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(165, 243, 252));
                ContractPeriodText.Foreground =
                    new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(11, 138, 143));
                ContractPeriodText.Text =
                    $"Contract Period:  {selectedContract.StartDate}  →  {selectedContract.EndDate}";
            }
        }



        // =====================================================
        // SAVE PURCHASE
        // =====================================================

        private async void SavePurchase_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                MainGrid.IsEnabled = false;
                MainGrid.Opacity = 0.75;

                if (sender is Button button)
                {
                    button.IsEnabled = false;
                    button.Content = "Saving...";
                }

                if (!ValidateForm())
                    return;

                // ── Collect values ──

                string vendorID =
                    VendorIDBox.Text.Trim();

                string invoiceNumber =
                    InvoiceNumberBox.Text.Trim();

                string purchaseOrderDate =
                    PurchaseOrderDatePicker.SelectedDate?
                    .ToString("dd-MM-yyyy") ?? "";

                string deliveryDate =
                    DeliveryDatePicker.SelectedDate?
                    .ToString("dd-MM-yyyy") ?? "";

                string invoiceWeight =
                    string.IsNullOrWhiteSpace(InvoiceWeightBox.Text)
                        ? ""
                        : decimal.Parse(InvoiceWeightBox.Text.Trim())
                          .ToString();

                string beforeUnloading =
                    decimal.Parse(BeforeUnloadingBox.Text.Trim()).ToString();

                string carrierWeight =
                    decimal.Parse(CarrierWeightBox.Text.Trim()).ToString();

                string noOfBags =
                    string.IsNullOrWhiteSpace(NoOfBagsBox.Text)
                        ? ""
                        : int.Parse(NoOfBagsBox.Text.Trim()).ToString();

                string calculatedDrc =
                    decimal.Parse(
                        CalculatedDrcBox.Text.Trim().Replace("%", ""))
                    .ToString();

                string gst =
                    decimal.Parse(GstBox.Text.Trim()).ToString();

                string tds =
                    decimal.Parse(TdsBox.Text.Trim()).ToString();

                string unloadingCharge =
                    string.IsNullOrWhiteSpace(UnloadingChargeBox.Text)
                        ? ""
                        : decimal.Parse(UnloadingChargeBox.Text.Trim())
                          .ToString();

                // ── Build arguments ──
                // Order must match create_purchase_data.py expected args
                // We pass vendor_id only (NOT vendor_name, item_code, base_rate)
                string arguments =
                    $"\"{vendorID}\" " +
                    $"\"{invoiceNumber}\" " +
                    $"\"{purchaseOrderDate}\" " +
                    $"\"{deliveryDate}\" " +
                    $"\"{invoiceWeight}\" " +
                    $"\"{beforeUnloading}\" " +
                    $"\"{carrierWeight}\" " +
                    $"\"{noOfBags}\" " +
                    $"\"{calculatedDrc}\" " +
                    $"\"{gst}\" " +
                    $"\"{tds}\" " +
                    $"\"{unloadingCharge}\"";

                string pythonScript = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "backend",
                    "create_purchase_data.py"
                );

                if (!File.Exists(pythonScript))
                {
                    MessageBox.Show(
                        "Backend Python file not found.\n\n" + pythonScript,
                        "File Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                string output = await RunPythonScript(pythonScript, arguments);

                if (output.Contains("ERROR"))
                {
                    MessageBox.Show(
                        output,
                        "Backend Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                MessageBox.Show(
                    output,
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                ClearForm();
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

                if (sender is Button button)
                {
                    button.IsEnabled = true;
                    button.Content = "Save Purchase";
                }
            }
        }



        // =====================================================
        // VALIDATION
        // =====================================================

        private bool ValidateForm()
        {
            // ── 1. Contract must be selected ──
            if (selectedContract == null ||
                string.IsNullOrWhiteSpace(VendorIDBox.Text))
            {
                MessageBox.Show(
                    "Please search and select an active contract before saving.",
                    "No Contract Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                VendorSearchBox.Focus();
                return false;
            }

            // ── 2. Re-verify contract is still ACTIVE ──
            // (Python will do the authoritative check; this is a fast client-side guard)
            if (string.IsNullOrWhiteSpace(VendorIDBox.Text))
            {
                MessageBox.Show(
                    "Vendor ID is missing. Please reselect the contract.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                VendorSearchBox.Focus();
                return false;
            }

            // ── 3. Invoice Number ──
            if (string.IsNullOrWhiteSpace(InvoiceNumberBox.Text))
            {
                MessageBox.Show(
                    "Invoice Number is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                InvoiceNumberBox.Focus();
                return false;
            }

            // ── 4. Purchase Order Date ──
            if (PurchaseOrderDatePicker.SelectedDate == null)
            {
                MessageBox.Show(
                    "Purchase Order Date is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                PurchaseOrderDatePicker.Focus();
                return false;
            }

            // ── 5. Purchase Order Date must be within contract period ──
            if (TryParseContractDates(
                    selectedContract,
                    out DateTime contractStart,
                    out DateTime contractEnd))
            {
                DateTime poDate = PurchaseOrderDatePicker.SelectedDate.Value;

                if (poDate < contractStart || poDate > contractEnd)
                {
                    MessageBox.Show(
                        $"Purchase Order Date ({poDate:dd-MM-yyyy}) is outside " +
                        $"the selected contract period.\n\n" +
                        $"Contract valid from {selectedContract.StartDate} " +
                        $"to {selectedContract.EndDate}.",
                        "Contract Date Mismatch",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    PurchaseOrderDatePicker.Focus();
                    return false;
                }
            }

            // ── 6. Delivery Date (optional) must not be before PO Date ──
            if (DeliveryDatePicker.SelectedDate != null)
            {
                if (DeliveryDatePicker.SelectedDate <
                    PurchaseOrderDatePicker.SelectedDate)
                {
                    MessageBox.Show(
                        "Delivery Date cannot be before the Purchase Order Date.",
                        "Date Validation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    DeliveryDatePicker.Focus();
                    return false;
                }
            }

            // ── 7. Before Unloading (required) ──
            if (!ValidatePositiveDecimal(
                    BeforeUnloadingBox, "Before Unloading", required: true))
                return false;

            // ── 8. Carrier Weight (required) ──
            if (!ValidatePositiveDecimal(
                    CarrierWeightBox, "Carrier Weight", required: true))
                return false;

            // ── 9. Invoice Weight (optional) ──
            if (!string.IsNullOrWhiteSpace(InvoiceWeightBox.Text))
            {
                if (!ValidatePositiveDecimal(
                        InvoiceWeightBox, "Invoice Weight", required: false))
                    return false;
            }

            // ── 10. No. of Bags (optional) ──
            if (!string.IsNullOrWhiteSpace(NoOfBagsBox.Text))
            {
                if (!int.TryParse(NoOfBagsBox.Text.Trim(), out int bags))
                {
                    MessageBox.Show(
                        "No. of Bags must be a whole number.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    NoOfBagsBox.Focus();
                    return false;
                }

                if (bags < 0)
                {
                    MessageBox.Show(
                        "No. of Bags cannot be negative.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    NoOfBagsBox.Focus();
                    return false;
                }
            }

            // ── 11. Calculated DRC (required) ──
            if (string.IsNullOrWhiteSpace(CalculatedDrcBox.Text))
            {
                MessageBox.Show(
                    "Calculated DRC (%) is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                CalculatedDrcBox.Focus();
                return false;
            }

            string drcText = CalculatedDrcBox.Text.Trim().Replace("%", "");

            if (!decimal.TryParse(drcText, out decimal drc))
            {
                MessageBox.Show(
                    "Calculated DRC must be a valid number.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                CalculatedDrcBox.Focus();
                return false;
            }

            if (drc < 0 || drc > 100)
            {
                MessageBox.Show(
                    "Calculated DRC must be between 0 and 100.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                CalculatedDrcBox.Focus();
                return false;
            }

            // ── 12. GST (required) ──
            if (string.IsNullOrWhiteSpace(GstBox.Text))
            {
                MessageBox.Show(
                    "GST (%) is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                GstBox.Focus();
                return false;
            }

            if (!decimal.TryParse(GstBox.Text.Trim(), out decimal gst) || gst < 0)
            {
                MessageBox.Show(
                    "GST (%) must be a valid non-negative number.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                GstBox.Focus();
                return false;
            }

            // ── 13. TDS (required) ──
            if (string.IsNullOrWhiteSpace(TdsBox.Text))
            {
                MessageBox.Show(
                    "TDS 194Q (%) is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                TdsBox.Focus();
                return false;
            }

            if (!decimal.TryParse(TdsBox.Text.Trim(), out decimal tds) ||
                tds < 0 || tds > 100)
            {
                MessageBox.Show(
                    "TDS 194Q (%) must be a valid number between 0 and 100.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                TdsBox.Focus();
                return false;
            }

            // ── 14. Unloading Charge (optional) ──
            if (!string.IsNullOrWhiteSpace(UnloadingChargeBox.Text))
            {
                if (!ValidatePositiveDecimal(
                        UnloadingChargeBox, "Unloading Charge", required: false))
                    return false;
            }

            return true;
        }



        // =====================================================
        // DECIMAL VALIDATION HELPER
        // =====================================================

        private bool ValidatePositiveDecimal(
            TextBox textBox,
            string fieldName,
            bool required)
        {
            string text = textBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                if (required)
                {
                    MessageBox.Show(
                        $"{fieldName} is required.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    textBox.Focus();
                    return false;
                }

                return true;
            }

            if (!decimal.TryParse(text, out decimal value))
            {
                MessageBox.Show(
                    $"{fieldName} must be a valid number.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                textBox.Focus();
                return false;
            }

            if (value < 0)
            {
                MessageBox.Show(
                    $"{fieldName} cannot be negative.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                textBox.Focus();
                return false;
            }

            return true;
        }



        // =====================================================
        // CONTRACT DATE PARSING HELPER
        // =====================================================

        private bool TryParseContractDates(
            ContractOption? contract,
            out DateTime start,
            out DateTime end)
        {
            start = DateTime.MinValue;
            end = DateTime.MaxValue;

            if (contract == null)
                return false;

            if (string.IsNullOrWhiteSpace(contract.StartDate) ||
                string.IsNullOrWhiteSpace(contract.EndDate))
                return false;

            bool startOk = DateTime.TryParseExact(
                contract.StartDate,
                new[] { "dd-MM-yyyy", "yyyy-MM-dd", "dd/MM/yyyy" },
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out start);

            bool endOk = DateTime.TryParseExact(
                contract.EndDate,
                new[] { "dd-MM-yyyy", "yyyy-MM-dd", "dd/MM/yyyy" },
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out end);

            return startOk && endOk;
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
                    Arguments = $"\"{pythonScript}\" {arguments}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.Combine(
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

                output = process.StandardOutput.ReadToEnd();
                error = process.StandardError.ReadToEnd();

                process.WaitForExit();
            });

            if (!string.IsNullOrWhiteSpace(error))
                throw new Exception(error);

            return output.Trim();
        }



        // =====================================================
        // CLEAR FORM (button handler)
        // =====================================================

        private void ClearForm_Click(
            object sender,
            RoutedEventArgs e)
        {
            ClearForm();
        }



        // =====================================================
        // CLEAR FORM METHOD
        // =====================================================

        private void ClearForm()
        {
            isSelectingVendor = false;

            VendorSearchBox.Clear();

            ClearAutoFill();

            InvoiceNumberBox.Clear();

            PurchaseOrderDatePicker.SelectedDate = null;
            DeliveryDatePicker.SelectedDate = null;

            InvoiceWeightBox.Clear();
            BeforeUnloadingBox.Clear();
            CarrierWeightBox.Clear();
            NoOfBagsBox.Clear();
            CalculatedDrcBox.Clear();

            // Preserve defaults for GST and TDS
            GstBox.Text = "5";
            TdsBox.Text = "0.1";

            UnloadingChargeBox.Clear();

            VendorSuggestionPopup.IsOpen = false;

            UpdateSearchPlaceholder();

            VendorSearchBox.Focus();
        }
    }
}