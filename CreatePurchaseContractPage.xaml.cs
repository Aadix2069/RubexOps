using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RubexOps
{
    public partial class CreatePurchaseContractPage : Page
    {
        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        public CreatePurchaseContractPage()
        {
            InitializeComponent();
        }



        // =====================================================
        // ENTER KEY SUBMIT
        // =====================================================

        private void MainGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                CreateContract_Click(CreateContractButton, new RoutedEventArgs());
            }
        }



        // =====================================================
        // CREATE CONTRACT
        // =====================================================

        private async void CreateContract_Click(
            object sender,
            RoutedEventArgs e)
        {
            Button? clickedButton = sender as Button;
            object? originalContent = clickedButton?.Content;

            try
            {
                // =============================================
                // MINIMAL LOADING EFFECT
                // =============================================

                Mouse.OverrideCursor = Cursors.Wait;
                MainGrid.IsEnabled = false;
                MainGrid.Opacity = 0.75;



                // =============================================
                // DISABLE BUTTON
                // =============================================

                if (clickedButton != null)
                {
                    clickedButton.IsEnabled = false;
                    clickedButton.Content = "Creating...";
                }



                // =============================================
                // REQUIRED FIELD VALIDATION
                // =============================================

                if (string.IsNullOrWhiteSpace(VendorNameBox.Text))
                {
                    MessageBox.Show(
                        "Vendor Name is required.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    VendorNameBox.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(VendorIDBox.Text))
                {
                    MessageBox.Show(
                        "Vendor ID is required.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    VendorIDBox.Focus();
                    return;
                }

                if (StartDatePicker.SelectedDate == null)
                {
                    MessageBox.Show(
                        "Please select a Start Date.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    StartDatePicker.Focus();
                    return;
                }

                if (EndDatePicker.SelectedDate == null)
                {
                    MessageBox.Show(
                        "Please select an End Date.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    EndDatePicker.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(BasePriceBox.Text))
                {
                    MessageBox.Show(
                        "Base Price is required.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    BasePriceBox.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(AgreedQuantityBox.Text))
                {
                    MessageBox.Show(
                        "Agreed Quantity is required.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    AgreedQuantityBox.Focus();
                    return;
                }



                // =============================================
                // TRIM VALUES
                // =============================================

                string vendorName = VendorNameBox.Text.Trim();
                string vendorID = VendorIDBox.Text.Trim();
                string itemCode = ItemCodeBox.Text.Trim();

                string penaltyRateText = PenaltyRateBox.Text.Trim();
                string remedyDaysText = RemedyDaysBox.Text.Trim();



                // =============================================
                // OPTIONAL VALUES DEFAULT TO ZERO
                // =============================================

                if (string.IsNullOrWhiteSpace(penaltyRateText))
                {
                    penaltyRateText = "0";
                }

                if (string.IsNullOrWhiteSpace(remedyDaysText))
                {
                    remedyDaysText = "0";
                }



                // =============================================
                // DATE VALIDATION
                // =============================================

                if (StartDatePicker.SelectedDate > EndDatePicker.SelectedDate)
                {
                    MessageBox.Show(
                        "End Date must be after Start Date.",
                        "Date Validation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }



                // =============================================
                // BASE PRICE VALIDATION
                // =============================================

                if (!decimal.TryParse(BasePriceBox.Text, out decimal basePrice))
                {
                    MessageBox.Show(
                        "Base Price must be a valid number.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    BasePriceBox.Focus();
                    return;
                }

                if (basePrice <= 0)
                {
                    MessageBox.Show(
                        "Base Price must be greater than zero.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    BasePriceBox.Focus();
                    return;
                }



                // =============================================
                // AGREED QUANTITY VALIDATION
                // =============================================

                if (!int.TryParse(AgreedQuantityBox.Text, out int agreedQuantity))
                {
                    MessageBox.Show(
                        "Agreed Quantity must be a whole number.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    AgreedQuantityBox.Focus();
                    return;
                }

                if (agreedQuantity <= 0)
                {
                    MessageBox.Show(
                        "Agreed Quantity must be greater than zero.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    AgreedQuantityBox.Focus();
                    return;
                }



                // =============================================
                // PENALTY RATE VALIDATION
                // =============================================

                penaltyRateText = penaltyRateText.Replace("%", "");

                if (!double.TryParse(penaltyRateText, out double penaltyRate))
                {
                    MessageBox.Show(
                        "Penalty Rate must be numeric.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    PenaltyRateBox.Focus();
                    return;
                }

                if (penaltyRate < 0 || penaltyRate > 100)
                {
                    MessageBox.Show(
                        "Penalty Rate must be between 0 and 100.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    PenaltyRateBox.Focus();
                    return;
                }



                // =============================================
                // REMEDY DAYS VALIDATION
                // =============================================

                if (!int.TryParse(remedyDaysText, out int remedyDays))
                {
                    MessageBox.Show(
                        "Remedy Days must be a whole number.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    RemedyDaysBox.Focus();
                    return;
                }

                if (remedyDays < 0)
                {
                    MessageBox.Show(
                        "Remedy Days cannot be negative.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    RemedyDaysBox.Focus();
                    return;
                }



                // =============================================
                // FORMAT DATES
                // =============================================

                string startDate =
                    StartDatePicker.SelectedDate?
                    .ToString("dd-MM-yyyy") ?? "";

                string endDate =
                    EndDatePicker.SelectedDate?
                    .ToString("dd-MM-yyyy") ?? "";



                // =============================================
                // PYTHON SCRIPT PATH
                // =============================================

                string pythonScript = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "backend",
                    "create_purchase_contract.py"
                );



                // =============================================
                // CHECK SCRIPT EXISTS
                // =============================================

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



                // =============================================
                // PYTHON COMMAND
                // =============================================

                string pythonExe = "python";



                // =============================================
                // BUILD ARGUMENTS
                // =============================================

                string arguments =
                    $"\"{vendorName}\" " +
                    $"\"{vendorID}\" " +
                    $"\"{itemCode}\" " +
                    $"\"{startDate}\" " +
                    $"\"{endDate}\" " +
                    $"\"{basePrice}\" " +
                    $"\"{agreedQuantity}\" " +
                    $"\"{penaltyRateText}\" " +
                    $"\"{remedyDaysText}\"";



                // =============================================
                // PROCESS INFO
                // =============================================

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



                // =============================================
                // RUN PROCESS
                // =============================================

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



                // =============================================
                // HANDLE ERRORS
                // =============================================

                if (!string.IsNullOrWhiteSpace(error))
                {
                    MessageBox.Show(
                        error,
                        "Backend Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return;
                }

                if (output.Contains("ERROR"))
                {
                    MessageBox.Show(
                        output,
                        "Backend Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return;
                }



                // =============================================
                // SUCCESS
                // =============================================

                MessageBox.Show(
                    output,
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                ClearForm();
            }



            // =============================================
            // APPLICATION ERROR
            // =============================================

            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Application Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }



            // =============================================
            // RESTORE UI
            // =============================================

            finally
            {
                Mouse.OverrideCursor = null;

                MainGrid.IsEnabled = true;
                MainGrid.Opacity = 1;

                if (clickedButton != null)
                {
                    clickedButton.IsEnabled = true;
                    clickedButton.Content = originalContent ?? "Create Contract";
                }
            }
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
            VendorNameBox.Clear();
            VendorIDBox.Clear();
            ItemCodeBox.Text = "NRFC";
            StartDatePicker.SelectedDate = null;
            EndDatePicker.SelectedDate = null;
            BasePriceBox.Clear();
            AgreedQuantityBox.Clear();
            PenaltyRateBox.Clear();
            RemedyDaysBox.Clear();
            VendorNameBox.Focus();
        }
    }
}