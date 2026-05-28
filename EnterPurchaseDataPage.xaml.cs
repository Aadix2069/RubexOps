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

        private readonly List<ContractOption> contractOptions = new List<ContractOption>();
        private ContractOption? selectedContract = null;
        private bool isSelectingVendor;

        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        public EnterPurchaseDataPage()
        {
            InitializeComponent();
            Loaded += EnterPurchaseDataPage_Loaded;

            // Global Enter key listener for form submission
            this.PreviewKeyDown += EnterPurchaseDataPage_PreviewKeyDown;
        }

        // =====================================================
        // PAGE LOAD
        // =====================================================

        private async void EnterPurchaseDataPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadActiveContracts();
            UpdateSearchPlaceholder();
        }

        // =====================================================
        // ENTER KEY SUBMIT LOGIC
        // =====================================================

        private void EnterPurchaseDataPage_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Prevent duplicate firing and default ding sound
                e.Handled = true;

                // Submit form pretending the Save button was clicked
                if (SavePurchaseButton.IsEnabled)
                {
                    SavePurchase_Click(SavePurchaseButton, new RoutedEventArgs());
                }
            }
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
                        "Vendor suggestion backend file not found.\n\n" + pythonScript,
                        "File Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string output = await RunPythonScript(pythonScript, "");

                List<ContractOption>? contracts =
                    JsonSerializer.Deserialize<List<ContractOption>>(
                        output,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                contractOptions.Clear();

                if (contracts != null)
                    contractOptions.AddRange(contracts);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Contract Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void VendorSearchBox_TextChanged(object sender, TextChangedEventArgs e)
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

            // Safely search against Name and ID case-insensitively
            List<ContractOption> matches = contractOptions.FindAll(c =>
                (c.VendorName ?? "").IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (c.VendorID ?? "").IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);

            VendorSuggestionList.ItemsSource = matches;
            VendorSuggestionPopup.IsOpen = matches.Count > 0;
        }

        // =====================================================
        // SELECT CONTRACT FROM DROPDOWN
        // =====================================================

        private void VendorSuggestionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VendorSuggestionList.SelectedItem is not ContractOption contract)
                return;

            isSelectingVendor = true;
            selectedContract = contract;

            VendorSearchBox.Text = $"{contract.VendorName}  —  {contract.VendorID}";

            // Auto-fill locked fields
            VendorNameBox.Text = contract.VendorName;
            VendorIDBox.Text = contract.VendorID;
            ItemCodeBox.Text = contract.ItemCode;
            BaseRateBox.Text = contract.BaseRate;

            // Show contract period badge
            if (!string.IsNullOrWhiteSpace(contract.StartDate) &&
                !string.IsNullOrWhiteSpace(contract.EndDate))
            {
                ContractPeriodText.Text = $"Contract Period:  {contract.StartDate}  →  {contract.EndDate}";
                ContractPeriodBadge.Visibility = Visibility.Visible;
            }

            VendorSuggestionPopup.IsOpen = false;

            // Clear selection to ensure SelectionChanged fires if they click the same item again
            VendorSuggestionList.SelectedItem = null;

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

        private void PurchaseOrderDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (selectedContract == null || PurchaseOrderDatePicker.SelectedDate == null)
                return;

            if (!TryParseContractDates(selectedContract, out DateTime contractStart, out DateTime contractEnd))
                return;

            DateTime poDate = PurchaseOrderDatePicker.SelectedDate.Value;

            if (poDate < contractStart || poDate > contractEnd)
            {
                ContractPeriodBadge.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 242, 242));
                ContractPeriodBadge.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(252, 165, 165));
                ContractPeriodText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(185, 28, 28));
                ContractPeriodText.Text = $"⚠  Date outside contract period:  {selectedContract.StartDate}  →  {selectedContract.EndDate}";
            }
            else
            {
                ContractPeriodBadge.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 253, 254));
                ContractPeriodBadge.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(165, 243, 252));
                ContractPeriodText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(11, 138, 143));
                ContractPeriodText.Text = $"Contract Period:  {selectedContract.StartDate}  →  {selectedContract.EndDate}";
            }
        }

        // =====================================================
        // SAVE PURCHASE
        // =====================================================

        private async void SavePurchase_Click(object sender, RoutedEventArgs e)
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

                string vendorID = VendorIDBox.Text.Trim();
                string invoiceNumber = InvoiceNumberBox.Text.Trim();
                string purchaseOrderDate = PurchaseOrderDatePicker.SelectedDate?.ToString("dd-MM-yyyy") ?? "";
                string deliveryDate = DeliveryDatePicker.SelectedDate?.ToString("dd-MM-yyyy") ?? "";
                string invoiceWeight = string.IsNullOrWhiteSpace(InvoiceWeightBox.Text) ? "" : decimal.Parse(InvoiceWeightBox.Text.Trim()).ToString();
                string beforeUnloading = decimal.Parse(BeforeUnloadingBox.Text.Trim()).ToString();
                string carrierWeight = decimal.Parse(CarrierWeightBox.Text.Trim()).ToString();
                string noOfBags = string.IsNullOrWhiteSpace(NoOfBagsBox.Text) ? "" : int.Parse(NoOfBagsBox.Text.Trim()).ToString();
                string calculatedDrc = decimal.Parse(CalculatedDrcBox.Text.Trim().Replace("%", "")).ToString();
                string gst = decimal.Parse(GstBox.Text.Trim()).ToString();
                string tds = decimal.Parse(TdsBox.Text.Trim()).ToString();
                string unloadingCharge = string.IsNullOrWhiteSpace(UnloadingChargeBox.Text) ? "" : decimal.Parse(UnloadingChargeBox.Text.Trim()).ToString();

                string SafeArg(string val) => string.IsNullOrWhiteSpace(val) ? "\"\"" : $"\"{val}\"";

                string arguments =
                    $"{SafeArg(vendorID)} " +
                    $"{SafeArg(invoiceNumber)} " +
                    $"{SafeArg(purchaseOrderDate)} " +
                    $"{SafeArg(deliveryDate)} " +
                    $"{SafeArg(invoiceWeight)} " +
                    $"{SafeArg(beforeUnloading)} " +
                    $"{SafeArg(carrierWeight)} " +
                    $"{SafeArg(noOfBags)} " +
                    $"{SafeArg(calculatedDrc)} " +
                    $"{SafeArg(gst)} " +
                    $"{SafeArg(tds)} " +
                    $"{SafeArg(unloadingCharge)}";

                string pythonScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backend", "create_purchase_data.py");

                if (!File.Exists(pythonScript))
                {
                    MessageBox.Show("Backend Python file not found.\n\n" + pythonScript, "File Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string output = await RunPythonScript(pythonScript, arguments);

                if (output.Contains("ERROR"))
                {
                    MessageBox.Show(output, "Backend Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                MessageBox.Show(output, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
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

                if (SavePurchaseButton != null)
                {
                    SavePurchaseButton.IsEnabled = true;
                    SavePurchaseButton.Content = "Save Purchase";
                }
            }
        }

        // =====================================================
        // VALIDATION
        // =====================================================

        private bool ValidateForm()
        {
            if (selectedContract == null || string.IsNullOrWhiteSpace(VendorIDBox.Text))
            {
                MessageBox.Show("Please search and select an active contract before saving.", "No Contract Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                VendorSearchBox.Focus(); return false;
            }

            if (string.IsNullOrWhiteSpace(InvoiceNumberBox.Text))
            {
                MessageBox.Show("Invoice Number is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                InvoiceNumberBox.Focus(); return false;
            }

            if (PurchaseOrderDatePicker.SelectedDate == null)
            {
                MessageBox.Show("Purchase Order Date is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                PurchaseOrderDatePicker.Focus(); return false;
            }

            if (TryParseContractDates(selectedContract, out DateTime contractStart, out DateTime contractEnd))
            {
                DateTime poDate = PurchaseOrderDatePicker.SelectedDate.Value;
                if (poDate < contractStart || poDate > contractEnd)
                {
                    MessageBox.Show($"Purchase Order Date ({poDate:dd-MM-yyyy}) is outside the contract period.", "Contract Date Mismatch", MessageBoxButton.OK, MessageBoxImage.Warning);
                    PurchaseOrderDatePicker.Focus(); return false;
                }
            }

            if (DeliveryDatePicker.SelectedDate != null && DeliveryDatePicker.SelectedDate < PurchaseOrderDatePicker.SelectedDate)
            {
                MessageBox.Show("Delivery Date cannot be before the Purchase Order Date.", "Date Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                DeliveryDatePicker.Focus(); return false;
            }

            if (!ValidatePositiveDecimal(BeforeUnloadingBox, "Before Unloading", required: true)) return false;
            if (!ValidatePositiveDecimal(CarrierWeightBox, "Carrier Weight", required: true)) return false;

            // CARRIER WEIGHT VALIDATION RULE
            decimal beforeUnloading = decimal.Parse(BeforeUnloadingBox.Text.Trim());
            decimal carrierWeight = decimal.Parse(CarrierWeightBox.Text.Trim());

            if (carrierWeight >= beforeUnloading)
            {
                MessageBox.Show("Carrier Weight must be strictly less than Before Unloading weight.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                CarrierWeightBox.Focus(); return false;
            }

            if (!string.IsNullOrWhiteSpace(InvoiceWeightBox.Text) && !ValidatePositiveDecimal(InvoiceWeightBox, "Invoice Weight", required: false)) return false;

            if (!string.IsNullOrWhiteSpace(NoOfBagsBox.Text))
            {
                if (!int.TryParse(NoOfBagsBox.Text.Trim(), out int bags) || bags < 0)
                {
                    MessageBox.Show("No. of Bags must be a valid non-negative whole number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    NoOfBagsBox.Focus(); return false;
                }
            }

            if (string.IsNullOrWhiteSpace(CalculatedDrcBox.Text) || !decimal.TryParse(CalculatedDrcBox.Text.Trim().Replace("%", ""), out decimal drc) || drc < 0 || drc > 100)
            {
                MessageBox.Show("Calculated DRC must be a valid percentage between 0 and 100.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                CalculatedDrcBox.Focus(); return false;
            }

            if (string.IsNullOrWhiteSpace(GstBox.Text) || !decimal.TryParse(GstBox.Text.Trim(), out decimal gst) || gst < 0 || gst > 100)
            {
                MessageBox.Show("GST (%) must be a valid non-negative percentage.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                GstBox.Focus(); return false;
            }

            if (string.IsNullOrWhiteSpace(TdsBox.Text) || !decimal.TryParse(TdsBox.Text.Trim(), out decimal tds) || tds < 0 || tds > 100)
            {
                MessageBox.Show("TDS 194Q (%) must be a valid percentage between 0 and 100.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TdsBox.Focus(); return false;
            }

            if (!string.IsNullOrWhiteSpace(UnloadingChargeBox.Text) && !ValidatePositiveDecimal(UnloadingChargeBox, "Unloading Charge", required: false)) return false;

            return true;
        }

        // =====================================================
        // DECIMAL VALIDATION HELPER
        // =====================================================

        private bool ValidatePositiveDecimal(TextBox textBox, string fieldName, bool required)
        {
            string text = textBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                if (required) { MessageBox.Show($"{fieldName} is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning); textBox.Focus(); return false; }
                return true;
            }

            if (!decimal.TryParse(text, out decimal value) || value < 0)
            {
                MessageBox.Show($"{fieldName} must be a valid non-negative number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                textBox.Focus(); return false;
            }
            return true;
        }

        // =====================================================
        // CONTRACT DATE PARSING HELPER
        // =====================================================

        private bool TryParseContractDates(ContractOption? contract, out DateTime start, out DateTime end)
        {
            start = DateTime.MinValue; end = DateTime.MaxValue;
            if (contract == null || string.IsNullOrWhiteSpace(contract.StartDate) || string.IsNullOrWhiteSpace(contract.EndDate)) return false;
            bool startOk = DateTime.TryParseExact(contract.StartDate, new[] { "dd-MM-yyyy", "yyyy-MM-dd", "dd/MM/yyyy" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out start);
            bool endOk = DateTime.TryParseExact(contract.EndDate, new[] { "dd-MM-yyyy", "yyyy-MM-dd", "dd/MM/yyyy" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out end);
            return startOk && endOk;
        }

        // =====================================================
        // PYTHON RUNNER
        // =====================================================

        private async Task<string> RunPythonScript(string pythonScript, string arguments)
        {
            string pythonExe = "python";
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{pythonScript}\" {arguments}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backend")
            };

            string output = "";
            string error = "";

            await Task.Run(() =>
            {
                using Process process = Process.Start(start) ?? throw new Exception("Failed to start backend process.");
                output = process.StandardOutput.ReadToEnd();
                error = process.StandardError.ReadToEnd();
                process.WaitForExit();
            });

            if (!string.IsNullOrWhiteSpace(error)) throw new Exception(error);
            return output.Trim();
        }

        // =====================================================
        // CLEAR FORM
        // =====================================================

        private void ClearForm_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

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
            GstBox.Text = "5";
            TdsBox.Text = "0.1";
            UnloadingChargeBox.Clear();
            VendorSuggestionPopup.IsOpen = false;
            UpdateSearchPlaceholder();
            VendorSearchBox.Focus();
        }
    }
}