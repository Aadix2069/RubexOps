using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RubexOps
{
    public partial class EditSalesContractWindow : Window
    {
        private readonly SalesContract currentContract;

        private readonly string pythonExe = "python";

        public EditSalesContractWindow(SalesContract contract)
        {
            InitializeComponent();

            currentContract = contract;

            LoadContractData();

            // Listen for the Enter key across the entire window to trigger a save
            this.KeyDown += EditSalesContractWindow_KeyDown;
        }

        // New event handler for the Enter key
        private void EditSalesContractWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Prevent the key event from propagating further
                e.Handled = true;

                // Trigger the existing save logic
                SaveButton_Click(this, new RoutedEventArgs());
            }
        }

        private void LoadContractData()
        {
            string contractId =
                string.IsNullOrWhiteSpace(currentContract.contract_id)
                    ? ""
                    : $" | {currentContract.contract_id}";

            ContractSummaryText.Text =
                $"{currentContract.customer_name ?? ""} - {currentContract.customer_id ?? ""}{contractId}";

            StatusTextBlock.Text =
                currentContract.DisplayStatus.ToUpperInvariant();

            StatusBadge.Background =
                BrushFromHex(
                    currentContract.StatusBackground);

            CustomerNameBox.Text =
                currentContract.customer_name ?? "";

            CustomerIDBox.Text =
                currentContract.customer_id ?? "";

            // Customer ID must now be editable.
            CustomerIDBox.IsReadOnly = false;

            BasePriceBox.Text =
                FormatNumberForInput(
                    currentContract.base_price);

            AgreedQtyBox.Text =
                FormatNumberForInput(
                    currentContract.agreed_qty);

            PenaltyPercentBox.Text =
                FormatNumberForInput(
                    currentContract.penalty_percent);

            RemedyDaysBox.Text =
                FormatNumberForInput(
                    currentContract.remedy_days);

            RevisedRateBox.Text =
                currentContract.revised_rate.HasValue
                    ? $"Rs. {FormatNumberForInput(currentContract.revised_rate)}"
                    : "Not calculated";

            RemedyDeadlineBox.Text =
                string.IsNullOrWhiteSpace(currentContract.remedy_deadline)
                    ? "Not calculated"
                    : currentContract.remedy_deadline;

            StartDatePicker.SelectedDate =
                TryParseContractDate(currentContract.start_date, out DateTime startDate)
                    ? startDate
                    : null;

            EndDatePicker.SelectedDate =
                TryParseContractDate(currentContract.end_date, out DateTime endDate)
                    ? endDate
                    : null;

            IgnoreRemainingQtyCheckBox.IsChecked = false;

            SelectResponsibility(
                currentContract.breach_responsibility);

            ConfigureBreachResponsibilityState();
        }

        private void ConfigureBreachResponsibilityState()
        {
            bool breachDetected =
                IsBreachDetected();

            ResponsibilityComboBox.IsEnabled =
                breachDetected;

            RevisedRateBox.Visibility =
                breachDetected
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            RemedyDeadlineBox.Visibility =
                breachDetected
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            if (breachDetected)
            {
                BreachInfoCard.Background =
                    BrushFromHex("#FEF2F2");

                BreachInfoCard.BorderBrush =
                    BrushFromHex("#FECACA");

                BreachInfoTitle.Text =
                    "Breach detected";

                BreachInfoMessage.Text =
                    "Assign responsibility to enable revised rate and remedy deadline handling.";

                return;
            }

            SelectResponsibility("Pending");

            BreachInfoCard.Background =
                BrushFromHex("#F3F4F6");

            BreachInfoCard.BorderBrush =
                BrushFromHex("#E5E7EB");

            BreachInfoTitle.Text =
                "No breach detected";

            BreachInfoMessage.Text =
                "Breach responsibility is locked because the Python contract engine has not detected a breach.";
        }

        private void ResponsibilityComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            if (!IsBreachDetected())
            {
                SelectResponsibility("Pending");
                return;
            }

            string responsibility =
                GetSelectedResponsibility();

            bool assigned =
                !responsibility.Equals(
                    "Pending",
                    StringComparison.OrdinalIgnoreCase);

            RevisedRateBox.Opacity =
                assigned ? 1 : 0.65;

            RemedyDeadlineBox.Opacity =
                assigned ? 1 : 0.65;
        }

        private void SaveButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            object? originalContent =
                sender is Button clickedButton
                    ? clickedButton.Content
                    : null;

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

                if (!ValidateForm(
                        out double basePrice,
                        out double agreedQty,
                        out double penaltyPercent,
                        out int remedyDays,
                        out DateTime startDate,
                        out DateTime endDate,
                        out string customerId,
                        out bool ignoreRemainingQty))
                {
                    return;
                }

                if (!IsCustomerIdUnique(customerId))
                {
                    ShowValidation(
                        "Another contract already uses this Customer ID. Please choose a unique Customer ID.",
                        CustomerIDBox);

                    return;
                }

                string responsibility =
                    IsBreachDetected()
                        ? GetSelectedResponsibility()
                        : "Pending";

                string pythonScript =
                    Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "backend",
                        "update_sales_contract.py"
                    );

                if (!File.Exists(pythonScript))
                {
                    MessageBox.Show(
                        "Backend Python file not found.\n\n" + pythonScript,
                        "File Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

                    return;
                }

                ProcessStartInfo start =
                    new()
                    {
                        FileName = pythonExe,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory =
                            Path.Combine(
                                AppDomain.CurrentDomain.BaseDirectory,
                                "backend"
                            )
                    };

                start.ArgumentList.Add(pythonScript);

                // Existing row identifier
                start.ArgumentList.Add(currentContract.row.ToString(CultureInfo.InvariantCulture));

                // Editable fields
                start.ArgumentList.Add(CustomerNameBox.Text.Trim());
                start.ArgumentList.Add(customerId);
                start.ArgumentList.Add(startDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture));
                start.ArgumentList.Add(endDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture));
                start.ArgumentList.Add(basePrice.ToString(CultureInfo.InvariantCulture));
                start.ArgumentList.Add(agreedQty.ToString(CultureInfo.InvariantCulture));
                start.ArgumentList.Add(penaltyPercent.ToString(CultureInfo.InvariantCulture));
                start.ArgumentList.Add(remedyDays.ToString(CultureInfo.InvariantCulture));
                start.ArgumentList.Add(responsibility);
                start.ArgumentList.Add(ignoreRemainingQty ? "1" : "0");

                using Process process =
                    Process.Start(start)
                    ?? throw new Exception("Failed to start backend process.");

                string output =
                    process.StandardOutput.ReadToEnd();

                string error =
                    process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string message =
                        !string.IsNullOrWhiteSpace(error)
                            ? error.Trim()
                            : output.Trim();

                    if (string.IsNullOrWhiteSpace(message))
                    {
                        message =
                            $"Backend process failed with exit code {process.ExitCode}.";
                    }

                    MessageBox.Show(
                        message,
                        "Backend Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

                    return;
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    MessageBox.Show(
                        error.Trim(),
                        "Backend Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

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
                        MessageBoxImage.Error
                    );

                    return;
                }

                MessageBox.Show(
                    output.Trim(),
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Application Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                Mouse.OverrideCursor = null;

                MainGrid.IsEnabled = true;

                MainGrid.Opacity = 1;

                if (sender is Button button)
                {
                    button.IsEnabled = true;
                    button.Content = originalContent ?? "Save Changes";
                }
            }
        }

        private bool ValidateForm(
            out double basePrice,
            out double agreedQty,
            out double penaltyPercent,
            out int remedyDays,
            out DateTime startDate,
            out DateTime endDate,
            out string customerId,
            out bool ignoreRemainingQty)
        {
            basePrice = 0;
            agreedQty = 0;
            penaltyPercent = 0;
            remedyDays = 0;
            startDate = default;
            endDate = default;
            customerId = "";
            ignoreRemainingQty = IgnoreRemainingQtyCheckBox.IsChecked == true;

            if (string.IsNullOrWhiteSpace(CustomerNameBox.Text))
            {
                ShowValidation(
                    "Customer Name is required.",
                    CustomerNameBox);

                return false;
            }

            if (string.IsNullOrWhiteSpace(CustomerIDBox.Text))
            {
                ShowValidation(
                    "Customer ID is required.",
                    CustomerIDBox);

                return false;
            }

            customerId = CustomerIDBox.Text.Trim();

            if (!TryReadDateFromPicker(
                    StartDatePicker,
                    "Start Date",
                    out startDate))
            {
                return false;
            }

            if (!TryReadDateFromPicker(
                    EndDatePicker,
                    "End Date",
                    out endDate))
            {
                return false;
            }

            if (endDate < startDate)
            {
                ShowValidation(
                    "End Date cannot be earlier than Start Date.",
                    EndDatePicker);

                return false;
            }

            if (!TryReadDouble(
                    BasePriceBox.Text,
                    out basePrice))
            {
                ShowValidation(
                    "Base Price must be numeric.",
                    BasePriceBox);

                return false;
            }

            if (basePrice <= 0)
            {
                ShowValidation(
                    "Base Price must be greater than zero.",
                    BasePriceBox);

                return false;
            }

            if (!TryReadDouble(
                    AgreedQtyBox.Text,
                    out agreedQty))
            {
                ShowValidation(
                    "Agreed Quantity must be numeric.",
                    AgreedQtyBox);

                return false;
            }

            if (agreedQty <= 0)
            {
                ShowValidation(
                    "Agreed Quantity must be greater than zero.",
                    AgreedQtyBox);

                return false;
            }

            string penaltyText =
                PenaltyPercentBox.Text.Replace("%", "").Trim();

            if (!TryReadDouble(
                    penaltyText,
                    out penaltyPercent))
            {
                ShowValidation(
                    "Penalty Percentage must be numeric.",
                    PenaltyPercentBox);

                return false;
            }

            if (penaltyPercent < 0 ||
                penaltyPercent > 100)
            {
                ShowValidation(
                    "Penalty Percentage must be between 0 and 100.",
                    PenaltyPercentBox);

                return false;
            }

            if (!int.TryParse(
                    RemedyDaysBox.Text.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out remedyDays))
            {
                ShowValidation(
                    "Remedy Days must be a whole number.",
                    RemedyDaysBox);

                return false;
            }

            if (remedyDays < 0)
            {
                ShowValidation(
                    "Remedy Days cannot be negative.",
                    RemedyDaysBox);

                return false;
            }

            if (IsBreachDetected() &&
                string.IsNullOrWhiteSpace(GetSelectedResponsibility()))
            {
                MessageBox.Show(
                    "Please select breach responsibility.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                ResponsibilityComboBox.Focus();

                return false;
            }

            return true;
        }

        private bool IsCustomerIdUnique(string newCustomerId)
        {
            if (string.IsNullOrWhiteSpace(newCustomerId))
            {
                return false;
            }

            try
            {
                List<SalesContract> contracts =
                    LoadAllContractsForValidation();

                string newKey =
                    newCustomerId.Trim().ToUpperInvariant();

                string currentKey =
                    (currentContract.customer_id ?? "")
                        .Trim()
                        .ToUpperInvariant();

                foreach (SalesContract contract in contracts)
                {
                    string contractCustomerId =
                        (contract.customer_id ?? "")
                            .Trim()
                            .ToUpperInvariant();

                    if (contractCustomerId == "")
                    {
                        continue;
                    }

                    // Allow the current customer chain to keep its own ID.
                    if (contractCustomerId == currentKey)
                    {
                        continue;
                    }

                    if (contractCustomerId == newKey)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                // If validation lookup fails, stop the save rather than risking duplication.
                return false;
            }
        }

        private List<SalesContract> LoadAllContractsForValidation()
        {
            string pythonScript =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "backend",
                    "read_sales_contract.py"
                );

            if (!File.Exists(pythonScript))
            {
                throw new FileNotFoundException(
                    "Backend Python file not found.",
                    pythonScript);
            }

            ProcessStartInfo start =
                new()
                {
                    FileName = pythonExe,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory =
                        Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "backend"
                        )
                };

            start.ArgumentList.Add(pythonScript);

            using Process process =
                Process.Start(start)
                ?? throw new Exception("Failed to start backend process.");

            string output =
                process.StandardOutput.ReadToEnd();

            string error =
                process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                string message =
                    !string.IsNullOrWhiteSpace(error)
                        ? error.Trim()
                        : output.Trim();

                throw new Exception(
                    string.IsNullOrWhiteSpace(message)
                        ? $"Backend process failed with exit code {process.ExitCode}."
                        : message);
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new Exception(error.Trim());
            }

            if (output.TrimStart().StartsWith(
                    "ERROR",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(output.Trim());
            }

            JsonSerializerOptions options =
                new()
                {
                    PropertyNameCaseInsensitive = true
                };

            return JsonSerializer.Deserialize<List<SalesContract>>(
                       output,
                       options)
                   ?? new List<SalesContract>();
        }

        private bool IsBreachDetected()
        {
            return currentContract.IsBreachDetected;
        }

        private void SelectResponsibility(
            string? responsibility)
        {
            string target =
                string.IsNullOrWhiteSpace(responsibility)
                    ? "Pending"
                    : responsibility.Trim();

            foreach (ComboBoxItem item in ResponsibilityComboBox.Items)
            {
                string value =
                    item.Content?.ToString() ?? "";

                if (value.Equals(
                        target,
                        StringComparison.OrdinalIgnoreCase))
                {
                    ResponsibilityComboBox.SelectedItem = item;

                    return;
                }
            }

            ResponsibilityComboBox.SelectedIndex = 0;
        }

        private string GetSelectedResponsibility()
        {
            if (ResponsibilityComboBox.SelectedItem is ComboBoxItem item)
            {
                return item.Content?.ToString() ?? "Pending";
            }

            return "Pending";
        }

        private static bool TryReadDouble(
            string text,
            out double value)
        {
            string cleanText =
                text.Trim()
                    .Replace("%", "")
                    .Replace(",", "");

            return double.TryParse(
                cleanText,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static bool TryParseContractDate(
            string? text,
            out DateTime date)
        {
            date = default;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string[] formats =
            {
                "dd-MM-yyyy",
                "d-M-yyyy",
                "dd/MM/yyyy",
                "d/M/yyyy",
                "yyyy-MM-dd",
                "d MMM yyyy",
                "dd MMM yyyy",
                "d MMMM yyyy",
                "dd MMMM yyyy"
            };

            return DateTime.TryParseExact(
                       text.Trim(),
                       formats,
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.AllowWhiteSpaces,
                       out date)
                   || DateTime.TryParse(
                       text.Trim(),
                       CultureInfo.CurrentCulture,
                       DateTimeStyles.AllowWhiteSpaces,
                       out date);
        }

        private static bool TryReadDateFromPicker(
            DatePicker picker,
            string fieldName,
            out DateTime date)
        {
            date = default;

            if (picker.SelectedDate.HasValue)
            {
                date = picker.SelectedDate.Value.Date;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(picker.Text) &&
                TryParseContractDate(picker.Text, out date))
            {
                return true;
            }

            MessageBox.Show(
                $"{fieldName} is required and must be a valid date.",
                "Validation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            picker.Focus();

            return false;
        }

        private static string FormatNumberForInput(
            double? value)
        {
            if (!value.HasValue)
            {
                return "";
            }

            return value.Value.ToString(
                "0.##",
                CultureInfo.InvariantCulture);
        }

        private static SolidColorBrush BrushFromHex(string hex)
        {
            object? converted =
                new BrushConverter().ConvertFromString(hex);

            if (converted is SolidColorBrush brush)
            {
                return brush;
            }

            return new SolidColorBrush(Colors.Transparent);
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