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
    public partial class EnterProductionDataPage : Page
    {
        // =====================================================
        // DATA MODELS
        // =====================================================

        private sealed class ProductionSourceResponse
        {
            public string next_batch_id { get; set; } = "BATCH-0001";

            public List<ProductionSourceOption> sources { get; set; } = new();
        }

        private sealed class ProductionSourceOption
        {
            public string SupplierName { get; set; } = "";

            public string SupplierID { get; set; } = "";

            public string InvoiceNumber { get; set; } = "";

            public string RawMaterial { get; set; } = "";

            public string RawMaterialCode { get; set; } = "";

            public double QuantityPurchased { get; set; }

            public double QuantityConsumed { get; set; }

            public double QuantityAvailable { get; set; }

            public string SourceDisplay =>
                $"{InvoiceNumber} | {SupplierID} | {RawMaterialCode}";

            public string AvailableDisplay =>
                $"Available: {QuantityAvailable:0.##} kg";
        }



        // =====================================================
        // FIELDS
        // =====================================================

        private readonly List<ProductionSourceOption> sourceOptions = new();

        private ProductionSourceOption selectedSource;

        private bool isSelectingSource;



        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        public EnterProductionDataPage()
        {
            InitializeComponent();

            Loaded += EnterProductionDataPage_Loaded;
        }



        // =====================================================
        // PAGE LOAD
        // =====================================================

        private async void EnterProductionDataPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            ProductionDatePicker.SelectedDate = DateTime.Today;

            await LoadProductionSources();
        }



        // =====================================================
        // LOAD SOURCES
        // =====================================================

        private async Task LoadProductionSources()
        {
            try
            {
                string pythonScript = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "backend",
                    "read_production_sources.py");

                if (!File.Exists(pythonScript))
                {
                    MessageBox.Show(
                        "Production source backend file not found.\n\n" + pythonScript,
                        "File Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                string output = await RunPythonScript(pythonScript, "");

                ProductionSourceResponse response =
                    JsonSerializer.Deserialize<ProductionSourceResponse>(
                        output,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        })
                    ?? new ProductionSourceResponse();

                BatchIdBox.Text = response.next_batch_id;
                BatchIdMetricText.Text = response.next_batch_id;

                sourceOptions.Clear();
                sourceOptions.AddRange(response.sources ?? new List<ProductionSourceOption>());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Production Source Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }



        // =====================================================
        // SOURCE SEARCH
        // =====================================================

        private void SourceSearchBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            if (isSelectingSource)
            {
                return;
            }

            selectedSource = null;
            ClearSourceFields();

            string search = SourceSearchBox.Text.Trim();

            if (search.Length == 0)
            {
                SourceSuggestionPopup.IsOpen = false;
                return;
            }

            List<ProductionSourceOption> matches =
                sourceOptions.FindAll(source =>
                    source.SupplierName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    source.SupplierID.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    source.InvoiceNumber.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    source.RawMaterial.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    source.RawMaterialCode.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);

            SourceSuggestionList.ItemsSource = matches;
            SourceSuggestionPopup.IsOpen = matches.Count > 0;
        }

        private void SourceSuggestionList_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (SourceSuggestionList.SelectedItem is ProductionSourceOption source)
            {
                ApplySelectedSource(source);
            }
        }

        private void ApplySelectedSource(
            ProductionSourceOption source)
        {
            selectedSource = source;
            isSelectingSource = true;

            SourceSearchBox.Text =
                $"{source.SupplierName} - {source.InvoiceNumber}";

            SupplierNameBox.Text = source.SupplierName;
            SupplierIDBox.Text = source.SupplierID;
            InvoiceNumberBox.Text = source.InvoiceNumber;
            RawMaterialBox.Text = source.RawMaterial;
            RawMaterialCodeBox.Text = source.RawMaterialCode;
            QuantityAvailableBox.Text = source.QuantityAvailable.ToString("0.##", CultureInfo.InvariantCulture);

            SourceSuggestionPopup.IsOpen = false;
            SourceSuggestionList.SelectedItem = null;
            isSelectingSource = false;

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

            RemainingQuantityBox.Text = remaining.ToString("0.##", CultureInfo.InvariantCulture);
            ProductionLossBox.Text = loss.ToString("0.##", CultureInfo.InvariantCulture);
            DrcVarianceBox.Text = variance.ToString("0.##", CultureInfo.InvariantCulture) + "%";

            RemainingMetricText.Text = RemainingQuantityBox.Text;
            LossMetricText.Text = ProductionLossBox.Text;
            VarianceMetricText.Text = DrcVarianceBox.Text;
        }



        // =====================================================
        // SAVE
        // =====================================================

        private async void SaveProduction_Click(
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
                    "create_production_entry.py");

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

                string output = await RunPythonScript(pythonScript, arguments);

                MessageBox.Show(
                    output,
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                ClearForm();
                await LoadProductionSources();
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
                    button.Content = "Save Production";
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

            if (selectedSource == null)
            {
                ShowValidation("Please select a valid purchase source.", SourceSearchBox);
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
                throw new Exception(
                    !string.IsNullOrWhiteSpace(error)
                        ? error.Trim()
                        : output.Trim());
            }

            return output.Trim();
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

        private void ClearSourceFields()
        {
            SupplierNameBox.Clear();
            SupplierIDBox.Clear();
            InvoiceNumberBox.Clear();
            RawMaterialBox.Clear();
            RawMaterialCodeBox.Clear();
            QuantityAvailableBox.Clear();
            UpdateLiveCalculations();
        }

        private void ClearForm_Click(
            object sender,
            RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            selectedSource = null;
            SourceSearchBox.Clear();
            ClearSourceFields();
            ProductionDatePicker.SelectedDate = DateTime.Today;
            FinishedProductBox.Text = "Processed Rubber";
            FinishedProductCodeBox.Text = "PRD";
            InputWeightBox.Clear();
            OutputWeightBox.Clear();
            InitialDrcBox.Clear();
            ActualDrcBox.Clear();
            SourceSuggestionPopup.IsOpen = false;
            UpdateLiveCalculations();
            SourceSearchBox.Focus();
        }
    }
}

