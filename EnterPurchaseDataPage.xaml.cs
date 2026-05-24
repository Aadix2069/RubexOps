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

        private sealed class VendorOption
        {
            public string VendorName { get; set; } = "";

            public string VendorID { get; set; } = "";
        }



        // =====================================================
        // FIELDS
        // =====================================================

        private readonly List<VendorOption> vendorOptions =
            new List<VendorOption>();

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
            await LoadVendorSuggestions();
        }



        // =====================================================
        // LOAD VENDOR SUGGESTIONS
        // =====================================================

        private async Task LoadVendorSuggestions()
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

                string output =
                    await RunPythonScript(
                        pythonScript,
                        "");

                List<VendorOption> vendors =
                    JsonSerializer.Deserialize<List<VendorOption>>(
                        output,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                vendorOptions.Clear();

                if (vendors != null)
                {
                    vendorOptions.AddRange(vendors);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Vendor Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }



        // =====================================================
        // VENDOR SEARCH
        // =====================================================

        private void VendorSearchBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            if (isSelectingVendor)
            {
                return;
            }

            string searchText =
                VendorSearchBox.Text.Trim();

            if (searchText.Length == 0)
            {
                VendorSuggestionPopup.IsOpen = false;

                return;
            }

            List<VendorOption> matches =
                vendorOptions.FindAll(vendor =>
                    vendor.VendorName.IndexOf(
                        searchText,
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    vendor.VendorID.IndexOf(
                        searchText,
                        StringComparison.OrdinalIgnoreCase) >= 0);

            VendorSuggestionList.ItemsSource = matches;

            VendorSuggestionPopup.IsOpen =
                matches.Count > 0;
        }



        // =====================================================
        // SELECT VENDOR
        // =====================================================

        private void VendorSuggestionList_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            VendorOption selectedVendor =
                VendorSuggestionList.SelectedItem as VendorOption;

            if (selectedVendor == null)
            {
                return;
            }

            isSelectingVendor = true;

            VendorSearchBox.Text =
                $"{selectedVendor.VendorName} - {selectedVendor.VendorID}";

            VendorNameBox.Text =
                selectedVendor.VendorName;

            VendorIDBox.Text =
                selectedVendor.VendorID;

            VendorSuggestionPopup.IsOpen = false;

            isSelectingVendor = false;
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
                Mouse.OverrideCursor =
                    Cursors.Wait;

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

                string vendorName =
                    VendorNameBox.Text.Trim();

                string vendorID =
                    VendorIDBox.Text.Trim();

                string itemCode =
                    ItemCodeBox.Text.Trim();

                string invoiceNumber =
                    InvoiceNumberBox.Text.Trim();

                string purchaseOrderDate =
                    PurchaseOrderDatePicker.SelectedDate?
                    .ToString("dd-MM-yyyy") ?? "";

                string deliveryDate =
                    DeliveryDatePicker.SelectedDate?
                    .ToString("dd-MM-yyyy") ?? "";

                decimal invoiceWeight =
                    decimal.Parse(InvoiceWeightBox.Text.Trim());

                decimal beforeUnloading =
                    decimal.Parse(BeforeUnloadingBox.Text.Trim());

                decimal carrierWeight =
                    decimal.Parse(CarrierWeightBox.Text.Trim());

                int noOfBags =
                    int.Parse(NoOfBagsBox.Text.Trim());

                decimal calculatedDrc =
                    decimal.Parse(
                        CalculatedDrcBox.Text
                        .Trim()
                        .Replace("%", ""));

                string pythonScript = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "backend",
                    "create_purchase_data.py"
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

                string arguments =
                    $"\"{vendorName}\" " +
                    $"\"{vendorID}\" " +
                    $"\"{itemCode}\" " +
                    $"\"{invoiceNumber}\" " +
                    $"\"{purchaseOrderDate}\" " +
                    $"\"{deliveryDate}\" " +
                    $"\"{invoiceWeight}\" " +
                    $"\"{beforeUnloading}\" " +
                    $"\"{carrierWeight}\" " +
                    $"\"{noOfBags}\" " +
                    $"\"{calculatedDrc}\"";

                string output =
                    await RunPythonScript(
                        pythonScript,
                        arguments);

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

                    button.Content =
                        "Save Purchase";
                }
            }
        }



        // =====================================================
        // VALIDATION
        // =====================================================

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(
                    VendorNameBox.Text))
            {
                MessageBox.Show(
                    "Please select a Vendor.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                VendorSearchBox.Focus();

                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    VendorIDBox.Text))
            {
                MessageBox.Show(
                    "Vendor ID is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                VendorIDBox.Focus();

                return false;
            }

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

            if (DeliveryDatePicker.SelectedDate == null)
            {
                MessageBox.Show(
                    "Please select a Delivery date.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                DeliveryDatePicker.Focus();

                return false;
            }

            if (PurchaseOrderDatePicker.SelectedDate >
                DeliveryDatePicker.SelectedDate)
            {
                MessageBox.Show(
                    "Delivery date must be after Purchase Order date.",
                    "Date Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            if (!ValidatePositiveDecimal(
                    InvoiceWeightBox,
                    "Invoice Weight"))
            {
                return false;
            }

            if (!ValidatePositiveDecimal(
                    BeforeUnloadingBox,
                    "Before Unloading"))
            {
                return false;
            }

            if (!ValidatePositiveDecimal(
                    CarrierWeightBox,
                    "Carrier Weight"))
            {
                return false;
            }

            if (!int.TryParse(
                    NoOfBagsBox.Text.Trim(),
                    out int noOfBags))
            {
                MessageBox.Show(
                    "No. of Bags must be a whole number.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                NoOfBagsBox.Focus();

                return false;
            }

            if (noOfBags < 0)
            {
                MessageBox.Show(
                    "No. of Bags cannot be negative.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                NoOfBagsBox.Focus();

                return false;
            }

            string drcText =
                CalculatedDrcBox.Text
                .Trim()
                .Replace("%", "");

            if (!decimal.TryParse(
                    drcText,
                    out decimal calculatedDrc))
            {
                MessageBox.Show(
                    "Calculated DRC must be numeric.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                CalculatedDrcBox.Focus();

                return false;
            }

            if (calculatedDrc < 0 ||
                calculatedDrc > 100)
            {
                MessageBox.Show(
                    "Calculated DRC must be between 0 and 100.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                CalculatedDrcBox.Focus();

                return false;
            }

            return true;
        }



        // =====================================================
        // DECIMAL VALIDATION
        // =====================================================

        private bool ValidatePositiveDecimal(
            TextBox textBox,
            string fieldName)
        {
            if (!decimal.TryParse(
                    textBox.Text.Trim(),
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



        // =====================================================
        // CLEAR FORM BUTTON
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
            VendorSearchBox.Clear();

            VendorNameBox.Clear();

            VendorIDBox.Clear();

            ItemCodeBox.Text = "NRFC";

            InvoiceNumberBox.Clear();

            PurchaseOrderDatePicker.SelectedDate = null;

            DeliveryDatePicker.SelectedDate = null;

            InvoiceWeightBox.Clear();

            BeforeUnloadingBox.Clear();

            CarrierWeightBox.Clear();

            NoOfBagsBox.Clear();

            CalculatedDrcBox.Clear();

            VendorSuggestionPopup.IsOpen = false;

            VendorSearchBox.Focus();
        }
    }
}
