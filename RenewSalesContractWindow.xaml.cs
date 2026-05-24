using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RubexOps
{
    public partial class RenewSalesContractWindow : Window
    {
        private readonly List<SalesContract> renewableContracts;

        public RenewSalesContractWindow(
            List<SalesContract> contracts)
        {
            InitializeComponent();

            renewableContracts =
                contracts
                    .Where(c => c.can_renew)
                    .ToList();

            ContractComboBox.ItemsSource = renewableContracts;

            if (renewableContracts.Count > 0)
            {
                ContractComboBox.SelectedIndex = 0;
            }
            else
            {
                RenewButton.IsEnabled = false;

                MessageBox.Show(
                    "No renewable sales contracts are available.",
                    "Renew Sales Contract",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void ContractComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (ContractComboBox.SelectedItem is not SalesContract contract)
            {
                ClearDisplay();
                return;
            }

            CustomerNameBox.Text = contract.customer_name ?? "";
            CustomerIDBox.Text = contract.customer_id ?? "";
            StatusBox.Text = contract.DisplayStatus;
            BasePriceBox.Text = FormatNumber(contract.base_price);
            AgreedQtyBox.Text = FormatNumber(contract.agreed_qty);
            PenaltyPercentBox.Text = FormatNumber(contract.penalty_percent);
            RemedyDaysBox.Text = FormatNumber(contract.remedy_days);
        }

        private void RenewButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                if (ContractComboBox.SelectedItem is not SalesContract contract)
                {
                    MessageBox.Show(
                        "Please select a sales contract.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!ValidateForm(
                        out double basePrice,
                        out double agreedQty,
                        out double penaltyPercent,
                        out int remedyDays))
                {
                    return;
                }

                Mouse.OverrideCursor = Cursors.Wait;
                MainGrid.IsEnabled = false;
                MainGrid.Opacity = 0.75;
                RenewButton.Content = "Renewing...";

                string pythonScript = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "backend",
                    "renew_sales_contract.py");

                if (!File.Exists(pythonScript))
                {
                    MessageBox.Show(
                        "Backend Python file not found.\n\n" + pythonScript,
                        "File Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                string startDate =
                    StartDatePicker.SelectedDate?.ToString("dd-MM-yyyy") ?? "";

                string endDate =
                    EndDatePicker.SelectedDate?.ToString("dd-MM-yyyy") ?? "";

                string arguments =
                    Quote(contract.row.ToString(CultureInfo.InvariantCulture)) + " " +
                    Quote(startDate) + " " +
                    Quote(endDate) + " " +
                    Quote(basePrice.ToString(CultureInfo.InvariantCulture)) + " " +
                    Quote(agreedQty.ToString(CultureInfo.InvariantCulture)) + " " +
                    Quote(penaltyPercent.ToString(CultureInfo.InvariantCulture)) + " " +
                    Quote(remedyDays.ToString(CultureInfo.InvariantCulture));

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
                RenewButton.Content = "Renew Contract";
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

            if (StartDatePicker.SelectedDate == null)
            {
                ShowValidation("Please select a new Start Date.", StartDatePicker);
                return false;
            }

            if (EndDatePicker.SelectedDate == null)
            {
                ShowValidation("Please select a new End Date.", EndDatePicker);
                return false;
            }

            if (EndDatePicker.SelectedDate <= StartDatePicker.SelectedDate)
            {
                ShowValidation("New End Date must be after New Start Date.", EndDatePicker);
                return false;
            }

            if (!ReadDouble(BasePriceBox.Text, out basePrice) || basePrice <= 0)
            {
                ShowValidation("Base Price must be greater than zero.", BasePriceBox);
                return false;
            }

            if (!ReadDouble(AgreedQtyBox.Text, out agreedQty) || agreedQty <= 0)
            {
                ShowValidation("Agreed Quantity must be greater than zero.", AgreedQtyBox);
                return false;
            }

            if (string.IsNullOrWhiteSpace(PenaltyPercentBox.Text))
            {
                penaltyPercent = 0;
            }
            else if (!ReadDouble(PenaltyPercentBox.Text, out penaltyPercent) ||
                     penaltyPercent < 0 ||
                     penaltyPercent > 100)
            {
                ShowValidation("Penalty / Discount must be between 0 and 100.", PenaltyPercentBox);
                return false;
            }

            if (string.IsNullOrWhiteSpace(RemedyDaysBox.Text))
            {
                remedyDays = 0;
            }
            else if (!int.TryParse(
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

        private static bool ReadDouble(
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

        private static string FormatNumber(
            double? value)
        {
            return !value.HasValue
                ? ""
                : value.Value.ToString("0.##", CultureInfo.InvariantCulture);
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

        private void ClearDisplay()
        {
            CustomerNameBox.Clear();
            CustomerIDBox.Clear();
            StatusBox.Clear();
            BasePriceBox.Clear();
            AgreedQtyBox.Clear();
            PenaltyPercentBox.Clear();
            RemedyDaysBox.Clear();
        }

        private void CancelButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }
    }
}

