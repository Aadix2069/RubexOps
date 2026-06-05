#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RubexOps
{
    public partial class ViewEditProductionDataPage : Page
    {
        private readonly ObservableCollection<ProductionBatchNode> _batchStream = new ObservableCollection<ProductionBatchNode>();
        private readonly ObservableCollection<ValidationRule> _validations = new ObservableCollection<ValidationRule>();

        private List<ProductionBatchNode> _allBatches = new List<ProductionBatchNode>();
        private ProductionBatchNode? _selectedBatch;

        private bool _isEditing = false;
        private bool _isDisplayInTonnes = false;
        private string? _backendDirectory;

        public ViewEditProductionDataPage()
        {
            InitializeComponent();
            BatchStreamListBox.ItemsSource = _batchStream;
            ValidationList.ItemsSource = _validations;
            SearchPlaceholder.Visibility = Visibility.Visible;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshBatchStreamAsync();
        }

        // ==========================================
        // 1. DATA PIPELINE & SEARCH
        // ==========================================
        private async Task RefreshBatchStreamAsync()
        {
            PythonResult result = await RunPythonScriptAsync("read_production_data.py");

            if (result.Success)
            {
                _allBatches = ParseProductionData(result.StandardOutput)
                    .OrderByDescending(b => b.RawProductionDate)
                    .ThenByDescending(b => b.BatchId)
                    .ToList();

                UpdateGlobalKPIs();
                FilterStream();
            }
        }

        private void UpdateGlobalKPIs()
        {
            TotalBatchesText.Text = _allBatches.Count.ToString();

            if (_allBatches.Count > 0)
            {
                double avgYield = _allBatches.Where(b => b.InputQuantity > 0).Average(b => (b.OutputQuantity / b.InputQuantity) * 100);
                double totalOutput = _allBatches.Sum(b => b.OutputQuantity);

                KpiYieldText.Text = $"{avgYield:0.00}%";
                KpiOutputText.Text = _isDisplayInTonnes ? $"{(totalOutput / 1000.0):#,##0.000} MT" : $"{totalOutput:#,##0.00} kg";
            }
        }

        private void FilterStream()
        {
            string query = (SearchBox.Text ?? string.Empty).Trim();
            _batchStream.Clear();

            var filtered = string.IsNullOrWhiteSpace(query)
                ? _allBatches
                : _allBatches.Where(b =>
                    b.BatchId.StartsWith(query, StringComparison.OrdinalIgnoreCase) ||
                    b.InvoiceNumber.StartsWith(query, StringComparison.OrdinalIgnoreCase) ||
                    b.VendorName.StartsWith(query, StringComparison.OrdinalIgnoreCase) ||
                    b.ContractId.StartsWith(query, StringComparison.OrdinalIgnoreCase));

            foreach (var batch in filtered)
            {
                batch.SetUnitContext(_isDisplayInTonnes);
                _batchStream.Add(batch);
            }

            if (_batchStream.Count > 0)
            {
                BatchStreamListBox.SelectedIndex = 0;
            }
            else
            {
                InspectorPanel.Visibility = Visibility.Hidden;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholder.Visibility = string.IsNullOrWhiteSpace(SearchBox.Text) ? Visibility.Visible : Visibility.Hidden;
            FilterStream();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && BatchStreamListBox.Items.Count > 0)
            {
                BatchStreamListBox.SelectedIndex = Math.Min(BatchStreamListBox.SelectedIndex + 1, BatchStreamListBox.Items.Count - 1);
                BatchStreamListBox.ScrollIntoView(BatchStreamListBox.SelectedItem);
            }
            else if (e.Key == Key.Up && BatchStreamListBox.Items.Count > 0)
            {
                BatchStreamListBox.SelectedIndex = Math.Max(BatchStreamListBox.SelectedIndex - 1, 0);
                BatchStreamListBox.ScrollIntoView(BatchStreamListBox.SelectedItem);
            }
        }

        // ==========================================
        // 2. BATCH INSPECTOR LOGIC
        // ==========================================
        private void BatchNode_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is ProductionBatchNode batch)
            {
                BatchStreamListBox.SelectedItem = batch;
            }
        }

        private void BatchStreamListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BatchStreamListBox.SelectedItem is ProductionBatchNode batch)
            {
                _selectedBatch = batch;
                PopulateInspector(batch);
            }
        }

        private void PopulateInspector(ProductionBatchNode batch)
        {
            InspectorPanel.Visibility = Visibility.Visible;
            EditModeToggle.IsChecked = false; // Always reset to read-only

            // Read-Only Source Fields
            InspectorBatchIdText.Text = batch.BatchId;
            InspectorVendorNameText.Text = batch.VendorName;
            InspectorContractText.Text = batch.ContractId;
            InspectorInvoiceText.Text = batch.InvoiceNumber;
            InspectorPurchDateText.Text = batch.PurchaseDateText;

            InspectorInputQtyText.Text = FormatWeight(batch.InputQuantity);
            InspectorInitialDrcText.Text = FormatPercent(batch.InitialDrc);

            // Editable Fields
            InspectorProductionDate.SelectedDate = batch.RawProductionDate;
            InspectorOutputQty.Text = FormatWeightRaw(batch.OutputQuantity);

            GaugeInitialDrc.Text = FormatPercent(batch.InitialDrc);

            RecomputeLiveMetrics();
        }

        // ==========================================
        // 3. EDIT MODE & LIVE CALCULATIONS
        // ==========================================
        private void EditModeToggle_CheckedChanged(object sender, RoutedEventArgs e)
        {
            _isEditing = EditModeToggle.IsChecked == true;

            InspectorProductionDate.IsEnabled = _isEditing;

            if (_isEditing)
            {
                InspectorOutputQty.IsReadOnly = false;
                InspectorOutputQty.Focusable = true;
                OutputBorderWrapper.Background = Brushes.White;
                OutputBorderWrapper.BorderThickness = new Thickness(1);
                InspectorOutputQty.Foreground = BrushFromHex("#0F172A");
                CancelEditButton.Visibility = Visibility.Visible;
                InspectorOutputQty.Focus();
            }
            else
            {
                InspectorOutputQty.IsReadOnly = true;
                InspectorOutputQty.Focusable = false;
                OutputBorderWrapper.Background = BrushFromHex("#F1F5F9");
                OutputBorderWrapper.BorderThickness = new Thickness(0);
                InspectorOutputQty.Foreground = BrushFromHex("#EA580C");
                CancelEditButton.Visibility = Visibility.Collapsed;
                SaveChangesButton.IsEnabled = false;

                // Revert to original data if aborted
                if (_selectedBatch != null)
                {
                    InspectorProductionDate.SelectedDate = _selectedBatch.RawProductionDate;
                    InspectorOutputQty.Text = FormatWeightRaw(_selectedBatch.OutputQuantity);
                    RecomputeLiveMetrics();
                }
            }
        }

        private void CancelEditButton_Click(object sender, RoutedEventArgs e)
        {
            EditModeToggle.IsChecked = false;
        }

        private void ProductionInput_Changed(object sender, EventArgs e)
        {
            if (!_isEditing || _selectedBatch == null) return;
            RecomputeLiveMetrics();
        }

        private void RecomputeLiveMetrics()
        {
            if (_selectedBatch == null) return;

            double inputQty = _selectedBatch.InputQuantity;
            double outputQty = ParseQuantity(InspectorOutputQty.Text);
            double initialDrc = _selectedBatch.InitialDrc;

            // Math execution
            double loss = inputQty - outputQty;
            double actualDrc = (inputQty > 0) ? (outputQty / inputQty) * 100.0 : 0.0;
            double variance = actualDrc - initialDrc;

            // UI Metric Assignment
            CalcLossText.Text = outputQty > 0 ? FormatWeight(loss) : "-";
            CalcActualDrcText.Text = outputQty > 0 ? FormatPercent(actualDrc) : "-";
            GaugeActualDrc.Text = outputQty > 0 ? FormatPercent(actualDrc) : "0.00%";

            CalcVarianceText.Text = outputQty > 0 ? FormatSignedPercent(variance) : "-";
            CalcVarianceText.Foreground = variance >= 0 ? BrushFromHex("#10B981") : BrushFromHex("#EF4444");

            UpdateGauge(initialDrc, actualDrc, outputQty > 0);
            RunValidationEngine(inputQty, outputQty, actualDrc);
        }

        private void UpdateGauge(double initialDrc, double actualDrc, bool hasOutput)
        {
            // Map 85% - 100% to a rough 250px margin width tracking layout
            double MapToTrack(double drc)
            {
                double normalized = Math.Max(85, Math.Min(100, drc));
                return ((normalized - 85) / 15.0) * 250.0;
            }

            GaugeInitialMarker.Margin = new Thickness(MapToTrack(initialDrc), 0, 0, 0);

            if (hasOutput)
            {
                GaugePointer.Visibility = Visibility.Visible;
                GaugePointer.Margin = new Thickness(MapToTrack(actualDrc) - 7, 0, 0, 0); // -7 centers the 14px ellipse
            }
            else
            {
                GaugePointer.Visibility = Visibility.Hidden;
            }
        }

        // ==========================================
        // 4. BUSINESS RULE VALIDATION ENGINE
        // ==========================================
        private void RunValidationEngine(double input, double output, double actualDrc)
        {
            _validations.Clear();
            bool allValid = true;

            AddValidation(true, "Batch Link Intact");

            // Rule 1: Output <= Input
            if (output <= 0)
            {
                AddValidation(false, "Output weight must be greater than zero.");
                allValid = false;
            }
            else if (output > input)
            {
                AddValidation(false, "Output quantity exceeds purchased Input quantity.");
                allValid = false;
            }
            else
            {
                AddValidation(true, "Mass Balance Valid (Output ≤ Input)");
            }

            // Rule 3: Actual DRC <= 100%
            if (actualDrc > 100.0)
            {
                AddValidation(false, "Calculated Actual DRC exceeds 100%. Check weight.");
                allValid = false;
            }
            else if (output > 0 && output <= input)
            {
                AddValidation(true, "Chemistry Boundary Valid (Yield ≤ 100%)");
            }

            // Rules 4 & 5: Production Date Logic
            DateTime? prodDate = InspectorProductionDate.SelectedDate;
            if (prodDate == null)
            {
                AddValidation(false, "Production Date is required.");
                allValid = false;
            }
            else
            {
                if (prodDate.Value.Date > DateTime.Today)
                {
                    AddValidation(false, "Production Date cannot be in the future.");
                    allValid = false;
                }
                else if (_selectedBatch != null && DateTime.TryParseExact(_selectedBatch.PurchaseDateText, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime purchDate))
                {
                    if (prodDate.Value.Date < purchDate.Date)
                    {
                        AddValidation(false, "Production Date cannot be earlier than Purchase Date.");
                        allValid = false;
                    }
                    else
                    {
                        AddValidation(true, "Production Date is chronologically valid.");
                    }
                }
                else
                {
                    AddValidation(true, "Production Date Valid.");
                }
            }

            if (allValid && _isEditing)
            {
                AddValidation(true, "Ready to save updates.");
                SaveChangesButton.IsEnabled = true;
            }
            else
            {
                SaveChangesButton.IsEnabled = false;
            }
        }

        private void AddValidation(bool isValid, string message)
        {
            _validations.Add(new ValidationRule
            {
                Message = message,
                IconKind = isValid ? "CheckCircle" : "AlertCircle",
                IconColor = isValid ? BrushFromHex("#10B981") : BrushFromHex("#EF4444"),
                TextColor = isValid ? BrushFromHex("#0F766E") : BrushFromHex("#EF4444")
            });
        }

        // ==========================================
        // 5. SAVE WORKFLOW & TOGGLE
        // ==========================================
        private async void SaveChangesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBatch == null || InspectorProductionDate.SelectedDate == null) return;

            SaveChangesButton.Content = "Saving...";
            SaveChangesButton.IsEnabled = false;
            EditModeToggle.IsEnabled = false;

            string prodDate = InspectorProductionDate.SelectedDate.Value.ToString("dd-MM-yyyy");
            string outputRaw = ParseQuantity(InspectorOutputQty.Text).ToString("0.##", CultureInfo.InvariantCulture);

            PythonResult result = await RunPythonScriptAsync("update_production_data.py", _selectedBatch.RowNumber.ToString(), prodDate, outputRaw);

            if (result.Success)
            {
                await RefreshBatchStreamAsync();

                var refreshedBatch = _batchStream.FirstOrDefault(b => b.BatchId == _selectedBatch.BatchId);
                if (refreshedBatch != null)
                {
                    BatchStreamListBox.SelectedItem = refreshedBatch;
                }

                EditModeToggle.IsChecked = false;
            }
            else
            {
                _validations.Add(new ValidationRule { Message = "Backend Save Failed: " + result.StandardError, IconKind = "AlertCircle", IconColor = BrushFromHex("#EF4444"), TextColor = BrushFromHex("#EF4444") });
                SaveChangesButton.IsEnabled = true;
            }

            SaveChangesButton.Content = "Save Changes";
            EditModeToggle.IsEnabled = true;
        }

        private void UnitToggleButton_CheckedChanged(object sender, RoutedEventArgs e)
        {
            _isDisplayInTonnes = UnitToggleButton.IsChecked == true;
            UnitSuffixText.Text = _isDisplayInTonnes ? "MT" : "kg";

            if (_selectedBatch != null)
            {
                PopulateInspector(_selectedBatch);
            }

            foreach (var batch in _allBatches)
            {
                batch.SetUnitContext(_isDisplayInTonnes);
            }

            UpdateGlobalKPIs();
        }

        // ==========================================
        // 6. UTILITIES & PARSING
        // ==========================================
        private string FormatWeight(double valueInKg)
        {
            return _isDisplayInTonnes
                ? (valueInKg / 1000.0).ToString("#,##0.000", CultureInfo.InvariantCulture) + " MT"
                : valueInKg.ToString("#,##0.00", CultureInfo.InvariantCulture) + " kg";
        }

        private string FormatWeightRaw(double valueInKg)
        {
            return _isDisplayInTonnes
                ? (valueInKg / 1000.0).ToString("#,##0.000", CultureInfo.InvariantCulture)
                : valueInKg.ToString("#,##0.00", CultureInfo.InvariantCulture);
        }

        private double ParseQuantity(string? text)
        {
            string clean = (text ?? string.Empty).Replace(",", "").Trim();
            if (double.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
            {
                return _isDisplayInTonnes ? val * 1000.0 : val;
            }
            return 0.0;
        }

        private string FormatPercent(double val) => val.ToString("0.00", CultureInfo.InvariantCulture) + "%";
        private string FormatSignedPercent(double val) => val.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%";
        private Brush BrushFromHex(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;

        private List<ProductionBatchNode> ParseProductionData(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<ProductionBatchNode>();
            try
            {
                var rows = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json.Trim());
                var batches = new List<ProductionBatchNode>();
                if (rows == null) return batches;

                foreach (var r in rows)
                {
                    batches.Add(new ProductionBatchNode
                    {
                        RowNumber = r.ContainsKey("row") ? r["row"].GetInt32() : 0,
                        BatchId = (r.TryGetValue("batch_id", out var bId) ? bId.GetString() : string.Empty) ?? string.Empty,
                        InvoiceNumber = (r.TryGetValue("invoice_number", out var iNum) ? iNum.GetString() : string.Empty) ?? string.Empty,
                        VendorName = (r.TryGetValue("vendor_name", out var vName) ? vName.GetString() : "Unknown Vendor") ?? "Unknown Vendor",
                        ContractId = (r.TryGetValue("contract_id", out var cId) ? cId.GetString() : "-") ?? "-",
                        PurchaseDateText = (r.TryGetValue("purchase_order_date", out var pDate) ? pDate.GetString() : "-") ?? "-",
                        InputQuantity = r.ContainsKey("input_quantity") ? r["input_quantity"].GetDouble() : 0.0,
                        OutputQuantity = r.ContainsKey("output") ? r["output"].GetDouble() : 0.0,
                        InitialDrc = r.ContainsKey("initial_drc") ? r["initial_drc"].GetDouble() : 0.0,
                        RawProductionDate = (r.TryGetValue("production_date", out var pd) && pd.GetString() != null)
                            ? DateTime.ParseExact(pd.GetString()!, "dd-MM-yyyy", CultureInfo.InvariantCulture)
                            : DateTime.Today
                    });
                }
                return batches;
            }
            catch { return new List<ProductionBatchNode>(); }
        }

        private async Task<PythonResult> RunPythonScriptAsync(string scriptName, params string[] args)
        {
            if (string.IsNullOrWhiteSpace(_backendDirectory))
            {
                _backendDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backend");
            }

            string scriptPath = Path.Combine(_backendDirectory, scriptName);

            return await Task.Run(() =>
            {
                var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments = $"\"{scriptPath}\" " + string.Join(" ", args.Select(a => $"\"{a}\"")),
                        WorkingDirectory = _backendDirectory,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                try
                {
                    p.Start();
                    string? stdOut = p.StandardOutput.ReadToEnd();
                    string? stdErr = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    return new PythonResult { Success = p.ExitCode == 0, StandardOutput = stdOut ?? string.Empty, StandardError = stdErr ?? string.Empty };
                }
                catch (Exception ex)
                {
                    return new PythonResult { Success = false, StandardOutput = string.Empty, StandardError = ex.Message };
                }
            });
        }
    }

    // ==========================================
    // DATA MODELS
    // ==========================================
    public class ProductionBatchNode : INotifyPropertyChanged
    {
        public int RowNumber { get; set; }
        public string BatchId { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public string ContractId { get; set; } = string.Empty;
        public string VendorName { get; set; } = string.Empty;
        public string PurchaseDateText { get; set; } = string.Empty;
        public DateTime RawProductionDate { get; set; }
        public double InputQuantity { get; set; }
        public double OutputQuantity { get; set; }
        public double InitialDrc { get; set; }

        private bool _isTonne;
        public void SetUnitContext(bool isTonne)
        {
            _isTonne = isTonne;
            OnPropertyChanged(nameof(OutputDisplay));
        }

        public string InvoiceDisplay => $"🧾 {InvoiceNumber}";
        public string DateDisplay => $"{RawProductionDate:dd-MM-yyyy}";
        public string OutputDisplay => _isTonne ? $"{(OutputQuantity / 1000.0):#,##0.000} MT" : $"{OutputQuantity:#,##0.00} kg";

        public string YieldDisplay
        {
            get
            {
                double y = InputQuantity > 0 ? (OutputQuantity / InputQuantity) * 100 : 0;
                return $"{y:0.00}% Yield";
            }
        }

        public Brush BackgroundBrush => (Brush)new BrushConverter().ConvertFromString("#FFFFFF")!;
        public Brush BorderBrush => (Brush)new BrushConverter().ConvertFromString("#E2E8F0")!;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ValidationRule
    {
        public string Message { get; set; } = string.Empty;
        public string IconKind { get; set; } = "CheckCircle";
        public Brush IconColor { get; set; } = Brushes.Green;
        public Brush TextColor { get; set; } = Brushes.Black;
    }

    public class PythonResult
    {
        public bool Success { get; set; }
        public string StandardOutput { get; set; } = string.Empty;
        public string StandardError { get; set; } = string.Empty;
    }
}