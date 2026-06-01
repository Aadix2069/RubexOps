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
    public partial class EnterSalesDataPage : Page
    {
        private const string DefaultItemCode = "ISNR20";

        private sealed class ContractOption
        {
            public string CustomerName { get; set; } = "";
            public string CustomerID { get; set; } = "";
            public string ContractID { get; set; } = "";
            public string ItemCode { get; set; } = "";
            public string BaseRate { get; set; } = "";
            public string StartDate { get; set; } = "";
            public string EndDate { get; set; } = "";
        }

        private readonly List<ContractOption> contractOptions =
            new List<ContractOption>();

        private ContractOption? selectedContract = null;

        private bool isSelectingCustomer;
        private bool suppressSuggestionCommit;
        private int suggestionIndex = -1;

        public EnterSalesDataPage()
        {
            InitializeComponent();

            Loaded += EnterSalesDataPage_Loaded;

            PreviewKeyDown += EnterSalesDataPage_PreviewKeyDown;
        }

        private async void EnterSalesDataPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            await LoadActiveContracts();

            UpdateSearchPlaceholder();
        }

        private void EnterSalesDataPage_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (CustomerSuggestionPopup.IsOpen &&
                CustomerSuggestionList.Items.Count > 0)
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
                    CustomerSuggestionPopup.IsOpen = false;
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.Enter)
            {
                e.Handled = true;

                if (SaveSalesButton.IsEnabled)
                {
                    SaveSales_Click(
                        SaveSalesButton,
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
                    "read_sales_customers.py"
                );

                if (!File.Exists(pythonScript))
                {
                    MessageBox.Show(
                        "Customer suggestion backend file not found.\n\n" + pythonScript,
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
                string.IsNullOrEmpty(CustomerSearchBox.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void CustomerSearchBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            UpdateSearchPlaceholder();

            if (isSelectingCustomer)
            {
                return;
            }

            if (selectedContract != null)
            {
                ClearAutoFill();
            }

            string searchText =
                CustomerSearchBox.Text.Trim();

            if (searchText.Length == 0)
            {
                CustomerSuggestionPopup.IsOpen = false;
                CustomerSuggestionList.ItemsSource = null;
                suggestionIndex = -1;
                return;
            }

            List<ContractOption> matches =
     contractOptions.FindAll(c =>

         (!string.IsNullOrWhiteSpace(c.CustomerName) &&
          c.CustomerName.Trim().StartsWith(
              searchText,
              StringComparison.OrdinalIgnoreCase))

         ||

         (!string.IsNullOrWhiteSpace(c.CustomerID) &&
          c.CustomerID.Trim().StartsWith(
              searchText,
              StringComparison.OrdinalIgnoreCase))

         ||

         (!string.IsNullOrWhiteSpace(c.ContractID) &&
          c.ContractID.Trim().StartsWith(
              searchText,
              StringComparison.OrdinalIgnoreCase)));

            CustomerSuggestionList.ItemsSource =
                matches;

            if (matches.Count > 0)
            {
                suggestionIndex = 0;

                suppressSuggestionCommit = true;
                try
                {
                    CustomerSuggestionList.SelectedIndex = suggestionIndex;

                    if (CustomerSuggestionList.SelectedItem != null)
                    {
                        CustomerSuggestionList.ScrollIntoView(
                            CustomerSuggestionList.SelectedItem);
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
                CustomerSuggestionList.SelectedItem = null;
            }

            CustomerSuggestionPopup.IsOpen =
                matches.Count > 0;
        }

        private void CustomerSuggestionItem_PreviewMouseLeftButtonUp(
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
                CustomerSuggestionList.Items.Count;

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
                CustomerSuggestionList.SelectedIndex = suggestionIndex;

                if (CustomerSuggestionList.SelectedItem != null)
                {
                    CustomerSuggestionList.ScrollIntoView(
                        CustomerSuggestionList.SelectedItem);
                }
            }
            finally
            {
                suppressSuggestionCommit = false;
            }

            CustomerSuggestionPopup.IsOpen = true;
        }

        private void CommitSelectedSuggestion()
        {
            if (CustomerSuggestionList.SelectedItem is ContractOption contract)
            {
                SelectContract(contract);
                return;
            }

            if (CustomerSuggestionList.Items.Count > 0 &&
                CustomerSuggestionList.Items[0] is ContractOption firstContract)
            {
                SelectContract(firstContract);
            }
        }

        private void SelectContract(
            ContractOption contract)
        {
            isSelectingCustomer = true;

            selectedContract = contract;

            string contractText =
                string.IsNullOrWhiteSpace(contract.ContractID)
                    ? ""
                    : $"  —  {contract.ContractID}";

            CustomerSearchBox.Text =
                $"{contract.CustomerName}{contractText}";

            CustomerNameBox.Text =
                contract.CustomerName;

            CustomerIDBox.Text =
                contract.CustomerID;

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

            CustomerSuggestionPopup.IsOpen = false;

            suppressSuggestionCommit = true;
            try
            {
                CustomerSuggestionList.SelectedItem = null;
            }
            finally
            {
                suppressSuggestionCommit = false;
            }

            suggestionIndex = -1;

            isSelectingCustomer = false;

            InvoiceNumberBox.Focus();

            UpdateSearchPlaceholder();
        }

        private void ClearAutoFill()
        {
            selectedContract = null;

            CustomerNameBox.Text = "";
            CustomerIDBox.Text = "";
            ItemCodeBox.Text = "";
            BaseRateBox.Text = "";

            ContractPeriodBadge.Visibility =
                Visibility.Collapsed;
        }

        private void SalesOrderDatePicker_SelectedDateChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (selectedContract == null ||
                SalesOrderDatePicker.SelectedDate == null)
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
                SalesOrderDatePicker.SelectedDate.Value;

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

        private async void SaveSales_Click(
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

                string customerID =
                    CustomerIDBox.Text.Trim();

                string contractID =
                    selectedContract?.ContractID?.Trim() ?? "";

                string invoiceNumber =
                    InvoiceNumberBox.Text.Trim();

                string salesOrderDate =
                    SalesOrderDatePicker.SelectedDate?
                    .ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? "";

                string dispatchDate =
                    DispatchDatePicker.SelectedDate?
                    .ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? "";

                string weight =
                    GetRequiredDecimalArgument(WeightBox);

                string gst =
                    GetRequiredPercentArgument(GstBox);

                string tcs =
                    GetRequiredPercentArgument(TcsBox);

                string loadingCharge =
                    GetOptionalDecimalArgument(LoadingChargeBox);

                string pythonScript = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "backend",
                    "create_sales_data.py"
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
                        customerID,
                        contractID,
                        invoiceNumber,
                        salesOrderDate,
                        dispatchDate,
                        weight,
                        gst,
                        tcs,
                        loadingCharge);

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

                if (SaveSalesButton != null)
                {
                    SaveSalesButton.IsEnabled = true;

                    SaveSalesButton.Content =
                        originalContent ?? "Save Sales";
                }
            }
        }

        private bool ValidateForm()
        {
            if (selectedContract == null ||
                string.IsNullOrWhiteSpace(CustomerIDBox.Text))
            {
                MessageBox.Show(
                    "Please search and select an active contract before saving.",
                    "No Contract Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                CustomerSearchBox.Focus();

                return false;
            }

            if (string.IsNullOrWhiteSpace(selectedContract.ContractID))
            {
                MessageBox.Show(
                    "Selected contract is missing Contract ID. Please refresh and select the contract again.",
                    "Contract ID Missing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                CustomerSearchBox.Focus();

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

            if (SalesOrderDatePicker.SelectedDate == null)
            {
                MessageBox.Show(
                    "Sales Order Date is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                SalesOrderDatePicker.Focus();

                return false;
            }

            if (TryParseContractDates(
                    selectedContract,
                    out DateTime contractStart,
                    out DateTime contractEnd))
            {
                DateTime poDate =
                    SalesOrderDatePicker.SelectedDate.Value;

                if (poDate < contractStart ||
                    poDate > contractEnd)
                {
                    MessageBox.Show(
                        $"Sales Order Date ({poDate:dd-MM-yyyy}) is outside the contract period.",
                        "Contract Date Mismatch",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    SalesOrderDatePicker.Focus();

                    return false;
                }
            }

            if (DispatchDatePicker.SelectedDate != null &&
                DispatchDatePicker.SelectedDate < SalesOrderDatePicker.SelectedDate)
            {
                MessageBox.Show(
                    "Dispatch Date cannot be before the Sales Order Date.",
                    "Date Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                DispatchDatePicker.Focus();

                return false;
            }

            if (!ValidatePositiveDecimal(
                    WeightBox,
                    "Weight",
                    required: true))
            {
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

            if (string.IsNullOrWhiteSpace(TcsBox.Text) ||
                !TryParseDecimalText(
                    TcsBox.Text.Trim().Replace("%", ""),
                    out decimal tcs) ||
                tcs < 0 ||
                tcs > 100)
            {
                MessageBox.Show(
                    "TCS 194Q (%) must be a valid percentage between 0 and 100.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                TcsBox.Focus();

                return false;
            }

            if (!string.IsNullOrWhiteSpace(LoadingChargeBox.Text) &&
                !ValidatePositiveDecimal(
                    LoadingChargeBox,
                    "Loading Charge",
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
                value <= 0)
            {
                MessageBox.Show(
                    $"{fieldName} must be a valid positive number.",
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
            isSelectingCustomer = false;

            CustomerSearchBox.Clear();

            ClearAutoFill();

            InvoiceNumberBox.Clear();

            SalesOrderDatePicker.SelectedDate = null;

            DispatchDatePicker.SelectedDate = null;

            WeightBox.Clear();

            GstBox.Text = "5";

            TcsBox.Text = "0.1";

            LoadingChargeBox.Clear();

            CustomerSuggestionPopup.IsOpen = false;

            suggestionIndex = -1;
            suppressSuggestionCommit = false;

            UpdateSearchPlaceholder();

            CustomerSearchBox.Focus();
        }
    }
}