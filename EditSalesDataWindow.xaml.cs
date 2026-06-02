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
    public partial class EditSalesDataWindow : Window
    {
        private const string DefaultItemCode = "ISNR20";

        private readonly SalesDataRecord salesData;

        public EditSalesDataWindow(
            SalesDataRecord selectedSalesData)
        {
            InitializeComponent();

            salesData =
                selectedSalesData;

            LoadSalesData();

            // Listen for the Enter key across the entire window to trigger a save
            this.KeyDown += EditSalesDataWindow_KeyDown;
        }

        // New event handler for the Enter key
        private void EditSalesDataWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Prevent the key event from propagating further
                e.Handled = true;

                // Trigger the existing async save logic
                Save_Click(this, new RoutedEventArgs());
            }
        }

        private void LoadSalesData()
        {
            CustomerNameBox.Text =
                salesData.CustomerName;
            CustomerNameBox.IsReadOnly = true;

            CustomerIDBox.Text =
                salesData.CustomerID;
            CustomerIDBox.IsReadOnly = true;

            ItemCodeBox.Text =
                DefaultItemCode;
            ItemCodeBox.IsReadOnly = true;

            InvoiceNumberBox.Text =
                salesData.InvoiceNumber;

            SalesOrderDatePicker.SelectedDate =
                ParseDate(salesData.SalesOrderDate);

            DispatchDatePicker.SelectedDate =
                ParseDate(salesData.DispatchDate);

            WeightBox.Text =
                FormatNullableDecimal(salesData.Weight);

            GstBox.Text =
                FormatNullableDecimal(salesData.GstPercent);

            Tcs194QBox.Text =
                FormatNullableDecimal(salesData.Tcs194QPercent);

            LoadingChargeBox.Text =
                salesData.LoadingCharge.GetValueOrDefault() == 0
                ? ""
                : FormatNullableDecimal(salesData.LoadingCharge);
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
                    "update_sales_data.py"
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

                string salesOrderDate =
                    SalesOrderDatePicker.SelectedDate?
                    .ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? "";

                string dispatchDate =
                    DispatchDatePicker.SelectedDate?
                    .ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? "";

                // Pass the underlying customer/item data (ignoring UI tampering)
                string output =
                    await RunPythonScript(
                        pythonScript,
                        salesData.RowNumber.ToString(CultureInfo.InvariantCulture),
                        salesData.CustomerName ?? "",
                        salesData.CustomerID ?? "",
                        GetContractId(),
                        DefaultItemCode,
                        InvoiceNumberBox.Text.Trim(),
                        salesOrderDate,
                        dispatchDate,
                        GetRequiredDecimalArgument(WeightBox),
                        GetRequiredDecimalArgument(GstBox),
                        GetRequiredDecimalArgument(Tcs194QBox),
                        GetOptionalDecimalArgument(LoadingChargeBox));

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

            if (SalesOrderDatePicker.SelectedDate == null)
            {
                MessageBox.Show(
                    "Please select a Sales Order date.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                SalesOrderDatePicker.Focus();

                return false;
            }

            if (DispatchDatePicker.SelectedDate != null &&
                SalesOrderDatePicker.SelectedDate > DispatchDatePicker.SelectedDate)
            {
                MessageBox.Show(
                    "Dispatch date must be on or after Sales Order date.",
                    "Date Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                DispatchDatePicker.Focus();

                return false;
            }

            if (!ValidateRequiredDecimal(
                    WeightBox,
                    "Weight",
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
                    Tcs194QBox,
                    "TCS 194Q",
                    true))
            {
                return false;
            }

            if (!ValidateOptionalDecimal(
                    LoadingChargeBox,
                    "Loading Charge",
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

        private string GetContractId()
        {
            object? value =
                salesData
                    .GetType()
                    .GetProperty("ContractID")
                    ?.GetValue(salesData);

            if (value == null)
            {
                value =
                    salesData
                        .GetType()
                        .GetProperty("contract_id")
                        ?.GetValue(salesData);
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