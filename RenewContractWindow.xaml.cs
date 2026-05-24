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
    public partial class RenewContractWindow : Window
    {
        private readonly List<PurchaseContract> renewableContracts;
        private readonly string pythonExe = "python";

        public RenewContractWindow(List<PurchaseContract> contracts)
        {
            InitializeComponent();

            renewableContracts = contracts
                .Where(c => !c.IsExpiredStatus &&
                            string.IsNullOrWhiteSpace(c.renewal_reference) &&
                            (c.DisplayStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
                             c.DisplayStatus.Equals("Violated", StringComparison.OrdinalIgnoreCase)))
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
                    "No renewable contracts are available. Only current Completed or Violated contracts can be renewed.",
                    "Renew Contract",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void ContractComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ContractComboBox.SelectedItem is not PurchaseContract contract)
            {
                ClearDisplay();
                return;
            }

            VendorNameBox.Text = contract.vendor_name ?? "";
            VendorIDBox.Text = contract.vendor_id ?? "";
            StatusBox.Text = contract.DisplayStatus;

            BasePriceBox.Text = contract.base_price?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
            AgreedQtyBox.Text = contract.agreed_qty?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
            PenaltyPercentBox.Text = contract.penalty_percent?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
            RemedyDaysBox.Text = contract.remedy_days?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
        }

        private void RenewButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ContractComboBox.SelectedItem is not PurchaseContract contract)
                {
                    MessageBox.Show("Please select a contract.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!ValidateForm(out double basePrice, out double agreedQty, out double penaltyPercent, out int remedyDays))
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
                    "renew_purchase_contract.py");

                if (!File.Exists(pythonScript))
                {
                    MessageBox.Show("Backend Python file not found.\n\n" + pythonScript, "File Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string startDate = StartDatePicker.SelectedDate?.ToString("dd-MM-yyyy") ?? "";
                string endDate = EndDatePicker.SelectedDate?.ToString("dd-MM-yyyy") ?? "";

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
                    FileName = pythonExe,
                    Arguments = $"\"{pythonScript}\" {arguments}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backend")
                };

                using Process process = Process.Start(start)
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

                if (output.TrimStart().StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(output, "Backend Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                MessageBox.Show(output, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                MainGrid.IsEnabled = true;
                MainGrid.Opacity = 1;
                RenewButton.Content = "Renew Contract";
            }
        }

        private bool ValidateForm(out double basePrice, out double agreedQty, out double penaltyPercent, out int remedyDays)
        {
            basePrice = 0;
            agreedQty = 0;
            penaltyPercent = 0;
            remedyDays = 0;

            if (StartDatePicker.SelectedDate == null)
            {
                MessageBox.Show("Please select a new Start Date.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                StartDatePicker.Focus();
                return false;
            }

            if (EndDatePicker.SelectedDate == null)
            {
                MessageBox.Show("Please select a new End Date.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                EndDatePicker.Focus();
                return false;
            }

            if (EndDatePicker.SelectedDate <= StartDatePicker.SelectedDate)
            {
                MessageBox.Show("New End Date must be after New Start Date.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                EndDatePicker.Focus();
                return false;
            }

            if (!ReadDouble(BasePriceBox.Text, out basePrice) || basePrice <= 0)
            {
                MessageBox.Show("Base Price must be greater than zero.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                BasePriceBox.Focus();
                return false;
            }

            if (!ReadDouble(AgreedQtyBox.Text, out agreedQty) || agreedQty <= 0)
            {
                MessageBox.Show("Agreed Quantity must be greater than zero.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                AgreedQtyBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(PenaltyPercentBox.Text))
            {
                penaltyPercent = 0;
            }
            else if (!ReadDouble(PenaltyPercentBox.Text.Replace("%", ""), out penaltyPercent) || penaltyPercent < 0 || penaltyPercent > 100)
            {
                MessageBox.Show("Penalty Percentage must be between 0 and 100.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                PenaltyPercentBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(RemedyDaysBox.Text))
            {
                remedyDays = 0;
            }
            else if (!int.TryParse(RemedyDaysBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out remedyDays) || remedyDays < 0)
            {
                MessageBox.Show("Remedy Days must be a non-negative whole number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                RemedyDaysBox.Focus();
                return false;
            }

            return true;
        }

        private static bool ReadDouble(string text, out double value)
        {
            return double.TryParse(
                text.Trim().Replace(",", "").Replace("%", ""),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private void ClearDisplay()
        {
            VendorNameBox.Clear();
            VendorIDBox.Clear();
            StatusBox.Clear();
            BasePriceBox.Clear();
            AgreedQtyBox.Clear();
            PenaltyPercentBox.Clear();
            RemedyDaysBox.Clear();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public partial class PurchaseContract
    {
        public string RenewDisplay =>
            $"{vendor_name} - {vendor_id} | Row {row} | {DisplayStatus}";
    }
}