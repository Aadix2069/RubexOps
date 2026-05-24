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
        private sealed class CustomerOption
        {
            public string CustomerName { get; set; } = "";
            public string CustomerID { get; set; } = "";
            public string ItemCode { get; set; } = "";
        }

        private readonly List<CustomerOption> customerOptions = new();
        private CustomerOption selectedCustomer;
        private bool isSelectingCustomer;

        public EnterSalesDataPage()
        {
            InitializeComponent();
            Loaded += EnterSalesDataPage_Loaded;
        }

        private async void EnterSalesDataPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCustomerSuggestions();
        }

        private async Task LoadCustomerSuggestions()
        {
            try
            {
                string pythonScript = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "backend",
                    "read_sales_customers.py");

                if (!File.Exists(pythonScript))
                {
                    MessageBox.Show("Customer backend file not found.\n\n" + pythonScript, "File Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string output = await RunPythonScript(pythonScript, "");

                customerOptions.Clear();

                List<CustomerOption> customers =
                    JsonSerializer.Deserialize<List<CustomerOption>>(
                        output,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new();

                customerOptions.AddRange(customers);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Customer Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CustomerSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isSelectingCustomer)
            {
                return;
            }

            selectedCustomer = null;
            CustomerNameBox.Clear();
            CustomerIDBox.Clear();
            ItemCodeBox.Clear();

            string searchText = CustomerSearchBox.Text.Trim();

            if (searchText.Length == 0)
            {
                CustomerSuggestionPopup.IsOpen = false;
                return;
            }

            List<CustomerOption> matches =
                customerOptions.FindAll(customer =>
                    customer.CustomerName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    customer.CustomerID.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    customer.ItemCode.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);

            CustomerSuggestionList.ItemsSource = matches;
            CustomerSuggestionPopup.IsOpen = matches.Count > 0;
        }

        private void CustomerSuggestionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CustomerSuggestionList.SelectedItem is CustomerOption customer)
            {
                ApplySelectedCustomer(customer);
            }
        }

        private void ApplySelectedCustomer(CustomerOption customer)
        {
            selectedCustomer = customer;
            isSelectingCustomer = true;
            CustomerSearchBox.Text = $"{customer.CustomerName} - {customer.CustomerID}";
            CustomerNameBox.Text = customer.CustomerName;
            CustomerIDBox.Text = customer.CustomerID;
            ItemCodeBox.Text = string.IsNullOrWhiteSpace(customer.ItemCode) ? "NRFC" : customer.ItemCode;
            CustomerSuggestionPopup.IsOpen = false;
            CustomerSuggestionList.SelectedItem = null;
            isSelectingCustomer = false;
        }

        private async void SaveSales_Click(object sender, RoutedEventArgs e)
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

                if (!ValidateForm(
                        out decimal weight,
                        out decimal baseRate,
                        out decimal gstPercent,
                        out decimal tcsPercent,
                        out decimal loadingCharge))
                {
                    return;
                }

                string pythonScript = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "backend",
                    "create_sales_data.py");

                if (!File.Exists(pythonScript))
                {
                    MessageBox.Show("Backend Python file not found.\n\n" + pythonScript, "File Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string arguments =
                    Quote(CustomerIDBox.Text.Trim()) + " " +
                    Quote(ItemCodeBox.Text.Trim()) + " " +
                    Quote(InvoiceNumberBox.Text.Trim()) + " " +
                    Quote(SalesOrderDatePicker.SelectedDate?.ToString("dd-MM-yyyy") ?? "") + " " +
                    Quote(DispatchDatePicker.SelectedDate?.ToString("dd-MM-yyyy") ?? "") + " " +
                    Quote(weight.ToString(CultureInfo.InvariantCulture)) + " " +
                    Quote(baseRate.ToString(CultureInfo.InvariantCulture)) + " " +
                    Quote(gstPercent.ToString(CultureInfo.InvariantCulture)) + " " +
                    Quote(tcsPercent.ToString(CultureInfo.InvariantCulture)) + " " +
                    Quote(loadingCharge.ToString(CultureInfo.InvariantCulture));

                string output = await RunPythonScript(pythonScript, arguments);

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

                if (sender is Button button)
                {
                    button.IsEnabled = true;
                    button.Content = "Save Sales";
                }
            }
        }

        private bool ValidateForm(
            out decimal weight,
            out decimal baseRate,
            out decimal gstPercent,
            out decimal tcsPercent,
            out decimal loadingCharge)
        {
            weight = 0;
            baseRate = 0;
            gstPercent = 0;
            tcsPercent = 0;
            loadingCharge = 0;

            if (selectedCustomer == null)
            {
                ShowValidation("Please select a valid customer from the suggestions.", CustomerSearchBox);
                return false;
            }

            if (string.IsNullOrWhiteSpace(InvoiceNumberBox.Text))
            {
                ShowValidation("Invoice Number is required.", InvoiceNumberBox);
                return false;
            }

            if (SalesOrderDatePicker.SelectedDate == null)
            {
                ShowValidation("Please select a Sales Order date.", SalesOrderDatePicker);
                return false;
            }

            if (DispatchDatePicker.SelectedDate == null)
            {
                ShowValidation("Please select a Dispatch date.", DispatchDatePicker);
                return false;
            }

            if (SalesOrderDatePicker.SelectedDate > DispatchDatePicker.SelectedDate)
            {
                ShowValidation("Dispatch date must be after Sales Order date.", DispatchDatePicker);
                return false;
            }

            if (!TryReadDecimal(WeightBox.Text, out weight) || weight <= 0)
            {
                ShowValidation("Weight must be greater than zero.", WeightBox);
                return false;
            }

            if (!TryReadDecimal(BaseRateBox.Text, out baseRate) || baseRate <= 0)
            {
                ShowValidation("Base Rate must be greater than zero.", BaseRateBox);
                return false;
            }

            if (!TryReadDecimal(GstPercentBox.Text, out gstPercent) || gstPercent < 0 || gstPercent > 100)
            {
                ShowValidation("GST must be between 0 and 100.", GstPercentBox);
                return false;
            }

            if (!TryReadDecimal(TcsPercentBox.Text, out tcsPercent) || tcsPercent < 0 || tcsPercent > 100)
            {
                ShowValidation("TCS 194Q must be between 0 and 100.", TcsPercentBox);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(LoadingChargeBox.Text) &&
                (!TryReadDecimal(LoadingChargeBox.Text, out loadingCharge) || loadingCharge < 0))
            {
                ShowValidation("Loading Charge cannot be negative.", LoadingChargeBox);
                return false;
            }

            return true;
        }

        private async Task<string> RunPythonScript(string pythonScript, string arguments)
        {
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{pythonScript}\" {arguments}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backend")
            };

            string output = "";
            string error = "";
            int exitCode = 0;

            await Task.Run(() =>
            {
                using Process process = Process.Start(start) ?? throw new Exception("Failed to start backend process.");
                output = process.StandardOutput.ReadToEnd();
                error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                exitCode = process.ExitCode;
            });

            if (exitCode != 0 || !string.IsNullOrWhiteSpace(error))
            {
                throw new Exception(!string.IsNullOrWhiteSpace(error) ? error.Trim() : output.Trim());
            }

            return output.Trim();
        }

        private static bool TryReadDecimal(string text, out decimal value)
        {
            return decimal.TryParse(
                text.Trim().Replace(",", "").Replace("%", ""),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static void ShowValidation(string message, Control control)
        {
            MessageBox.Show(message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            control.Focus();
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            selectedCustomer = null;
            CustomerSearchBox.Clear();
            CustomerNameBox.Clear();
            CustomerIDBox.Clear();
            ItemCodeBox.Clear();
            InvoiceNumberBox.Clear();
            SalesOrderDatePicker.SelectedDate = null;
            DispatchDatePicker.SelectedDate = null;
            WeightBox.Clear();
            BaseRateBox.Clear();
            GstPercentBox.Clear();
            TcsPercentBox.Clear();
            LoadingChargeBox.Clear();
            CustomerSuggestionPopup.IsOpen = false;
            CustomerSearchBox.Focus();
        }
    }
}

