using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RubexOps
{
    public partial class EditPurchaseDataWindow : Window
    {
        private const string DefaultItemCode = "NRFC";

        private readonly PurchaseDataRecord purchaseData;

        public EditPurchaseDataWindow(
            PurchaseDataRecord selectedPurchaseData)
        {
            InitializeComponent();

            purchaseData =
                selectedPurchaseData;

            LoadPurchaseData();

            // Listen for the Enter key across the entire window to trigger a save
            this.KeyDown += EditPurchaseDataWindow_KeyDown;
        }

        // New event handler for the Enter key
        private void EditPurchaseDataWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Prevent the key event from propagating further
                e.Handled = true;

                // Trigger the existing async save logic
                Save_Click(this, new RoutedEventArgs());
            }
        }

        private void LoadPurchaseData()
        {
            VendorNameBox.Text =
                purchaseData.VendorName;
            VendorNameBox.IsReadOnly = true;

            VendorIDBox.Text =
                purchaseData.VendorID;
            VendorIDBox.IsReadOnly = true;

            ItemCodeBox.Text =
                DefaultItemCode;
            ItemCodeBox.IsReadOnly = true;

            InvoiceNumberBox.Text =
                purchaseData.InvoiceNumber;

            PurchaseOrderDatePicker.SelectedDate =
                ParseDate(purchaseData.PurchaseOrderDate);

            DeliveryDatePicker.SelectedDate =
                ParseDate(purchaseData.DeliveryDate);

            InvoiceWeightBox.Text =
                (purchaseData.InvoiceWeight == null || purchaseData.InvoiceWeight == 0)
                ? ""
                : FormatNullableDecimal(purchaseData.InvoiceWeight);

            BeforeUnloadingBox.Text =
                FormatNullableDecimal(purchaseData.BeforeUnloading);

            CarrierWeightBox.Text =
                FormatNullableDecimal(purchaseData.CarrierWeight);

            NoOfBagsBox.Text =
                (purchaseData.NoOfBags == null || purchaseData.NoOfBags == 0)
                    ? ""
                    : ((int)purchaseData.NoOfBags.Value).ToString(CultureInfo.InvariantCulture);

            CalculatedDrcBox.Text =
                FormatNullableDecimal(purchaseData.CalculatedDrc);

            GstBox.Text =
                FormatNullableDecimal(purchaseData.GstPercent);

            Tds194QBox.Text =
                FormatNullableDecimal(purchaseData.Tds194QPercent);

            UnloadingChargeBox.Text =
                purchaseData.UnloadingCharge.GetValueOrDefault() == 0
                ? ""
                : FormatNullableDecimal(purchaseData.UnloadingCharge);
        }

        private async void Save_Click(
            object sender,
            RoutedEventArgs e)
        {
            object? originalContent =
                SaveButton.Content;

            try
            {
                Mouse.OverrideCursor =
                    Cursors.Wait;

                MainGrid.IsEnabled = false;

                MainGrid.Opacity = 0.75;

                SaveButton.IsEnabled = false;

                SaveButton.Content = "Saving...";

                if (!ValidateForm())
                {
                    return;
                }

                string pythonScript = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "backend",
                    "update_purchase_data.py"
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

                string purchaseOrderDate =
                    PurchaseOrderDatePicker.SelectedDate?
                    .ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? "";

                string deliveryDate =
                    DeliveryDatePicker.SelectedDate?
                    .ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? "";

                // Pass the underlying vendor/item data (ignoring UI tampering)
                string output =
                    await RunPythonScript(
                        pythonScript,
                        purchaseData.RowNumber.ToString(CultureInfo.InvariantCulture),
                        purchaseData.VendorName ?? "",
                        purchaseData.VendorID ?? "",
                        GetContractId(),
                        DefaultItemCode,
                        InvoiceNumberBox.Text.Trim(),
                        purchaseOrderDate,
                        deliveryDate,
                        GetOptionalDecimalArgument(InvoiceWeightBox),
                        GetRequiredDecimalArgument(BeforeUnloadingBox),
                        GetRequiredDecimalArgument(CarrierWeightBox),
                        GetOptionalWholeNumberArgument(NoOfBagsBox),
                        GetRequiredDecimalArgument(CalculatedDrcBox),
                        GetRequiredDecimalArgument(GstBox),
                        GetRequiredDecimalArgument(Tds194QBox),
                        GetOptionalDecimalArgument(UnloadingChargeBox));

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

                DialogResult = true;

                Close();
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

                SaveButton.IsEnabled = true;

                SaveButton.Content =
                    originalContent ?? "Save Changes";
            }
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(
                    InvoiceNumberBox.Text))
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
                    "Please select a Purchase Order date.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                PurchaseOrderDatePicker.Focus();

                return false;
            }

            if (DeliveryDatePicker.SelectedDate != null &&
                PurchaseOrderDatePicker.SelectedDate > DeliveryDatePicker.SelectedDate)
            {
                MessageBox.Show(
                    "Delivery date must be on or after Purchase Order date.",
                    "Date Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                DeliveryDatePicker.Focus();

                return false;
            }

            if (!ValidateOptionalDecimal(
                    InvoiceWeightBox,
                    "Invoice Weight",
                    allowZero: true))
            {
                return false;
            }

            if (!ValidateRequiredDecimal(
                    BeforeUnloadingBox,
                    "Before Unloading",
                    false))
            {
                return false;
            }

            if (!ValidateRequiredDecimal(
                    CarrierWeightBox,
                    "Carrier Weight",
                    true))
            {
                return false;
            }

            decimal beforeUnloading =
                GetDecimalValue(
                    BeforeUnloadingBox);

            decimal carrierWeight =
                GetDecimalValue(
                    CarrierWeightBox);

            if (carrierWeight > beforeUnloading)
            {
                MessageBox.Show(
                    "Carrier Weight cannot be greater than Before Unloading weight.",
                    "Weight Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                CarrierWeightBox.Focus();

                return false;
            }

            if (!ValidateOptionalWholeNumber(
                    NoOfBagsBox,
                    "No. of Bags"))
            {
                return false;
            }

            if (!ValidateRequiredPercent(
                    CalculatedDrcBox,
                    "Calculated DRC",
                    false))
            {
                return false;
            }

            if (!ValidateRequiredPercent(
                    GstBox,
                    "GST",
                    true))
            {
                return false;
            }

            if (!ValidateRequiredPercent(
                    Tds194QBox,
                    "TDS 194Q",
                    true))
            {
                return false;
            }

            if (!ValidateOptionalDecimal(
                    UnloadingChargeBox,
                    "Unloading Charge",
                    true))
            {
                return false;
            }

            return true;
        }

        private bool ValidateRequiredDecimal(
            TextBox textBox,
            string fieldName,
            bool allowZero)
        {
            if (string.IsNullOrWhiteSpace(
                    textBox.Text))
            {
                MessageBox.Show(
                    $"{fieldName} is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                textBox.Focus();

                return false;
            }

            if (!TryParseDecimalText(
                    textBox.Text,
                    out decimal value))
            {
                MessageBox.Show(
                    $"{fieldName} must be a valid number.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                textBox.Focus();

                return false;
            }

            if (allowZero)
            {
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
            }
            else if (value <= 0)
            {
                MessageBox.Show(
                    $"{fieldName} must be greater than zero.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                textBox.Focus();

                return false;
            }

            return true;
        }

        private bool ValidateOptionalDecimal(
            TextBox textBox,
            string fieldName,
            bool allowZero)
        {
            if (string.IsNullOrWhiteSpace(
                    textBox.Text))
            {
                return true;
            }

            if (!TryParseDecimalText(
                    textBox.Text,
                    out decimal value))
            {
                MessageBox.Show(
                    $"{fieldName} must be a valid number.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                textBox.Focus();

                return false;
            }

            if (allowZero)
            {
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
            }
            else if (value <= 0)
            {
                MessageBox.Show(
                    $"{fieldName} must be greater than zero when entered.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                textBox.Focus();

                return false;
            }

            return true;
        }

        private bool ValidateRequiredPercent(
            TextBox textBox,
            string fieldName,
            bool allowZero)
        {
            if (string.IsNullOrWhiteSpace(
                    textBox.Text))
            {
                MessageBox.Show(
                    $"{fieldName} is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                textBox.Focus();

                return false;
            }

            if (!TryParseDecimalText(
                    textBox.Text.Replace("%", ""),
                    out decimal value))
            {
                MessageBox.Show(
                    $"{fieldName} must be numeric.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                textBox.Focus();

                return false;
            }

            if (allowZero)
            {
                if (value < 0 ||
                    value > 100)
                {
                    MessageBox.Show(
                        $"{fieldName} must be between 0 and 100.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    textBox.Focus();

                    return false;
                }
            }
            else if (value <= 0 ||
                     value > 100)
            {
                MessageBox.Show(
                    $"{fieldName} must be greater than 0 and not more than 100.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                textBox.Focus();

                return false;
            }

            return true;
        }

        private bool ValidateOptionalWholeNumber(
            TextBox textBox,
            string fieldName)
        {
            if (string.IsNullOrWhiteSpace(
                    textBox.Text))
            {
                return true;
            }

            if (!double.TryParse(
                    textBox.Text.Trim(),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out double value) ||
                value < 0 ||
                value % 1 != 0)
            {
                MessageBox.Show(
                    $"{fieldName} must be a valid non-negative whole number.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                textBox.Focus();

                return false;
            }

            return true;
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
            if (string.IsNullOrWhiteSpace(
                    textBox.Text))
            {
                return "";
            }

            return GetRequiredDecimalArgument(
                textBox);
        }

        private string GetOptionalWholeNumberArgument(
            TextBox textBox)
        {
            if (string.IsNullOrWhiteSpace(
                    textBox.Text))
            {
                return "";
            }

            if (double.TryParse(
                    textBox.Text.Trim(),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out double value))
            {
                return ((int)value).ToString(CultureInfo.InvariantCulture);
            }

            return "";
        }

        private string GetContractId()
        {
            object? value =
                purchaseData
                    .GetType()
                    .GetProperty("ContractID")
                    ?.GetValue(purchaseData);

            if (value == null)
            {
                value =
                    purchaseData
                        .GetType()
                        .GetProperty("contract_id")
                        ?.GetValue(purchaseData);
            }

            return value?.ToString()?.Trim() ?? "";
        }

        private DateTime? ParseDate(
            string value)
        {
            if (DateTime.TryParseExact(
                    value,
                    "dd-MM-yyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime parsedDate))
            {
                return parsedDate;
            }

            return null;
        }

        private string FormatNullableDecimal(
            decimal? value)
        {
            return value.HasValue
                ? value.Value.ToString("0.##", CultureInfo.InvariantCulture)
                : "";
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

        private void Cancel_Click(
            object sender,
            RoutedEventArgs e)
        {
            DialogResult = false;

            Close();
        }
    }
}