#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RubexOps
{
    public partial class EnterProductionDataPage : Page
    {
        private const string DateFormat = "dd-MM-yyyy";
        private const int PythonTimeoutMilliseconds = 30000;

        private readonly List<PurchaseRecord> _allPurchases = new List<PurchaseRecord>();
        private readonly ObservableCollection<PurchaseRecord> _invoiceSuggestions = new ObservableCollection<PurchaseRecord>();
        private PurchaseRecord _selectedPurchase;
        private bool _isLoading;
        private bool _isSaving;
        private bool _suppressInvoiceTextChanged;
        private string _backendDirectory;

        public EnterProductionDataPage()
        {
            InitializeComponent();
            SuggestionListBox.ItemsSource = _invoiceSuggestions;
            ProductionDatePicker.SelectedDate = DateTime.Today;
            ResetProductionDisplay();
            UpdateSaveState();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadPurchasesAsync();
        }

        private async Task LoadPurchasesAsync()
        {
            _isLoading = true;
            SetFooterStatus("Loading purchase invoices...");
            SetInvoiceStatus("Loading purchase invoices from backend.", StatusTone.Neutral);

            PythonResult result = await RunPythonScriptAsync("read_purchase_data.py");
            if (!result.Success)
            {
                _allPurchases.Clear();
                _invoiceSuggestions.Clear();
                SetInvoiceStatus(CleanBackendError(result), StatusTone.Error);
                SetFooterStatus("Purchase lookup unavailable.");
                _isLoading = false;
                UpdateSaveState();
                return;
            }

            try
            {
                List<PurchaseRecord> loaded = ParsePurchaseRows(result.StandardOutput)
                    .Where(purchase => !string.IsNullOrWhiteSpace(purchase.InvoiceNumber))
                    .OrderBy(purchase => purchase.InvoiceNumber, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _allPurchases.Clear();
                _allPurchases.AddRange(loaded);

                if (_allPurchases.Count == 0)
                {
                    SetInvoiceStatus("No purchase invoices are available for production.", StatusTone.Warning);
                    SetFooterStatus("No invoices loaded.");
                }
                else
                {
                    SetInvoiceStatus(string.Format(CultureInfo.InvariantCulture, "{0} purchase invoices loaded.", _allPurchases.Count), StatusTone.Success);
                    SetFooterStatus("Ready.");
                }
            }
            catch (Exception ex)
            {
                _allPurchases.Clear();
                _invoiceSuggestions.Clear();
                SetInvoiceStatus("Could not read purchase invoice data: " + ex.Message, StatusTone.Error);
                SetFooterStatus("Purchase data parse failed.");
            }
            finally
            {
                _isLoading = false;
                UpdateSuggestions(openPopup: false);
                UpdateSaveState();
            }
        }

        private void SearchInvoiceTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressInvoiceTextChanged)
                return;

            _selectedPurchase = null;
            ClearSourceDisplay();
            ResetCalculatedDisplay();
            UpdateSuggestions(openPopup: SearchInvoiceTextBox.IsKeyboardFocusWithin);
            UpdateSaveState();
        }

        private async void SearchInvoiceTextBox_KeyDown(object sender, KeyEventArgs e)
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
                if (SuggestionPopup.IsOpen && _invoiceSuggestions.Count > 0)
                {
                    await SelectHighlightedSuggestionAsync();
                }
                else
                {
                    await TrySaveFromEnterAsync();
                }

                e.Handled = true;
            }
        }

        private async void OutputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await TrySaveFromEnterAsync();
                e.Handled = true;
            }
        }

        private async void Page_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !SuggestionPopup.IsOpen)
            {
                await TrySaveFromEnterAsync();
                e.Handled = true;
            }
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await SelectBestSearchMatchAsync();
        }

        private void ClearInvoiceButton_Click(object sender, RoutedEventArgs e)
        {
            _suppressInvoiceTextChanged = true;
            SearchInvoiceTextBox.Text = string.Empty;
            _suppressInvoiceTextChanged = false;

            _selectedPurchase = null;
            SuggestionPopup.IsOpen = false;
            _invoiceSuggestions.Clear();
            ReviewCheckBox.IsChecked = false;
            ClearSourceDisplay();
            ResetCalculatedDisplay();
            SetInvoiceStatus(_allPurchases.Count == 0 ? "No purchase invoices loaded." : "Select a purchase invoice to begin.", StatusTone.Neutral);
            SetFooterStatus("Ready.");
            UpdateSaveState();
            SearchInvoiceTextBox.Focus();
        }

        private async void SuggestionListBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            await SelectHighlightedSuggestionAsync();
        }

        private async void SuggestionListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            await SelectHighlightedSuggestionAsync();
        }

        private async void SuggestionListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await SelectHighlightedSuggestionAsync();
                e.Handled = true;
            }
        }

        private void ProductionDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCalculatedDisplay();
            UpdateSaveState();
        }

        private void OutputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateCalculatedDisplay();
            UpdateSaveState();
        }

        private void ReviewCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateSaveState();
        }

        private async void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPurchase != null && string.IsNullOrWhiteSpace(_selectedPurchase.BatchIdPreview))
                await RefreshProductionProbeAsync(_selectedPurchase);

            string validation = ValidateProductionInputs(requireReview: false);
            if (!string.IsNullOrWhiteSpace(validation))
            {
                SetFooterStatus(validation);
                InputValidationText.Text = validation;
                return;
            }

            SetFooterStatus("Batch preview is ready.");
        }

        private async void ConfirmProductionButton_Click(object sender, RoutedEventArgs e)
        {
            await SaveProductionAsync();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null && NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        private void UpdateSuggestions(bool openPopup)
        {
            string query = (SearchInvoiceTextBox.Text ?? string.Empty).Trim();
            _invoiceSuggestions.Clear();

            if (string.IsNullOrWhiteSpace(query) || _allPurchases.Count == 0)
            {
                SuggestionPopup.IsOpen = false;
                return;
            }

            List<PurchaseRecord> matches = _allPurchases
                .Where(purchase => purchase.InvoiceNumber.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                .Take(10)
                .ToList();

            foreach (PurchaseRecord match in matches)
                _invoiceSuggestions.Add(match);

            SuggestionListBox.SelectedIndex = _invoiceSuggestions.Count > 0 ? 0 : -1;
            SuggestionPopup.IsOpen = openPopup && _invoiceSuggestions.Count > 0;

            if (_invoiceSuggestions.Count == 0)
                SetInvoiceStatus("No purchase invoice starts with \"" + query + "\".", StatusTone.Warning);
        }

        private void MoveSuggestionSelection(int direction)
        {
            if (_invoiceSuggestions.Count == 0)
            {
                UpdateSuggestions(openPopup: true);
                return;
            }

            if (!SuggestionPopup.IsOpen)
                SuggestionPopup.IsOpen = true;

            int nextIndex = SuggestionListBox.SelectedIndex;
            if (nextIndex < 0)
                nextIndex = direction > 0 ? 0 : _invoiceSuggestions.Count - 1;
            else
                nextIndex += direction;

            if (nextIndex < 0)
                nextIndex = _invoiceSuggestions.Count - 1;
            if (nextIndex >= _invoiceSuggestions.Count)
                nextIndex = 0;

            SuggestionListBox.SelectedIndex = nextIndex;
            SuggestionListBox.ScrollIntoView(SuggestionListBox.SelectedItem);
        }

        private async Task SelectHighlightedSuggestionAsync()
        {
            PurchaseRecord selected = SuggestionListBox.SelectedItem as PurchaseRecord;
            if (selected == null && _invoiceSuggestions.Count > 0)
                selected = _invoiceSuggestions[0];

            if (selected != null)
                await SelectPurchaseAsync(selected);
        }

        private async Task SelectBestSearchMatchAsync()
        {
            string query = (SearchInvoiceTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                SetInvoiceStatus("Enter a purchase invoice number to search.", StatusTone.Warning);
                return;
            }

            PurchaseRecord exact = _allPurchases.FirstOrDefault(p =>
                string.Equals(p.InvoiceNumber, query, StringComparison.OrdinalIgnoreCase));

            if (exact != null)
            {
                await SelectPurchaseAsync(exact);
                return;
            }

            PurchaseRecord firstPrefix = _allPurchases.FirstOrDefault(p =>
                p.InvoiceNumber.StartsWith(query, StringComparison.OrdinalIgnoreCase));

            if (firstPrefix != null)
            {
                await SelectPurchaseAsync(firstPrefix);
                return;
            }

            SetInvoiceStatus("No purchase invoice starts with \"" + query + "\".", StatusTone.Warning);
        }

        private async Task SelectPurchaseAsync(PurchaseRecord purchase)
        {
            _selectedPurchase = purchase;

            _suppressInvoiceTextChanged = true;
            SearchInvoiceTextBox.Text = purchase.InvoiceNumber;
            SearchInvoiceTextBox.CaretIndex = SearchInvoiceTextBox.Text.Length;
            _suppressInvoiceTextChanged = false;

            SuggestionPopup.IsOpen = false;
            _invoiceSuggestions.Clear();
            ReviewCheckBox.IsChecked = false;

            FillSourceDisplay(purchase);
            UpdateCalculatedDisplay();
            SetInvoiceStatus("Invoice " + purchase.InvoiceNumber + " selected. Verifying production availability...", StatusTone.Neutral);
            await RefreshProductionProbeAsync(purchase);
            UpdateCalculatedDisplay();
            UpdateSaveState();
        }

        private async Task RefreshProductionProbeAsync(PurchaseRecord purchase)
        {
            if (purchase == null)
                return;

            string code =
                "import json, sys\n" +
                "from database import production_invoice_exists, generate_production_batch_id\n" +
                "invoice = sys.argv[1]\n" +
                "contract_id = sys.argv[2]\n" +
                "used = production_invoice_exists(invoice)\n" +
                "batch_id = '' if used else generate_production_batch_id(contract_id)\n" +
                "print(json.dumps({'invoice_used': used, 'batch_id': batch_id}))\n";

            PythonResult result = await RunPythonInlineAsync(code, purchase.InvoiceNumber, purchase.ContractId);
            if (!result.Success)
            {
                purchase.InvoiceUsed = false;
                purchase.BatchIdPreview = string.Empty;
                BatchIdText.Text = "Generated on save";
                SummaryBatchText.Text = "Generated on save";
                SetInvoiceStatus("Invoice selected. Batch preview could not be verified by Python: " + CleanBackendError(result), StatusTone.Warning);
                return;
            }

            try
            {
                ProductionProbe probe = ParseProductionProbe(result.StandardOutput);
                purchase.InvoiceUsed = probe.InvoiceUsed;
                purchase.BatchIdPreview = probe.BatchId;

                if (purchase.InvoiceUsed)
                {
                    BatchIdText.Text = "Invoice already used";
                    SummaryBatchText.Text = "-";
                    SetInvoiceStatus("This purchase invoice has already been used for a production batch.", StatusTone.Error);
                }
                else
                {
                    string batchText = string.IsNullOrWhiteSpace(purchase.BatchIdPreview) ? "Generated on save" : purchase.BatchIdPreview;
                    BatchIdText.Text = batchText;
                    SummaryBatchText.Text = batchText;
                    SetInvoiceStatus("Invoice verified for production.", StatusTone.Success);
                }
            }
            catch (Exception ex)
            {
                purchase.InvoiceUsed = false;
                purchase.BatchIdPreview = string.Empty;
                BatchIdText.Text = "Generated on save";
                SummaryBatchText.Text = "Generated on save";
                SetInvoiceStatus("Invoice selected. Production verification returned unreadable data: " + ex.Message, StatusTone.Warning);
            }
        }

        private async Task TrySaveFromEnterAsync()
        {
            if (ConfirmProductionButton.IsEnabled)
                await SaveProductionAsync();
        }

        private async Task SaveProductionAsync()
        {
            if (_isSaving)
                return;

            string validation = ValidateProductionInputs(requireReview: true);
            if (!string.IsNullOrWhiteSpace(validation))
            {
                InputValidationText.Text = validation;
                SetFooterStatus(validation);
                UpdateSaveState();
                return;
            }

            _isSaving = true;
            ConfirmProductionButton.Content = "Saving...";
            UpdateSaveState();

            DateTime productionDate = ProductionDatePicker.SelectedDate.Value;
            double output = ParseQuantity(OutputTextBox.Text);

            PythonResult result = await RunPythonScriptAsync(
                "save_production.py",
                productionDate.ToString(DateFormat, CultureInfo.InvariantCulture),
                _selectedPurchase.InvoiceNumber,
                output.ToString("0.##", CultureInfo.InvariantCulture));

            _isSaving = false;
            ConfirmProductionButton.Content = "Confirm Production";

            if (!result.Success)
            {
                string message = CleanBackendError(result);
                InputValidationText.Text = message;
                SetFooterStatus("Production was not saved.");
                SetInvoiceStatus(message, StatusTone.Error);
                UpdateSaveState();
                return;
            }

            _selectedPurchase.InvoiceUsed = true;
            string savedBatch = ExtractBatchIdFromSaveOutput(result.StandardOutput);
            if (!string.IsNullOrWhiteSpace(savedBatch))
            {
                _selectedPurchase.BatchIdPreview = savedBatch;
                BatchIdText.Text = savedBatch;
                SummaryBatchText.Text = savedBatch;
            }

            SetInvoiceStatus(result.StandardOutput.Trim(), StatusTone.Success);
            SetFooterStatus("Production batch saved.");
            ReviewCheckBox.IsChecked = false;
            UpdateSaveState();
        }

        private string ValidateProductionInputs(bool requireReview)
        {
            if (_isLoading)
                return "Purchase invoices are still loading.";

            if (_selectedPurchase == null)
                return "Select a valid purchase invoice.";

            if (_selectedPurchase.InvoiceUsed)
                return "This purchase invoice has already been used for production.";

            if (ProductionDatePicker.SelectedDate == null)
                return "Production Date is required.";

            DateTime productionDate = ProductionDatePicker.SelectedDate.Value.Date;
            DateTime purchaseDate;
            if (TryParseDisplayDate(_selectedPurchase.PurchaseDateText, out purchaseDate) && productionDate < purchaseDate.Date)
                return "Production Date cannot be earlier than the Purchase Order Date.";

            double input = _selectedPurchase.InputQuantity;
            if (input <= 0)
                return "Input Quantity could not be determined from the selected purchase invoice.";

            double output = ParseQuantity(OutputTextBox.Text);
            if (output <= 0)
                return "Output Weight must be greater than zero.";

            if (output > input)
                return "Output Weight cannot be greater than the purchase input quantity.";

            if (requireReview && ReviewCheckBox.IsChecked != true)
                return "Review the final summary before confirming production.";

            return string.Empty;
        }

        private void FillSourceDisplay(PurchaseRecord purchase)
        {
            VendorNameText.Text = EmptyIfMissing(purchase.VendorName);
            VendorIdText.Text = EmptyIfMissing(purchase.VendorId);
            ContractIdText.Text = EmptyIfMissing(purchase.ContractId);
            PurchaseInvoiceText.Text = EmptyIfMissing(purchase.InvoiceNumber);
            PurchaseDateText.Text = EmptyIfMissing(purchase.PurchaseDateText);
            SourceInputQuantityText.Text = FormatKg(purchase.InputQuantity);

            SummaryInvoiceText.Text = EmptyIfMissing(purchase.InvoiceNumber);
            SummaryVendorText.Text = EmptyIfMissing(purchase.VendorName);
            SummaryContractText.Text = EmptyIfMissing(purchase.ContractId);
            SummaryInputText.Text = FormatKg(purchase.InputQuantity);
            SummaryProductionInputText.Text = FormatKg(purchase.InputQuantity);

            MetricInputText.Text = FormatKg(purchase.InputQuantity);
            MetricInitialDrcText.Text = FormatPercent(purchase.InitialDrc);
            SummaryInitialDrcText.Text = FormatPercent(purchase.InitialDrc);
            ImpactInputText.Text = FormatKg(purchase.InputQuantity);

            string batchText = string.IsNullOrWhiteSpace(purchase.BatchIdPreview) ? "Generated on save" : purchase.BatchIdPreview;
            BatchIdText.Text = batchText;
            SummaryBatchText.Text = batchText;
        }

        private void ClearSourceDisplay()
        {
            VendorNameText.Text = "-";
            VendorIdText.Text = "-";
            ContractIdText.Text = "-";
            PurchaseInvoiceText.Text = "-";
            PurchaseDateText.Text = "-";
            SourceInputQuantityText.Text = "-";
            BatchIdText.Text = "Select invoice";

            SummaryInvoiceText.Text = "-";
            SummaryVendorText.Text = "-";
            SummaryContractText.Text = "-";
            SummaryInputText.Text = "-";
            SummaryProductionInputText.Text = "-";
            SummaryBatchText.Text = "-";
            MetricInputText.Text = "-";
            MetricInitialDrcText.Text = "-";
            SummaryInitialDrcText.Text = "-";
            ImpactInputText.Text = "-";
        }

        private void ResetCalculatedDisplay()
        {
            MetricOutputText.Text = "-";
            MetricLossText.Text = "-";
            MetricActualDrcText.Text = "-";
            MetricVarianceText.Text = "-";
            SummaryDateText.Text = ProductionDatePicker.SelectedDate.HasValue
                ? ProductionDatePicker.SelectedDate.Value.ToString(DateFormat, CultureInfo.InvariantCulture)
                : "-";
            SummaryOutputText.Text = "-";
            SummaryLossText.Text = "-";
            SummaryActualDrcText.Text = "-";
            SummaryVarianceText.Text = "-";
            ImpactOutputText.Text = "-";
            InputValidationText.Text = string.Empty;
        }

        private void ResetProductionDisplay()
        {
            ClearSourceDisplay();
            ResetCalculatedDisplay();
            SetInvoiceStatus("Select a purchase invoice to begin.", StatusTone.Neutral);
            SetFooterStatus("Ready.");
        }

        private void UpdateCalculatedDisplay()
        {
            if (_selectedPurchase == null)
            {
                ResetCalculatedDisplay();
                return;
            }

            FillSourceDisplay(_selectedPurchase);

            SummaryDateText.Text = ProductionDatePicker.SelectedDate.HasValue
                ? ProductionDatePicker.SelectedDate.Value.ToString(DateFormat, CultureInfo.InvariantCulture)
                : "-";

            double input = _selectedPurchase.InputQuantity;
            double output = ParseQuantity(OutputTextBox.Text);
            double loss = input - output;
            double actualDrc = input > 0 && output > 0 ? (output / input) * 100.0 : 0.0;
            double variance = actualDrc - _selectedPurchase.InitialDrc;

            MetricOutputText.Text = output > 0 ? FormatKg(output) : "-";
            MetricLossText.Text = output > 0 ? FormatKg(Math.Max(loss, 0.0)) : "-";
            MetricActualDrcText.Text = actualDrc > 0 ? FormatPercent(actualDrc) : "-";
            MetricVarianceText.Text = actualDrc > 0 ? FormatSignedPercent(variance) : "-";

            SummaryOutputText.Text = output > 0 ? FormatKg(output) : "-";
            SummaryLossText.Text = output > 0 ? FormatKg(Math.Max(loss, 0.0)) : "-";
            SummaryActualDrcText.Text = actualDrc > 0 ? FormatPercent(actualDrc) : "-";
            SummaryVarianceText.Text = actualDrc > 0 ? FormatSignedPercent(variance) : "-";
            ImpactOutputText.Text = output > 0 ? "+" + FormatKg(output) : "-";

            string validation = ValidateProductionInputs(requireReview: false);
            InputValidationText.Text = validation;
        }

        private void UpdateSaveState()
        {
            ConfirmProductionButton.IsEnabled = !_isSaving && string.IsNullOrWhiteSpace(ValidateProductionInputs(requireReview: true));
        }

        private void SetInvoiceStatus(string message, StatusTone tone)
        {
            InvoiceStatusBorder.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
            InvoiceStatusText.Text = message ?? string.Empty;

            switch (tone)
            {
                case StatusTone.Success:
                    InvoiceStatusBorder.Background = BrushFromHex("#E8F7F4");
                    InvoiceStatusBorder.BorderBrush = BrushFromHex("#BDE7E1");
                    InvoiceStatusText.Foreground = BrushFromHex("#0D625E");
                    break;
                case StatusTone.Warning:
                    InvoiceStatusBorder.Background = BrushFromHex("#FFF8EF");
                    InvoiceStatusBorder.BorderBrush = BrushFromHex("#F2D2AF");
                    InvoiceStatusText.Foreground = BrushFromHex("#9B4B16");
                    break;
                case StatusTone.Error:
                    InvoiceStatusBorder.Background = BrushFromHex("#FFF1F1");
                    InvoiceStatusBorder.BorderBrush = BrushFromHex("#F0B8B8");
                    InvoiceStatusText.Foreground = BrushFromHex("#B42318");
                    break;
                default:
                    InvoiceStatusBorder.Background = BrushFromHex("#F6FBFA");
                    InvoiceStatusBorder.BorderBrush = BrushFromHex("#DDE7E8");
                    InvoiceStatusText.Foreground = BrushFromHex("#0D625E");
                    break;
            }
        }

        private void SetFooterStatus(string message)
        {
            FooterStatusText.Text = message ?? string.Empty;
        }

        private async Task<PythonResult> RunPythonScriptAsync(string scriptName, params string[] args)
        {
            string backendDirectory = ResolveBackendDirectory();
            if (string.IsNullOrWhiteSpace(backendDirectory))
                return PythonResult.Fail("Backend folder was not found.");

            string scriptPath = Path.Combine(backendDirectory, scriptName);
            if (!File.Exists(scriptPath))
                return PythonResult.Fail("Backend script was not found: " + scriptPath);

            List<string> pythonArgs = new List<string>();
            pythonArgs.Add(scriptPath);
            pythonArgs.AddRange(args ?? Array.Empty<string>());

            return await RunPythonAsync(backendDirectory, pythonArgs);
        }

        private async Task<PythonResult> RunPythonInlineAsync(string code, params string[] args)
        {
            string backendDirectory = ResolveBackendDirectory();
            if (string.IsNullOrWhiteSpace(backendDirectory))
                return PythonResult.Fail("Backend folder was not found.");

            List<string> pythonArgs = new List<string>();
            pythonArgs.Add("-c");
            pythonArgs.Add(code);
            pythonArgs.AddRange(args ?? Array.Empty<string>());

            return await RunPythonAsync(backendDirectory, pythonArgs);
        }

        private Task<PythonResult> RunPythonAsync(string workingDirectory, IList<string> pythonArguments)
        {
            return Task.Run(() =>
            {
                List<PythonCommand> candidates = GetPythonCandidates();
                string lastError = string.Empty;

                foreach (PythonCommand candidate in candidates)
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = candidate.Executable,
                        Arguments = BuildArgumentLine(candidate.PrefixArguments.Concat(pythonArguments)),
                        WorkingDirectory = workingDirectory,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using (Process process = new Process())
                    {
                        process.StartInfo = startInfo;
                        try
                        {
                            process.Start();
                            string stdout = process.StandardOutput.ReadToEnd();
                            string stderr = process.StandardError.ReadToEnd();

                            if (!process.WaitForExit(PythonTimeoutMilliseconds))
                            {
                                try
                                {
                                    process.Kill();
                                }
                                catch
                                {
                                    // Best effort only; the result below reports the timeout.
                                }

                                return PythonResult.Fail("Python backend timed out.");
                            }

                            return new PythonResult
                            {
                                Success = process.ExitCode == 0,
                                ExitCode = process.ExitCode,
                                StandardOutput = stdout ?? string.Empty,
                                StandardError = stderr ?? string.Empty
                            };
                        }
                        catch (Win32Exception ex)
                        {
                            lastError = ex.Message;
                        }
                        catch (Exception ex)
                        {
                            return PythonResult.Fail(ex.Message);
                        }
                    }
                }

                return PythonResult.Fail("Python executable was not found. " + lastError);
            });
        }

        private string ResolveBackendDirectory()
        {
            if (!string.IsNullOrWhiteSpace(_backendDirectory) && Directory.Exists(_backendDirectory))
                return _backendDirectory;

            List<string> roots = new List<string>();
            AddRootCandidate(roots, AppDomain.CurrentDomain.BaseDirectory);
            AddRootCandidate(roots, Directory.GetCurrentDirectory());
            AddRootCandidate(roots, Path.GetDirectoryName(typeof(EnterProductionDataPage).Assembly.Location));

            foreach (string root in roots)
            {
                DirectoryInfo current = new DirectoryInfo(root);
                while (current != null)
                {
                    string candidate = Path.Combine(current.FullName, "backend");
                    if (Directory.Exists(candidate))
                    {
                        _backendDirectory = candidate;
                        return _backendDirectory;
                    }

                    current = current.Parent;
                }
            }

            return string.Empty;
        }

        private static void AddRootCandidate(ICollection<string> roots, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(value);
            }
            catch
            {
                return;
            }

            if (!roots.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
                roots.Add(fullPath);
        }

        private static List<PythonCommand> GetPythonCandidates()
        {
            List<PythonCommand> candidates = new List<PythonCommand>();

            string configured = Environment.GetEnvironmentVariable("RUBEXOPS_PYTHON");
            if (!string.IsNullOrWhiteSpace(configured))
                candidates.Add(new PythonCommand(configured));

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            AddPythonPathIfExists(candidates, Path.Combine(baseDirectory, ".venv", "Scripts", "python.exe"));
            AddPythonPathIfExists(candidates, Path.Combine(baseDirectory, "venv", "Scripts", "python.exe"));
            AddPythonPathIfExists(candidates, Path.Combine(Directory.GetCurrentDirectory(), ".venv", "Scripts", "python.exe"));
            AddPythonPathIfExists(candidates, Path.Combine(Directory.GetCurrentDirectory(), "venv", "Scripts", "python.exe"));

            candidates.Add(new PythonCommand("python"));
            candidates.Add(new PythonCommand("py", "-3"));
            candidates.Add(new PythonCommand("python3"));

            return candidates;
        }

        private static void AddPythonPathIfExists(ICollection<PythonCommand> candidates, string path)
        {
            if (File.Exists(path) && !candidates.Any(c => string.Equals(c.Executable, path, StringComparison.OrdinalIgnoreCase)))
                candidates.Add(new PythonCommand(path));
        }

        private static string BuildArgumentLine(IEnumerable<string> arguments)
        {
            return string.Join(" ", arguments.Select(QuoteArgument));
        }

        private static string QuoteArgument(string argument)
        {
            if (argument == null)
                return "\"\"";

            bool needsQuotes = argument.Length == 0 || argument.Any(char.IsWhiteSpace) || argument.Contains("\"");
            if (!needsQuotes)
                return argument;

            StringBuilder builder = new StringBuilder();
            builder.Append('"');
            int backslashCount = 0;

            foreach (char c in argument)
            {
                if (c == '\\')
                {
                    backslashCount++;
                    continue;
                }

                if (c == '"')
                {
                    builder.Append('\\', backslashCount * 2 + 1);
                    builder.Append('"');
                    backslashCount = 0;
                    continue;
                }

                if (backslashCount > 0)
                {
                    builder.Append('\\', backslashCount);
                    backslashCount = 0;
                }

                builder.Append(c);
            }

            if (backslashCount > 0)
                builder.Append('\\', backslashCount * 2);

            builder.Append('"');
            return builder.ToString();
        }

        private static List<PurchaseRecord> ParsePurchaseRows(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<PurchaseRecord>();

            List<Dictionary<string, JsonElement>> rows =
                JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json.Trim());

            List<PurchaseRecord> purchases = new List<PurchaseRecord>();
            if (rows == null)
                return purchases;

            foreach (Dictionary<string, JsonElement> row in rows)
            {
                string invoiceNumber = GetJsonString(row, "InvoiceNumber", "invoice_number");
                double netWeight = GetJsonDouble(row, "NetWeight", "net_weight", "InputQuantity", "input_quantity");
                if (netWeight <= 0)
                    netWeight = GetJsonDouble(row, "InvoiceWeight", "invoice_weight");

                purchases.Add(new PurchaseRecord
                {
                    VendorName = GetJsonString(row, "VendorName", "vendor_name"),
                    VendorId = GetJsonString(row, "VendorID", "VendorId", "vendor_id"),
                    ContractId = GetJsonString(row, "ContractID", "ContractId", "contract_id"),
                    InvoiceNumber = invoiceNumber,
                    PurchaseDateText = GetJsonString(row, "PurchaseOrderDate", "purchase_order_date"),
                    InputQuantity = netWeight,
                    InitialDrc = GetJsonDouble(row, "CalculatedDrc", "CalculatedDRC", "calculated_drc_percent", "initial_drc")
                });
            }

            return purchases;
        }

        private static ProductionProbe ParseProductionProbe(string json)
        {
            Dictionary<string, JsonElement> row =
                JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json.Trim());

            if (row == null)
                return new ProductionProbe();

            return new ProductionProbe
            {
                InvoiceUsed = GetJsonBool(row, "invoice_used", "InvoiceUsed"),
                BatchId = GetJsonString(row, "batch_id", "BatchID", "BatchId")
            };
        }

        private static string GetJsonString(Dictionary<string, JsonElement> row, params string[] keys)
        {
            JsonElement value;
            if (!TryGetJsonValue(row, out value, keys))
                return string.Empty;

            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    return value.GetString() ?? string.Empty;
                case JsonValueKind.Number:
                    return value.ToString();
                case JsonValueKind.True:
                    return "true";
                case JsonValueKind.False:
                    return "false";
                default:
                    return string.Empty;
            }
        }

        private static double GetJsonDouble(Dictionary<string, JsonElement> row, params string[] keys)
        {
            JsonElement value;
            if (!TryGetJsonValue(row, out value, keys))
                return 0.0;

            if (value.ValueKind == JsonValueKind.Number)
            {
                double number;
                if (value.TryGetDouble(out number))
                    return number;
            }

            return ParseQuantity(value.ToString());
        }

        private static bool GetJsonBool(Dictionary<string, JsonElement> row, params string[] keys)
        {
            JsonElement value;
            if (!TryGetJsonValue(row, out value, keys))
                return false;

            if (value.ValueKind == JsonValueKind.True)
                return true;
            if (value.ValueKind == JsonValueKind.False)
                return false;

            bool parsed;
            return bool.TryParse(value.ToString(), out parsed) && parsed;
        }

        private static bool TryGetJsonValue(Dictionary<string, JsonElement> row, out JsonElement value, params string[] keys)
        {
            foreach (string key in keys)
            {
                if (row.TryGetValue(key, out value))
                    return true;

                KeyValuePair<string, JsonElement> match = row.FirstOrDefault(pair =>
                    string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(match.Key))
                {
                    value = match.Value;
                    return true;
                }
            }

            value = default(JsonElement);
            return false;
        }

        private static bool TryParseDisplayDate(string value, out DateTime date)
        {
            string text = (value ?? string.Empty).Trim();
            string[] formats = { "dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "MM/dd/yyyy" };
            return DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                   || DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
        }

        private static double ParseQuantity(string value)
        {
            string cleaned = (value ?? string.Empty)
                .Replace(",", string.Empty)
                .Replace("%", string.Empty)
                .Trim();
            cleaned = RemoveTokenIgnoreCase(cleaned, "kgs");
            cleaned = RemoveTokenIgnoreCase(cleaned, "kg").Trim();

            double number;
            if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out number))
                return number;

            if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.CurrentCulture, out number))
                return number;

            return 0.0;
        }

        private static string RemoveTokenIgnoreCase(string value, string token)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(token))
                return value ?? string.Empty;

            string result = value;
            int index = result.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                result = result.Remove(index, token.Length);
                index = result.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            }

            return result;
        }

        private static string FormatKg(double value)
        {
            return value > 0
                ? value.ToString("#,##0.00", CultureInfo.InvariantCulture) + " kgs"
                : "-";
        }

        private static string FormatPercent(double value)
        {
            return value > 0
                ? value.ToString("0.00", CultureInfo.InvariantCulture) + " %"
                : "-";
        }

        private static string FormatSignedPercent(double value)
        {
            return value.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + " %";
        }

        private static string EmptyIfMissing(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        }

        private static Brush BrushFromHex(string hex)
        {
            return (Brush)new BrushConverter().ConvertFromString(hex);
        }

        private static string CleanBackendError(PythonResult result)
        {
            string message = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput
                : result.StandardError;

            message = (message ?? string.Empty).Trim();
            if (message.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                message = message.Substring(6).Trim();

            if (string.IsNullOrWhiteSpace(message))
                message = "Backend failed with exit code " + result.ExitCode.ToString(CultureInfo.InvariantCulture) + ".";

            return message;
        }

        private static string ExtractBatchIdFromSaveOutput(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return string.Empty;

            const string marker = "Batch ID:";
            int markerIndex = output.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
                return string.Empty;

            string tail = output.Substring(markerIndex + marker.Length).Trim();
            int separator = tail.IndexOf('|');
            if (separator >= 0)
                tail = tail.Substring(0, separator).Trim();

            return tail;
        }

        private sealed class PurchaseRecord
        {
            public string VendorName { get; set; }
            public string VendorId { get; set; }
            public string ContractId { get; set; }
            public string InvoiceNumber { get; set; }
            public string PurchaseDateText { get; set; }
            public double InputQuantity { get; set; }
            public double InitialDrc { get; set; }
            public bool InvoiceUsed { get; set; }
            public string BatchIdPreview { get; set; }

            public string SuggestionSubText
            {
                get
                {
                    string vendor = string.IsNullOrWhiteSpace(VendorName) ? "Unknown vendor" : VendorName;
                    string contract = string.IsNullOrWhiteSpace(ContractId) ? "No contract" : ContractId;
                    return vendor + " | " + contract;
                }
            }

            public string NetWeightDisplay
            {
                get { return InputQuantity > 0 ? InputQuantity.ToString("#,##0.##", CultureInfo.InvariantCulture) + " kgs" : "No qty"; }
            }
        }

        private sealed class ProductionProbe
        {
            public bool InvoiceUsed { get; set; }
            public string BatchId { get; set; }
        }

        private sealed class PythonResult
        {
            public bool Success { get; set; }
            public int ExitCode { get; set; }
            public string StandardOutput { get; set; }
            public string StandardError { get; set; }

            public static PythonResult Fail(string message)
            {
                return new PythonResult
                {
                    Success = false,
                    ExitCode = -1,
                    StandardOutput = string.Empty,
                    StandardError = message ?? string.Empty
                };
            }
        }

        private sealed class PythonCommand
        {
            public PythonCommand(string executable, params string[] prefixArguments)
            {
                Executable = executable;
                PrefixArguments = prefixArguments ?? Array.Empty<string>();
            }

            public string Executable { get; private set; }
            public IEnumerable<string> PrefixArguments { get; private set; }
        }

        private enum StatusTone
        {
            Neutral,
            Success,
            Warning,
            Error
        }
    }
}
