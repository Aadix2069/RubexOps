using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RubexOps
{
    public partial class EditProductionRecordWindow : Window
    {
        // =====================================================
        // FIELDS
        // =====================================================

        private readonly ProductionRecord currentRecord;



        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        public EditProductionRecordWindow(
            ProductionRecord record)
        {
            InitializeComponent();

            currentRecord = record;
            LoadRecordData();
        }



        // =====================================================
        // LOAD DATA
        // =====================================================

        private void LoadRecordData()
        {
            BatchIdText.Text = currentRecord.batch_id;

            if (DateTime.TryParseExact(
                    currentRecord.production_date,
                    "dd-MM-yyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime productionDate))
            {
                ProductionDatePicker.SelectedDate = productionDate;
            }

            SupplierNameBox.Text = currentRecord.supplier_name;
            SupplierIDBox.Text = currentRecord.supplier_id;
            InvoiceNumberBox.Text = currentRecord.invoice_number;
            QuantityAvailableBox.Text = FormatNumber(currentRecord.quantity_available);
            RawMaterialBox.Text = currentRecord.raw_material;
            RawMaterialCodeBox.Text = currentRecord.raw_material_code;
            FinishedProductBox.Text = currentRecord.finished_product;
            FinishedProductCodeBox.Text = currentRecord.finished_product_code;
            InputWeightBox.Text = FormatNumber(currentRecord.input_weight);
            OutputWeightBox.Text = FormatNumber(currentRecord.output_weight);
            InitialDrcBox.Text = FormatNumber(currentRecord.initial_drc);
            ActualDrcBox.Text = FormatNumber(currentRecord.actual_drc);

            UpdateLiveCalculations();
        }



        // =====================================================
        // LIVE CALCULATIONS
        // =====================================================

        private void CalculationInput_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            UpdateLiveCalculations();
        }

        private void UpdateLiveCalculations()
        {
            double available = ReadDoubleOrZero(QuantityAvailableBox.Text);
            double input = ReadDoubleOrZero(InputWeightBox.Text);
            double output = ReadDoubleOrZero(OutputWeightBox.Text);
            double initialDrc = ReadDoubleOrZero(InitialDrcBox.Text);
            double actualDrc = ReadDoubleOrZero(ActualDrcBox.Text);

            double remaining = available - input;
            double loss = input - output;
            double variance = actualDrc - initialDrc;

            RemainingText.Text = FormatNumber(remaining);
            LossVarianceText.Text =
                $"{FormatNumber(loss)} / {FormatNumber(variance)}%";
            CalculatedResultsBox.Text =
                $"Remaining: {FormatNumber(remaining)} | Loss: {FormatNumber(loss)} | DRC Variance: {FormatNumber(variance)}%";
        }



        // =====================================================
        // SAVE
        // =====================================================

        private void SaveButton_Click(
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
                    button.Content = "Saving...";
                }

                if (!ValidateForm(
                        out double inputWeight,
                        out double outputWeight,
                        out double initialDrc,
                        out double actualDrc))
                {
                    return;
                }

                string pythonScript = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "backend",
                    "update_production_record.py");

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
                    Quote(currentRecord.row.ToString(CultureInfo.InvariantCulture)) + " " +
                    Quote(ProductionDatePicker.SelectedDate?.ToString("dd-MM-yyyy") ?? "") + " " +
                    Quote(SupplierNameBox.Text.Trim()) + " " +
                    Quote(SupplierIDBox.Text.Trim()) + " " +
                    Quote(InvoiceNumberBox.Text.Trim()) + " " +
                    Quote(RawMaterialBox.Text.Trim()) + " " +
                    Quote(RawMaterialCodeBox.Text.Trim()) + " " +
                    Quote(FinishedProductBox.Text.Trim()) + " " +
                    Quote(FinishedProductCodeBox.Text.Trim()) + " " +
                    Quote(inputWeight.ToString(CultureInfo.InvariantCulture)) + " " +
                    Quote(outputWeight.ToString(CultureInfo.InvariantCulture)) + " " +
                    Quote(initialDrc.ToString(CultureInfo.InvariantCulture)) + " " +
                    Quote(actualDrc.ToString(CultureInfo.InvariantCulture));

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

                using Process process =
                    Process.Start(start)
                    ?? throw new Exception("Failed to start backend process.");

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(error))
                {
                    MessageBox.Show(
                        !string.IsNullOrWhiteSpace(error) ? error.Trim() : output.Trim(),
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

                if (sender is Button button)
                {
                    button.IsEnabled = true;
                    button.Content = "Save Changes";
                }
            }
        }



        // =====================================================
        // VALIDATION
        // =====================================================

        private bool ValidateForm(
            out double inputWeight,
            out double outputWeight,
            out double initialDrc,
            out double actualDrc)
        {
            inputWeight = 0;
            outputWeight = 0;
            initialDrc = 0;
            actualDrc = 0;

            if (ProductionDatePicker.SelectedDate == null)
            {
                ShowValidation("Please select a Production Date.", ProductionDatePicker);
                return false;
            }

            if (string.IsNullOrWhiteSpace(FinishedProductBox.Text))
            {
                ShowValidation("Finished Product is required.", FinishedProductBox);
                return false;
            }

            if (string.IsNullOrWhiteSpace(FinishedProductCodeBox.Text))
            {
                ShowValidation("Finished Product Code is required.", FinishedProductCodeBox);
                return false;
            }

            double available = ReadDoubleOrZero(QuantityAvailableBox.Text);

            if (!TryReadDouble(InputWeightBox.Text, out inputWeight) || inputWeight <= 0)
            {
                ShowValidation("Input Weight must be greater than zero.", InputWeightBox);
                return false;
            }

            if (inputWeight > available)
            {
                ShowValidation("Input Weight cannot exceed Quantity Available.", InputWeightBox);
                return false;
            }

            if (!TryReadDouble(OutputWeightBox.Text, out outputWeight) || outputWeight < 0)
            {
                ShowValidation("Output Weight cannot be negative.", OutputWeightBox);
                return false;
            }

            if (outputWeight > inputWeight)
            {
                ShowValidation("Output Weight cannot exceed Input Weight.", OutputWeightBox);
                return false;
            }

            if (!TryReadDouble(InitialDrcBox.Text, out initialDrc) ||
                initialDrc < 0 ||
                initialDrc > 100)
            {
                ShowValidation("Initial DRC must be between 0 and 100.", InitialDrcBox);
                return false;
            }

            if (!TryReadDouble(ActualDrcBox.Text, out actualDrc) ||
                actualDrc < 0 ||
                actualDrc > 100)
            {
                ShowValidation("Actual DRC must be between 0 and 100.", ActualDrcBox);
                return false;
            }

            return true;
        }



        // =====================================================
        // HELPERS
        // =====================================================

        private static bool TryReadDouble(
            string text,
            out double value)
        {
            string cleanText =
                text.Trim()
                    .Replace(",", "")
                    .Replace("%", "");

            return double.TryParse(
                cleanText,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static double ReadDoubleOrZero(
            string text)
        {
            return TryReadDouble(text, out double value) ? value : 0;
        }

        private static string FormatNumber(
            double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
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

        private void CancelButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }
    }
}

