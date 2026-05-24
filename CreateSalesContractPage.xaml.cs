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
    public partial class CreateSalesContractPage : Page
    {
        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        public CreateSalesContractPage()
        {
            InitializeComponent();
        }



        // =====================================================
        // CREATE CONTRACT
        // =====================================================

        private async void CreateContract_Click(
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
                    button.Content = "Creating...";
                }

                if (!ValidateForm(
                        out decimal basePrice,
                        out decimal agreedQuantity,
                        out decimal penaltyRate,
                        out int remedyDays))
                {
                    return;
                }

                string startDate =
                    StartDatePicker.SelectedDate?.ToString("dd-MM-yyyy") ?? "";

                string endDate =
                    EndDatePicker.SelectedDate?.ToString("dd-MM-yyyy") ?? "";

                string pythonScript = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "backend",
                    "create_sales_contract.py");

                if (!File.Exists(pythonScript))
                {
                    MessageBox.Show(
                        "Backend Python file not found.\n\n" + pythonScript,
                        "File Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return;
                }

                string arguments =
                    Quote(CustomerNameBox.Text.Trim()) + " " +
                    Quote(CustomerIDBox.Text.Trim()) + " " +
                    Quote(ItemCodeBox.Text.Trim()) + " " +
                    Quote(startDate) + " " +
                    Quote(endDate) + " " +
                    Quote(basePrice.ToString(CultureInfo.InvariantCulture)) + " " +
                    Quote(agreedQuantity.ToString(CultureInfo.InvariantCulture)) + " " +
                    Quote(penaltyRate.ToString(CultureInfo.InvariantCulture)) + " " +
                    Quote(remedyDays.ToString(CultureInfo.InvariantCulture));

                string output = await RunPythonScript(pythonScript, arguments);

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

                if (sender is Button button)
                {
                    button.IsEnabled = true;
                    button.Content = "Create Contract";
                }
            }
        }



        // =====================================================
        // VALIDATION
        // =====================================================

        private bool ValidateForm(
            out decimal basePrice,
            out decimal agreedQuantity,
            out decimal penaltyRate,
            out int remedyDays)
        {
            basePrice = 0;
            agreedQuantity = 0;
            penaltyRate = 0;
            remedyDays = 0;

            if (string.IsNullOrWhiteSpace(CustomerNameBox.Text))
            {
                ShowValidation("Customer Name is required.", CustomerNameBox);
                return false;
            }

            if (string.IsNullOrWhiteSpace(CustomerIDBox.Text))
            {
                ShowValidation("Customer ID is required.", CustomerIDBox);
                return false;
            }

            if (StartDatePicker.SelectedDate == null)
            {
                ShowValidation("Please select a Start Date.", StartDatePicker);
                return false;
            }

            if (EndDatePicker.SelectedDate == null)
            {
                ShowValidation("Please select an End Date.", EndDatePicker);
                return false;
            }

            if (StartDatePicker.SelectedDate >= EndDatePicker.SelectedDate)
            {
                ShowValidation("End Date must be after Start Date.", EndDatePicker);
                return false;
            }

            if (!TryReadDecimal(BasePriceBox.Text, out basePrice) || basePrice <= 0)
            {
                ShowValidation("Base Price must be greater than zero.", BasePriceBox);
                return false;
            }

            if (!TryReadDecimal(AgreedQuantityBox.Text, out agreedQuantity) ||
                agreedQuantity <= 0)
            {
                ShowValidation("Agreed Quantity must be greater than zero.", AgreedQuantityBox);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(PenaltyRateBox.Text))
            {
                if (!TryReadDecimal(PenaltyRateBox.Text, out penaltyRate) ||
                    penaltyRate < 0 ||
                    penaltyRate > 100)
                {
                    ShowValidation("Penalty / Discount must be between 0 and 100.", PenaltyRateBox);
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(RemedyDaysBox.Text))
            {
                if (!int.TryParse(
                        RemedyDaysBox.Text.Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out remedyDays) ||
                    remedyDays < 0)
                {
                    ShowValidation("Remedy Days must be a non-negative whole number.", RemedyDaysBox);
                    return false;
                }
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
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = "python",
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
            int exitCode = 0;

            await Task.Run(() =>
            {
                using Process process =
                    Process.Start(start)
                    ?? throw new Exception("Failed to start backend process.");

                output = process.StandardOutput.ReadToEnd();
                error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                exitCode = process.ExitCode;
            });

            if (exitCode != 0 || !string.IsNullOrWhiteSpace(error))
            {
                string message =
                    !string.IsNullOrWhiteSpace(error)
                        ? error.Trim()
                        : output.Trim();

                throw new Exception(message);
            }

            return output.Trim();
        }



        // =====================================================
        // HELPERS
        // =====================================================

        private static bool TryReadDecimal(
            string text,
            out decimal value)
        {
            string cleanText =
                text.Trim()
                    .Replace(",", "")
                    .Replace("%", "");

            return decimal.TryParse(
                cleanText,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static string Quote(
            string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static void ShowValidation(
            string message,
            Control control)
        {
            MessageBox.Show(
                message,
                "Validation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            control.Focus();
        }



        // =====================================================
        // CLEAR FORM
        // =====================================================

        private void ClearForm_Click(
            object sender,
            RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            CustomerNameBox.Clear();
            CustomerIDBox.Clear();
            ItemCodeBox.Text = "NRFC";
            StartDatePicker.SelectedDate = null;
            EndDatePicker.SelectedDate = null;
            BasePriceBox.Clear();
            AgreedQuantityBox.Clear();
            PenaltyRateBox.Clear();
            RemedyDaysBox.Clear();
            CustomerNameBox.Focus();
        }
    }
}

