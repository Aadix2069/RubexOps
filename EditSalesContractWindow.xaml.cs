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
    public partial class EditSalesContractWindow : Window
    {
        private readonly SalesContract currentContract;

        public EditSalesContractWindow(SalesContract contract)
        {
            InitializeComponent();

            currentContract = contract;
            LoadContractData();
        }

        private void LoadContractData()
        {
            ContractSummaryText.Text =
                $"{currentContract.customer_name ?? ""} - {currentContract.customer_id ?? ""}";

            StatusTextBlock.Text =
                currentContract.DisplayStatus.ToUpperInvariant();

            StatusBadge.Background =
                BrushFromHex(currentContract.StatusBackground);

            CustomerNameBox.Text = currentContract.customer_name ?? "";
            CustomerIDBox.Text = currentContract.customer_id ?? "";
            BasePriceBox.Text = FormatNumberForInput(currentContract.base_price);
            AgreedQtyBox.Text = FormatNumberForInput(currentContract.agreed_qty);
            PenaltyPercentBox.Text = FormatNumberForInput(currentContract.penalty_percent);
            RemedyDaysBox.Text = FormatNumberForInput(currentContract.remedy_days);

            RevisedRateBox.Text =
                currentContract.revised_rate.HasValue
                    ? $"Rs. {FormatNumberForInput(currentContract.revised_rate)}"
                    : "Not calculated";

            RemedyDeadlineBox.Text =
                string.IsNullOrWhiteSpace(currentContract.remedy_deadline)
                    ? "Not calculated"
                    : currentContract.remedy_deadline;

            SelectResponsibility(currentContract.breach_responsibility);
            ConfigureBreachResponsibilityState();
        }

        private void ConfigureBreachResponsibilityState()
        {
            bool breachDetected = currentContract.IsBreachDetected;
            ResponsibilityComboBox.IsEnabled = breachDetected;

            if (breachDetected)
            {
                BreachInfoCard.Background = BrushFromHex("#FEF2F2");
                BreachInfoCard.BorderBrush = BrushFromHex("#FECACA");
                BreachInfoTitle.Text = "Breach detected";
                BreachInfoMessage.Text =
                    "Assign Company, Customer, or Shared responsibility to support remedy handling.";
                return;
            }

            SelectResponsibility("Pending");
            BreachInfoCard.Background = BrushFromHex("#F3F4F6");
            BreachInfoCard.BorderBrush = BrushFromHex("#E5E7EB");
            BreachInfoTitle.Text = "No breach detected";
            BreachInfoMessage.Text =
                "Breach responsibility is locked because Excel column Q does not indicate a breach.";
        }

        private void ResponsibilityComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            if (!currentContract.IsBreachDetected)
            {
                SelectResponsibility("Pending");
            }
        }

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
                        out double basePrice,
                        out double agreedQty,
                        out double penaltyPercent,
                        out int remedyDays))
                {
                    return;
                }

                string responsibility =
                    currentContract.IsBreachDetected
                        ? GetSelectedResponsibility()
                        : "Pending";

                string pythonScript = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "backend",
                    "update_sales_contract.py");

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
                    Quote(currentContract.row.ToString(CultureInfo.InvariantCulture)) + " " +
                    Quote(CustomerNameBox.Text.Trim()) + " " +
                    Quote(CustomerIDBox.Text.Trim()) + " " +
                    Quote(basePrice.ToString(CultureInfo.InvariantCulture)) + " " +
                    Quote(agreedQty.ToString(CultureInfo.InvariantCulture)) + " " +
                    Quote(penaltyPercent.ToString(CultureInfo.InvariantCulture)) + " " +
                    Quote(remedyDays.ToString(CultureInfo.InvariantCulture)) + " " +
                    Quote(responsibility);

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

            if (!TryReadDouble(BasePriceBox.Text, out basePrice) || basePrice <= 0)
            {
                ShowValidation("Base Price must be greater than zero.", BasePriceBox);
                return false;
            }

            if (!TryReadDouble(AgreedQtyBox.Text, out agreedQty) || agreedQty <= 0)
            {
                ShowValidation("Agreed Quantity must be greater than zero.", AgreedQtyBox);
                return false;
            }

            if (!TryReadDouble(PenaltyPercentBox.Text, out penaltyPercent) ||
                penaltyPercent < 0 ||
                penaltyPercent > 100)
            {
                ShowValidation("Penalty / Discount must be between 0 and 100.", PenaltyPercentBox);
                return false;
            }

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

            return true;
        }

        private void SelectResponsibility(string? responsibility)
        {
            string target =
                string.IsNullOrWhiteSpace(responsibility)
                    ? "Pending"
                    : responsibility.Trim();

            foreach (ComboBoxItem item in ResponsibilityComboBox.Items)
            {
                string value = item.Content?.ToString() ?? "";

                if (value.Equals(target, StringComparison.OrdinalIgnoreCase))
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

        private static bool TryReadDouble(string text, out double value)
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

        private static string FormatNumberForInput(double? value)
        {
            return !value.HasValue
                ? ""
                : value.Value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static SolidColorBrush BrushFromHex(string hex)
        {
            return (SolidColorBrush)new BrushConverter().ConvertFromString(hex);
        }

        private static void ShowValidation(string message, Control control)
        {
            MessageBox.Show(
                message,
                "Validation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            control.Focus();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

