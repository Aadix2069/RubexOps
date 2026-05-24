using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RubexOps
{
    public partial class EditContractWindow : Window
    {
        // =====================================================
        // CURRENT CONTRACT
        // =====================================================

        private readonly PurchaseContract currentContract;



        // =====================================================
        // PYTHON COMMAND
        // =====================================================

        private readonly string pythonExe =
            "python";



        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        public EditContractWindow(
            PurchaseContract contract)
        {
            InitializeComponent();

            currentContract = contract;

            LoadContractData();
        }



        // =====================================================
        // LOAD EXISTING DATA
        // =====================================================

        private void LoadContractData()
        {
            ContractSummaryText.Text =
                $"{currentContract.vendor_name ?? ""} - {currentContract.vendor_id ?? ""}";

            StatusTextBlock.Text =
                currentContract.DisplayStatus.ToUpperInvariant();

            StatusBadge.Background =
                BrushFromHex(
                    currentContract.StatusBackground);

            VendorNameBox.Text =
                currentContract.vendor_name ?? "";

            VendorIDBox.Text =
                currentContract.vendor_id ?? "";

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

            SelectResponsibility(
                currentContract.breach_responsibility);

            ConfigureBreachResponsibilityState();
        }



        // =====================================================
        // BREACH RESPONSIBILITY STATE
        // =====================================================

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
                "Breach responsibility is locked because Excel column Q does not indicate a breach.";
        }



        // =====================================================
        // RESPONSIBILITY CHANGED
        // =====================================================

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



        // =====================================================
        // SAVE BUTTON
        // =====================================================

        private void SaveButton_Click(
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

                if (!ValidateForm(
                        out double basePrice,
                        out double agreedQty,
                        out double penaltyPercent,
                        out int remedyDays))
                {
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
                        "update_purchase_contract.py"
                    );

                if (!File.Exists(pythonScript))
                {
                    MessageBox.Show(
                        "Backend Python file not found.\n\n" +
                        pythonScript,
                        "File Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

                    return;
                }

                string arguments =
                    QuoteArgument(currentContract.row.ToString(
                        CultureInfo.InvariantCulture)) + " " +

                    QuoteArgument(VendorNameBox.Text.Trim()) + " " +

                    QuoteArgument(VendorIDBox.Text.Trim()) + " " +

                    QuoteArgument(basePrice.ToString(
                        CultureInfo.InvariantCulture)) + " " +

                    QuoteArgument(agreedQty.ToString(
                        CultureInfo.InvariantCulture)) + " " +

                    QuoteArgument(penaltyPercent.ToString(
                        CultureInfo.InvariantCulture)) + " " +

                    QuoteArgument(remedyDays.ToString(
                        CultureInfo.InvariantCulture)) + " " +

                    QuoteArgument(responsibility);

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
                                "backend"
                            )
                    };

                using Process process =
                    Process.Start(start)
                    ?? throw new Exception(
                        "Failed to start backend process."
                    );

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
                        output,
                        "Backend Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

                    return;
                }

                MessageBox.Show(
                    output,
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

                    button.Content = "Save Changes";
                }
            }
        }



        // =====================================================
        // VALIDATION
        // =====================================================

        private bool ValidateForm(
            out double basePrice,
            out double agreedQty,
            out double penaltyPercent,
            out int remedyDays)
        {
            basePrice = 0;
            agreedQty = 0;
            penaltyPercent = 0;
            remedyDays = 0;

            if (string.IsNullOrWhiteSpace(
                    VendorNameBox.Text))
            {
                ShowValidation(
                    "Vendor Name is required.",
                    VendorNameBox);

                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    VendorIDBox.Text))
            {
                ShowValidation(
                    "Vendor ID is required.",
                    VendorIDBox);

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

            if (!TryReadDouble(
                    PenaltyPercentBox.Text.Replace("%", ""),
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



        // =====================================================
        // HELPERS
        // =====================================================

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

        private static string QuoteArgument(
            string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static SolidColorBrush BrushFromHex(
            string hex)
        {
            return (SolidColorBrush)
                new BrushConverter().ConvertFromString(hex);
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
        // CANCEL BUTTON
        // =====================================================

        private void CancelButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }
    }
}