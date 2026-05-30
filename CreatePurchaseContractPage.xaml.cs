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
    public partial class CreatePurchaseContractPage : Page
    {
        private const string DefaultItemCode = "NRFC";

        public CreatePurchaseContractPage()
        {
            InitializeComponent();

            ItemCodeBox.Text = DefaultItemCode;
            ItemCodeBox.IsReadOnly = true;
        }

        private void MainGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                CreateContract_Click(CreateContractButton, new RoutedEventArgs());
            }
        }

        private async void CreateContract_Click(
            object sender,
            RoutedEventArgs e)
        {
            Button? clickedButton = sender as Button;
            object? originalContent = clickedButton?.Content;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                MainGrid.IsEnabled = false;
                MainGrid.Opacity = 0.75;

                if (clickedButton != null)
                {
                    clickedButton.IsEnabled = false;
                    clickedButton.Content = "Creating...";
                }

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

                string vendorName = VendorNameBox.Text.Trim();
                string vendorID = VendorIDBox.Text.Trim();
                string itemCode = DefaultItemCode;

                string penaltyRateText = PenaltyRateBox.Text.Trim();
                string remedyDaysText = RemedyDaysBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(penaltyRateText))
                {
                    penaltyRateText = "0";
                }

                if (string.IsNullOrWhiteSpace(remedyDaysText))
                {
                    remedyDaysText = "0";
                }

                DateTime startDateValue = StartDatePicker.SelectedDate.Value;
                DateTime endDateValue = EndDatePicker.SelectedDate.Value;

                if (endDateValue <= startDateValue)
                {
                    MessageBox.Show(
                        "End Date must be after Start Date.",
                        "Date Validation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    EndDatePicker.Focus();
                    return;
                }

                if (!decimal.TryParse(
                        BasePriceBox.Text.Trim(),
                        NumberStyles.Number,
                        CultureInfo.CurrentCulture,
                        out decimal basePrice))
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

                if (!decimal.TryParse(
                        AgreedQuantityBox.Text.Trim(),
                        NumberStyles.Number,
                        CultureInfo.CurrentCulture,
                        out decimal agreedQuantity))
                {
                    MessageBox.Show(
                        "Agreed Quantity must be a valid number.",
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

                penaltyRateText = penaltyRateText.Replace("%", "").Trim();

                if (!double.TryParse(
                        penaltyRateText,
                        NumberStyles.Number,
                        CultureInfo.CurrentCulture,
                        out double penaltyRate))
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

                if (!int.TryParse(
                        remedyDaysText,
                        NumberStyles.Integer,
                        CultureInfo.CurrentCulture,
                        out int remedyDays))
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

                string startDate =
                    startDateValue.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

                string endDate =
                    endDateValue.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

                string pythonScript = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "backend",
                    "create_purchase_contract.py"
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
                start.ArgumentList.Add(vendorName);
                start.ArgumentList.Add(vendorID);
                start.ArgumentList.Add(itemCode);
                start.ArgumentList.Add(startDate);
                start.ArgumentList.Add(endDate);
                start.ArgumentList.Add(basePrice.ToString(CultureInfo.InvariantCulture));
                start.ArgumentList.Add(agreedQuantity.ToString(CultureInfo.InvariantCulture));
                start.ArgumentList.Add(penaltyRate.ToString(CultureInfo.InvariantCulture));
                start.ArgumentList.Add(remedyDays.ToString(CultureInfo.InvariantCulture));

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

                    exitCode = process.ExitCode;
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

                    MessageBox.Show(
                        message,
                        "Backend Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return;
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    MessageBox.Show(
                        error.Trim(),
                        "Backend Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return;
                }

                if (output.TrimStart().StartsWith(
                        "ERROR",
                        StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        output.Trim(),
                        "Backend Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return;
                }

                MessageBox.Show(
                    output.Trim(),
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

                if (clickedButton != null)
                {
                    clickedButton.IsEnabled = true;
                    clickedButton.Content = originalContent ?? "Create Contract";
                }
            }
        }

        private void ClearForm_Click(
            object sender,
            RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            VendorNameBox.Clear();
            VendorIDBox.Clear();
            ItemCodeBox.Text = DefaultItemCode;
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