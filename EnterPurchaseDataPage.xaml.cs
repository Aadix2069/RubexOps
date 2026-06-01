using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
        private const string DefaultItemCode = "NRFC";

        private sealed class ContractOption
        {
            public string VendorName { get; set; } = "";
            public string VendorID { get; set; } = "";
            public string ContractID { get; set; } = "";
            public string ItemCode { get; set; } = "";
            public string BaseRate { get; set; } = "";
            public string StartDate { get; set; } = "";
            public string EndDate { get; set; } = "";
        }

        private readonly List<ContractOption> contractOptions =
            new List<ContractOption>();

        private ContractOption? selectedContract = null;

        private bool isSelectingVendor;
        private bool suppressSuggestionCommit;
        private int suggestionIndex = -1;

        public EnterPurchaseDataPage()
        {
            InitializeComponent();

            Loaded += EnterPurchaseDataPage_Loaded;

            PreviewKeyDown += EnterPurchaseDataPage_PreviewKeyDown;
        }

        private async void EnterPurchaseDataPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            await LoadActiveContracts();

            UpdateSearchPlaceholder();
        }

        private void EnterPurchaseDataPage_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (VendorSuggestionPopup.IsOpen &&
                VendorSuggestionList.Items.Count > 0)
            {
                if (e.Key == Key.Down)
                {
                    MoveSuggestionSelection(1);
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.Up)
                {
                    MoveSuggestionSelection(-1);
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.Enter)
                {
                    CommitSelectedSuggestion();
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.Escape)
                {
                    VendorSuggestionPopup.IsOpen = false;
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.Enter)
            {
                e.Handled = true;

                if (SavePurchaseButton.IsEnabled)
                {
                    SavePurchase_Click(
                        SavePurchaseButton,
                        new RoutedEventArgs());
                }
            }
        }

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
                        "File Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return;
                }

                string output =
                    await RunPythonScript(pythonScript);

                List<ContractOption>? contracts =
                    JsonSerializer.Deserialize<List<ContractOption>>(
                        output,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                contractOptions.Clear();

                if (contracts != null)
                {
                    contractOptions.AddRange(contracts);
                }
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

        private void UpdateSearchPlaceholder()
        {
            if (SearchPlaceholder == null)
            {
                return;
            }

            SearchPlaceholder.Visibility =
                string.IsNullOrEmpty(VendorSearchBox.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void VendorSearchBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            UpdateSearchPlaceholder();

            if (isSelectingVendor)
            {
                return;
            }

            if (selectedContract != null)
            {
                ClearAutoFill();
            }

            string searchText =
                VendorSearchBox.Text.Trim();

            if (searchText.Length == 0)
            {
                VendorSuggestionPopup.IsOpen = false;
                VendorSuggestionList.ItemsSource = null;
                suggestionIndex = -1;
                return;
            }

            List<ContractOption> matches =
     contractOptions.FindAll(c =>

         (!string.IsNullOrWhiteSpace(c.VendorName) &&
          c.VendorName.Trim().StartsWith(
              searchText,
              StringComparison.OrdinalIgnoreCase))

         ||

         (!string.IsNullOrWhiteSpace(c.VendorID) &&
          c.VendorID.Trim().StartsWith(
              searchText,
              StringComparison.OrdinalIgnoreCase))

         ||

         (!string.IsNullOrWhiteSpace(c.ContractID) &&
          c.ContractID.Trim().StartsWith(
              searchText,
              StringComparison.OrdinalIgnoreCase)));

            VendorSuggestionList.ItemsSource =
                matches;

            if (matches.Count > 0)
            {
                suggestionIndex = 0;

                suppressSuggestionCommit = true;
                try
                {
                    VendorSuggestionList.SelectedIndex = suggestionIndex;

                    if (VendorSuggestionList.SelectedItem != null)
                    {
                        VendorSuggestionList.ScrollIntoView(
                            VendorSuggestionList.SelectedItem);
                    }
                }
                finally
                {
                    suppressSuggestionCommit = false;
                }
            }
            else
            {
                suggestionIndex = -1;
                VendorSuggestionList.SelectedItem = null;
            }

            VendorSuggestionPopup.IsOpen =
                matches.Count > 0;
        }

        // Fired when the user explicitly clicks a suggestion with the mouse
        private void VendorSuggestionItem_PreviewMouseLeftButtonUp(
            object sender,
            MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && item.DataContext is ContractOption contract)
            {
                SelectContract(contract);
                e.Handled = true;
            }
        }

        private void MoveSuggestionSelection(
            int direction)
        {
            int itemCount =
                VendorSuggestionList.Items.Count;

            if (itemCount <= 0)
            {
                return;
            }

            if (suggestionIndex < 0)
            {
                suggestionIndex = 0;
            }
            else
            {
                suggestionIndex += direction;
            }

            if (suggestionIndex < 0)
            {
                suggestionIndex = itemCount - 1;
            }
            else if (suggestionIndex >= itemCount)
            {
                suggestionIndex = 0;
            }

            suppressSuggestionCommit = true;
            try
            {
                VendorSuggestionList.SelectedIndex = suggestionIndex;

                if (VendorSuggestionList.SelectedItem != null)
                {
                    VendorSuggestionList.ScrollIntoView(
                        VendorSuggestionList.SelectedItem);
                }
            }
            finally
            {
                suppressSuggestionCommit = false;
            }

            VendorSuggestionPopup.IsOpen = true;
        }

        private void CommitSelectedSuggestion()
        {
            if (VendorSuggestionList.SelectedItem is ContractOption contract)
            {
                SelectContract(contract);
                return;
            }

            if (VendorSuggestionList.Items.Count > 0 &&
                VendorSuggestionList.Items[0] is ContractOption firstContract)
            {
                SelectContract(firstContract);
            }
        }

        private void SelectContract(
            ContractOption contract)
        {
            isSelectingVendor = true;

            selectedContract = contract;

            string contractText =
                string.IsNullOrWhiteSpace(contract.ContractID)
                    ? ""
                    : $"  —  {contract.ContractID}";

            VendorSearchBox.Text =
                $"{contract.VendorName}{contractText}";

            VendorNameBox.Text =
                contract.VendorName;

            VendorIDBox.Text =
                contract.VendorID;

            ItemCodeBox.Text =
                string.IsNullOrWhiteSpace(contract.ItemCode)
                    ? DefaultItemCode
                    : contract.ItemCode;

            BaseRateBox.Text =
                contract.BaseRate;

            if (!string.IsNullOrWhiteSpace(contract.StartDate) &&
                !string.IsNullOrWhiteSpace(contract.EndDate))
            {
                ContractPeriodText.Text =
                    $"Contract Period:  {contract.StartDate}  →  {contract.EndDate}";

                ContractPeriodBadge.Visibility =
                    Visibility.Visible;
            }
            else
            {
                ContractPeriodText.Text = "";

                ContractPeriodBadge.Visibility =
                    Visibility.Collapsed;
            }

            VendorSuggestionPopup.IsOpen = false;

            suppressSuggestionCommit = true;
            try
            {
                VendorSuggestionList.SelectedItem = null;
            }
            finally
            {
                suppressSuggestionCommit = false;
            }

            suggestionIndex = -1;

            isSelectingVendor = false;

            InvoiceNumberBox.Focus();

            UpdateSearchPlaceholder();
        }

        private void ClearAutoFill()
        {
            selectedContract = null;

            VendorNameBox.Text = "";
            VendorIDBox.Text = "";
            ItemCodeBox.Text = "";
            BaseRateBox.Text = "";

            ContractPeriodBadge.Visibility =
                Visibility.Collapsed;
        }

        private void PurchaseOrderDatePicker_SelectedDateChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (selectedContract == null ||
                PurchaseOrderDatePicker.SelectedDate == null)
            {
                return;
            }

            if (!TryParseContractDates(
                    selectedContract,
                    out DateTime contractStart,
                    out DateTime contractEnd))
            {
                return;
            }

            DateTime poDate =
                PurchaseOrderDatePicker.SelectedDate.Value;

            if (poDate < contractStart ||
                poDate > contractEnd)
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
                    $"Date outside contract period:  {selectedContract.StartDate}  →  {selectedContract.EndDate}";
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

        private async void SavePurchase_Click(
            object sender,
            RoutedEventArgs e)
        {
            object? originalContent =
                sender is Button clickedButton
                    ? clickedButton.Content
                    : null;

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
                {
                    return;
                }

                string vendorID =
                    VendorIDBox.Text.Trim();

                string contractID =
                    selectedContract?.ContractID?.Trim() ?? "";

                string invoiceNumber =
                    InvoiceNumberBox.Text.Trim();

                string purchaseOrderDate =
                    PurchaseOrderDatePicker.SelectedDate?
                    .ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? "";

                string deliveryDate =
                    DeliveryDatePicker.SelectedDate?
                    .ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? "";

                string invoiceWeight =
                    GetOptionalDecimalArgument(InvoiceWeightBox);

                string beforeUnloading =
                    GetRequiredDecimalArgument(BeforeUnloadingBox);

                string carrierWeight =
                    GetRequiredDecimalArgument(CarrierWeightBox);

                string noOfBags =
                    GetOptionalWholeNumberArgument(NoOfBagsBox);

                string calculatedDrc =
                    GetRequiredPercentArgument(CalculatedDrcBox);

                string gst =
                    GetRequiredPercentArgument(GstBox);

                string tds =
                    GetRequiredPercentArgument(TdsBox);

                string unloadingCharge =
                    GetOptionalDecimalArgument(UnloadingChargeBox);

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

                string output =
                    await RunPythonScript(
                        pythonScript,
                        vendorID,
                        contractID,
                        invoiceNumber,
                        purchaseOrderDate,
                        deliveryDate,
                        invoiceWeight,
                        beforeUnloading,
                        carrierWeight,
                        noOfBags,
                        calculatedDrc,
                        gst,
                        tds,
                        unloadingCharge);

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

                if (SavePurchaseButton != null)
                {
                    SavePurchaseButton.IsEnabled = true;

                    SavePurchaseButton.Content =
                        originalContent ?? "Save Purchase";
                }
            }
        }

        private bool ValidateForm()
        {
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

            if (string.IsNullOrWhiteSpace(selectedContract.ContractID))
            {
                MessageBox.Show(
                    "Selected contract is missing Contract ID. Please refresh and select the contract again.",
                    "Contract ID Missing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                VendorSearchBox.Focus();

                return false;
            }

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

            if (TryParseContractDates(
                    selectedContract,
                    out DateTime contractStart,
                    out DateTime contractEnd))
            {
                DateTime poDate =
                    PurchaseOrderDatePicker.SelectedDate.Value;

                if (poDate < contractStart ||
                    poDate > contractEnd)
                {
                    MessageBox.Show(
                        $"Purchase Order Date ({poDate:dd-MM-yyyy}) is outside the contract period.",
                        "Contract Date Mismatch",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    PurchaseOrderDatePicker.Focus();

                    return false;
                }
            }

            if (DeliveryDatePicker.SelectedDate != null &&
                DeliveryDatePicker.SelectedDate < PurchaseOrderDatePicker.SelectedDate)
            {
                MessageBox.Show(
                    "Delivery Date cannot be before the Purchase Order Date.",
                    "Date Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                DeliveryDatePicker.Focus();

                return false;
            }

            if (!ValidatePositiveDecimal(
                    BeforeUnloadingBox,
                    "Before Unloading",
                    required: true))
            {
                return false;
            }

            if (!ValidatePositiveDecimal(
                    CarrierWeightBox,
                    "Carrier Weight",
                    required: true))
            {
                return false;
            }

            decimal beforeUnloading =
                GetDecimalValue(BeforeUnloadingBox);

            decimal carrierWeight =
                GetDecimalValue(CarrierWeightBox);

            if (carrierWeight >= beforeUnloading)
            {
                MessageBox.Show(
                    "Carrier Weight must be strictly less than Before Unloading weight.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                CarrierWeightBox.Focus();

                return false;
            }

            if (!string.IsNullOrWhiteSpace(InvoiceWeightBox.Text) &&
                !ValidatePositiveDecimal(
                    InvoiceWeightBox,
                    "Invoice Weight",
                    required: false))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(NoOfBagsBox.Text))
            {
                if (!int.TryParse(
                        NoOfBagsBox.Text.Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int bags) ||
                    bags < 0)
                {
                    MessageBox.Show(
                        "No. of Bags must be a valid non-negative whole number.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    NoOfBagsBox.Focus();

                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(CalculatedDrcBox.Text) ||
                !TryParseDecimalText(
                    CalculatedDrcBox.Text.Trim().Replace("%", ""),
                    out decimal drc) ||
                drc < 0 ||
                drc > 100)
            {
                MessageBox.Show(
                    "Calculated DRC must be a valid percentage between 0 and 100.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                CalculatedDrcBox.Focus();

                return false;
            }

            if (string.IsNullOrWhiteSpace(GstBox.Text) ||
                !TryParseDecimalText(
                    GstBox.Text.Trim().Replace("%", ""),
                    out decimal gst) ||
                gst < 0 ||
                gst > 100)
            {
                MessageBox.Show(
                    "GST (%) must be a valid non-negative percentage.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                GstBox.Focus();

                return false;
            }

            if (string.IsNullOrWhiteSpace(TdsBox.Text) ||
                !TryParseDecimalText(
                    TdsBox.Text.Trim().Replace("%", ""),
                    out decimal tds) ||
                tds < 0 ||
                tds > 100)
            {
                MessageBox.Show(
                    "TDS 194Q (%) must be a valid percentage between 0 and 100.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                TdsBox.Focus();

                return false;
            }

            if (!string.IsNullOrWhiteSpace(UnloadingChargeBox.Text) &&
                !ValidatePositiveDecimal(
                    UnloadingChargeBox,
                    "Unloading Charge",
                    required: false))
            {
                return false;
            }

            return true;
        }

        private bool ValidatePositiveDecimal(
            TextBox textBox,
            string fieldName,
            bool required)
        {
            string text =
                textBox.Text.Trim();

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

            if (!TryParseDecimalText(
                    text,
                    out decimal value) ||
                value < 0)
            {
                MessageBox.Show(
                    $"{fieldName} must be a valid non-negative number.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                textBox.Focus();

                return false;
            }

            return true;
        }

        private bool TryParseContractDates(
            ContractOption? contract,
            out DateTime start,
            out DateTime end)
        {
            start = DateTime.MinValue;

            end = DateTime.MaxValue;

            if (contract == null ||
                string.IsNullOrWhiteSpace(contract.StartDate) ||
                string.IsNullOrWhiteSpace(contract.EndDate))
            {
                return false;
            }

            bool startOk =
                DateTime.TryParseExact(
                    contract.StartDate,
                    new[] { "dd-MM-yyyy", "yyyy-MM-dd", "dd/MM/yyyy" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out start);

            bool endOk =
                DateTime.TryParseExact(
                    contract.EndDate,
                    new[] { "dd-MM-yyyy", "yyyy-MM-dd", "dd/MM/yyyy" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out end);

            return startOk && endOk;
        }

        private bool TryParseDecimalText(
            string text,
            out decimal value)
        {
            string cleanText =
                text.Trim()
                    .Replace("%", "")
                    .Replace(",", "");

            if (decimal.TryParse(
                    cleanText,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                return true;
            }

            return decimal.TryParse(
                cleanText,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out value);
        }

        private decimal GetDecimalValue(
            TextBox textBox)
        {
            TryParseDecimalText(
                textBox.Text,
                out decimal value);

            return value;
        }

        private string GetRequiredDecimalArgument(
            TextBox textBox)
        {
            return GetDecimalValue(textBox)
                .ToString(CultureInfo.InvariantCulture);
        }

        private string GetOptionalDecimalArgument(
            TextBox textBox)
        {
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                return "";
            }

            return GetRequiredDecimalArgument(textBox);
        }

        private string GetRequiredPercentArgument(
            TextBox textBox)
        {
            string cleanText =
                textBox.Text.Trim().Replace("%", "");

            TryParseDecimalText(
                cleanText,
                out decimal value);

            return value.ToString(CultureInfo.InvariantCulture);
        }

        private string GetOptionalWholeNumberArgument(
            TextBox textBox)
        {
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                return "";
            }

            int value =
                int.Parse(
                    textBox.Text.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture);

            return value.ToString(CultureInfo.InvariantCulture);
        }

        private async Task<string> RunPythonScript(
            string pythonScript,
            params string[] arguments)
        {
            ProcessStartInfo start =
                new ProcessStartInfo
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

        private void ClearForm_Click(
            object sender,
            RoutedEventArgs e)
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

            suggestionIndex = -1;
            suppressSuggestionCommit = false;

            UpdateSearchPlaceholder();

            VendorSearchBox.Focus();
        }
    }
}