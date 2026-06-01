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
    public partial class ViewEditSalesContractsPage : Page
    {
        private const string KilogramUnit = "Kg";
        private const string TonnesUnit = "Tonnes";

        private const string AllContractsFilter = "All Contracts";
        private const string AllContractsIncludingExpiredFilter = "All Contracts (Including Expired)";

        private static bool sessionFocusMode;
        private static int sessionCardsPerRow = 3;

        public static readonly DependencyProperty CardMinWidthProperty =
            DependencyProperty.Register(
                nameof(CardMinWidth),
                typeof(double),
                typeof(ViewEditSalesContractsPage),
                new PropertyMetadata(300.0));

        private readonly Dictionary<FrameworkElement, Visibility> focusHiddenElements =
            new();

        private List<SalesContract> allContracts =
            new();

        private readonly string pythonExe =
            "python";

        private string selectedWeightUnit =
            KilogramUnit;

        public double CardMinWidth
        {
            get => (double)GetValue(CardMinWidthProperty);
            set => SetValue(CardMinWidthProperty, value);
        }

        public ViewEditSalesContractsPage()
        {
            InitializeComponent();

            EnsureBackendFolderExists();

            SortComboBox.SelectedIndex = 0;

            FilterComboBox.SelectedIndex = 0;

            RestoreSessionViewState();

            LoadContracts();
        }

        private void EnsureBackendFolderExists()
        {
            try
            {
                string backendFolder =
                    Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "backend"
                    );

                if (!Directory.Exists(backendFolder))
                {
                    Directory.CreateDirectory(backendFolder);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Backend Folder Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void RestoreSessionViewState()
        {
            SelectCardsPerRow(sessionCardsPerRow);

            FocusModeCheckBox.IsChecked =
                sessionFocusMode;

            ApplyFocusMode(
                sessionFocusMode);
        }

        private void SelectCardsPerRow(
            int cardsPerRow)
        {
            foreach (ComboBoxItem item in CardsPerRowComboBox.Items)
            {
                string text =
                    item.Content?.ToString() ?? "";

                if (int.TryParse(
                        text,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int value) &&
                    value == cardsPerRow)
                {
                    CardsPerRowComboBox.SelectedItem =
                        item;

                    ApplyCardsPerRow(
                        value);

                    return;
                }
            }

            CardsPerRowComboBox.SelectedIndex = 0;

            ApplyCardsPerRow(3);
        }

        private void LoadContracts()
        {
            try
            {
                Mouse.OverrideCursor =
                    Cursors.Wait;

                MainGrid.IsEnabled = false;

                MainGrid.Opacity = 0.75;

                string pythonScript = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "backend",
                    "read_sales_contract.py"
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

                ProcessStartInfo start =
                    new()
                    {
                        FileName = pythonExe,

                        Arguments =
                            $"\"{pythonScript}\"",

                        UseShellExecute = false,

                        RedirectStandardOutput = true,

                        RedirectStandardError = true,

                        CreateNoWindow = true,

                        WorkingDirectory =
                            Path.Combine(
                                AppDomain.CurrentDomain.BaseDirectory,
                                "backend")
                    };

                using Process process =
                    Process.Start(start)
                    ?? throw new Exception(
                        "Failed to start backend process."
                    );

                string json =
                    process.StandardOutput.ReadToEnd();

                string error =
                    process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string message =
                        !string.IsNullOrWhiteSpace(error)
                            ? error.Trim()
                            : json.Trim();

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
                        error,
                        "Backend Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

                    return;
                }

                if (json.TrimStart().StartsWith(
                        "ERROR",
                        StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        json,
                        "Backend Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

                    return;
                }

                JsonSerializerOptions options =
                    new()
                    {
                        PropertyNameCaseInsensitive = true
                    };

                allContracts =
                    JsonSerializer.Deserialize<List<SalesContract>>(
                        json,
                        options
                    ) ?? new();

                ApplyWeightDisplayUnit();

                ApplySortingAndSearch();
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
            }
        }

        private void SearchBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            ApplySortingAndSearch();
        }

        private void FilterComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            ApplySortingAndSearch();
        }

        private void SortComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            ApplySortingAndSearch();
        }

        private void UnitRadioButton_Checked(
            object sender,
            RoutedEventArgs e)
        {
            selectedWeightUnit =
                TonnesRadioButton?.IsChecked == true
                    ? TonnesUnit
                    : KilogramUnit;

            ApplyWeightDisplayUnit();

            ApplySortingAndSearch();
        }

        private void CardsPerRowComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (CardsPerRowComboBox?.SelectedItem is not ComboBoxItem item)
            {
                return;
            }

            string text =
                item.Content?.ToString() ?? "";

            if (!int.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int cardsPerRow))
            {
                cardsPerRow = 3;
            }

            ApplyCardsPerRow(
                cardsPerRow);
        }

        private void ApplyCardsPerRow(
            int cardsPerRow)
        {
            if (cardsPerRow < 3 ||
                cardsPerRow > 5)
            {
                cardsPerRow = 3;
            }

            sessionCardsPerRow =
                cardsPerRow;

            CardMinWidth =
                cardsPerRow switch
                {
                    4 => 240.0,
                    5 => 200.0,
                    _ => 300.0
                };

            if (ContractsItemsControl != null)
            {
                ContractsItemsControl.Tag =
                    cardsPerRow;
            }
        }

        private void FocusModeCheckBox_Checked(
            object sender,
            RoutedEventArgs e)
        {
            bool enabled =
                FocusModeCheckBox?.IsChecked == true;

            sessionFocusMode =
                enabled;

            ApplyFocusMode(
                enabled);
        }

        private void ApplyFocusMode(
            bool enabled)
        {
            HeaderSection.Visibility =
                enabled ? Visibility.Collapsed : Visibility.Visible;

            StatusLegendPanel.Visibility =
                enabled ? Visibility.Collapsed : Visibility.Visible;

            WatermarkLogo.Visibility =
                enabled ? Visibility.Collapsed : Visibility.Visible;

            HeaderRow.Height =
                enabled ? new GridLength(0) : GridLength.Auto;

            HeaderSpacerRow.Height =
                enabled ? new GridLength(0) : new GridLength(18);

            LegendRow.Height =
                enabled ? new GridLength(0) : GridLength.Auto;

            LegendSpacerRow.Height =
                enabled ? new GridLength(0) : new GridLength(18);

            SetExternalNavigationFocusMode(
                enabled);
        }

        private void SetExternalNavigationFocusMode(
            bool enabled)
        {
            Window? window =
                Window.GetWindow(this);

            if (window == null)
            {
                return;
            }

            string[] possibleNavigationNames =
            {
                "NavigationRail",
                "NavigationPanel",
                "NavigationSidebar",
                "Sidebar",
                "SideBar",
                "LeftSidebar",
                "LeftNavigation",
                "NavPanel",
                "NavGrid",
                "MenuGrid",
                "SideMenu"
            };

            if (enabled)
            {
                foreach (string name in possibleNavigationNames)
                {
                    if (window.FindName(name) is FrameworkElement element &&
                        element != this)
                    {
                        if (!focusHiddenElements.ContainsKey(element))
                        {
                            focusHiddenElements[element] =
                                element.Visibility;
                        }

                        element.Visibility =
                            Visibility.Collapsed;
                    }
                }

                return;
            }

            foreach (KeyValuePair<FrameworkElement, Visibility> item in focusHiddenElements)
            {
                item.Key.Visibility =
                    item.Value;
            }

            focusHiddenElements.Clear();
        }

        private void ApplyWeightDisplayUnit()
        {
            foreach (SalesContract contract in allContracts)
            {
                contract.DisplayUnit =
                    selectedWeightUnit;
            }
        }

        private void ApplySortingAndSearch()
        {
            if (ContractsItemsControl == null)
            {
                return;
            }

            IEnumerable<SalesContract> filtered =
                allContracts;

            string search =
                SearchBox?.Text?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                filtered = filtered.Where(c =>

                    (!string.IsNullOrWhiteSpace(c.customer_name) &&
                     c.customer_name.Trim().StartsWith(
                         search,
                         StringComparison.OrdinalIgnoreCase))

                    ||

                    (!string.IsNullOrWhiteSpace(c.customer_id) &&
                     c.customer_id.Trim().StartsWith(
                         search,
                         StringComparison.OrdinalIgnoreCase)));
            }

            string filter =
                (FilterComboBox?.SelectedItem as ComboBoxItem)
                ?.Content
                ?.ToString()
                ?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(filter) ||
                filter.Equals(
                    AllContractsFilter,
                    StringComparison.OrdinalIgnoreCase) ||
                filter.Equals(
                    "All",
                    StringComparison.OrdinalIgnoreCase))
            {
                filtered =
                    filtered.Where(c => !c.IsExpiredStatus);
            }
            else if (filter.Equals(
                         AllContractsIncludingExpiredFilter,
                         StringComparison.OrdinalIgnoreCase))
            {
                // Show everything, including expired contracts.
            }
            else
            {
                filtered =
                    filtered.Where(c =>
                        c.DisplayStatus.Equals(
                            filter,
                            StringComparison.OrdinalIgnoreCase));
            }

            filtered =
                ApplySort(filtered);

            ContractsItemsControl.ItemsSource =
                filtered.ToList();
        }

        private IEnumerable<SalesContract> ApplySort(
            IEnumerable<SalesContract> contracts)
        {
            string sort =
                (SortComboBox?.SelectedItem as ComboBoxItem)
                ?.Content
                ?.ToString()
                ?.Trim() ?? "Name Ascending";

            return sort switch
            {
                "Name Descending" =>
                    contracts
                        .OrderByDescending(c => c.customer_name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(c => c.customer_id ?? string.Empty, StringComparer.OrdinalIgnoreCase),

                "Agreed Qty Ascending" =>
                    contracts
                        .OrderBy(c => c.agreed_qty ?? 0)
                        .ThenBy(c => c.customer_name ?? string.Empty, StringComparer.OrdinalIgnoreCase),

                "Agreed Qty Descending" =>
                    contracts
                        .OrderByDescending(c => c.agreed_qty ?? 0)
                        .ThenBy(c => c.customer_name ?? string.Empty, StringComparer.OrdinalIgnoreCase),

                _ =>
                    contracts
                        .OrderBy(c => c.customer_name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(c => c.StartDateSort)
            };
        }

        private void RenewContract_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                RenewSalesContractWindow window =
                    new RenewSalesContractWindow(allContracts);

                bool? result =
                    window.ShowDialog();

                if (result == true)
                {
                    LoadContracts();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Renew Sales Contract Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void EditButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                if (sender is not Button button)
                {
                    return;
                }

                if (button.DataContext is not SalesContract contract)
                {
                    return;
                }

                if (!contract.CanEdit)
                {
                    MessageBox.Show(
                        "Expired historical contracts are read-only.",
                        "Contract Locked",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );

                    return;
                }

                EditSalesContractWindow window =
                    new(contract);

                bool? result =
                    window.ShowDialog();

                if (result == true)
                {
                    LoadContracts();
                }
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
        }
    }

    public partial class SalesContract
    {
        private const double RingCenter = 48.0;
        private const double RingRadius = 36.0;

        public int row { get; set; }

        public string? customer_name { get; set; }

        public string? customer_id { get; set; }

        public string? contract_id { get; set; }

        public string? item_name { get; set; }

        public string? item_code { get; set; }

        public string? status { get; set; }

        public string? dynamic_status { get; set; }

        public string? status_color { get; set; }

        public string? start_date { get; set; }

        public string? end_date { get; set; }

        public string? days_remaining { get; set; }

        public string? sales_so_far { get; set; }

        public string? recent_dispatch { get; set; }

        public double? base_price { get; set; }

        public double? agreed_qty { get; set; }

        public double? sold_qty { get; set; }

        public double? qty_sold_so_far { get; set; }

        public double? remaining_qty { get; set; }

        public double? completion_percent { get; set; }

        public string? breach { get; set; }

        public string? breach_responsibility { get; set; }

        public double? penalty_percent { get; set; }

        public double? revised_rate { get; set; }

        public double? penalty_value { get; set; }

        public double? remedy_days { get; set; }

        public string? remedy_deadline { get; set; }

        public string? remedy_status { get; set; }

        public string? renewal_reference { get; set; }

        public bool can_renew { get; set; }

        public bool can_edit { get; set; } = true;

        public bool is_expired { get; set; }

        public bool is_breach_detected { get; set; }

        public bool show_responsibility { get; set; }

        public bool show_breach_panel { get; set; }

        public string DisplayUnit { get; set; } = "Kg";

        public string DisplayStatus
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(dynamic_status))
                {
                    return dynamic_status;
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    return status;
                }

                return "Unknown";
            }
        }

        public bool IsExpiredStatus
        {
            get
            {
                return is_expired ||
                       DisplayStatus.Equals(
                           "Expired",
                           StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool CanEdit
        {
            get
            {
                return can_edit && !IsExpiredStatus;
            }
        }

        public string EditToolTip
        {
            get
            {
                return CanEdit
                    ? "Edit contract"
                    : "Expired historical contracts are read-only";
            }
        }

        public double CardOpacity
        {
            get
            {
                return IsExpiredStatus ? 0.72 : 1.0;
            }
        }

        public string StatusBackground
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(status_color))
                {
                    return status_color;
                }

                return DisplayStatus switch
                {
                    "Active" => "#10B981",
                    "Upcoming" => "#3B82F6",
                    "Completed" => "#8B5CF6",
                    "Violated" => "#EF4444",
                    "Expired" => "#F59E0B",
                    _ => "#9CA3AF"
                };
            }
        }

        public int StatusPriority
        {
            get
            {
                return DisplayStatus switch
                {
                    "Active" => 0,
                    "Upcoming" => 1,
                    "Violated" => 2,
                    "Completed" => 3,
                    "Expired" => 4,
                    _ => 5
                };
            }
        }

        public DateTime StartDateSort
        {
            get
            {
                if (DateTime.TryParseExact(
                        start_date,
                        "dd-MM-yyyy",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime parsedDate))
                {
                    return parsedDate;
                }

                return DateTime.MinValue;
            }
        }

        public string ContractIdDisplay =>
            string.IsNullOrWhiteSpace(contract_id)
                ? "-"
                : contract_id;

        public string ItemCodeDisplay =>
            string.IsNullOrWhiteSpace(item_code)
                ? "ISNR20"
                : item_code;

        public string BasePriceDisplay =>
            $"Rs. {FormatPlainNumber(base_price)}";

        public string AgreedQtyDisplay =>
            FormatWeight(agreed_qty);

        public string SoldQtyDisplay =>
            FormatWeight(sold_qty);

        public string SoldQtySoFarDisplay =>
            FormatWeight(qty_sold_so_far ?? sold_qty);

        public string QtySoldSoFarDisplay =>
            FormatWeight(qty_sold_so_far ?? sold_qty);

        public string SalesSoFarDisplay =>
            string.IsNullOrWhiteSpace(sales_so_far)
                ? "0"
                : sales_so_far;

        public string RemainingQtyDisplay =>
            FormatWeight(remaining_qty);

        public string RevisedRateDisplay =>
            revised_rate.HasValue
                ? $"Rs. {FormatPlainNumber(revised_rate)}"
                : "Not calculated";

        public string BreachDisplay
        {
            get
            {
                return IsBreachDetected ? "YES" : "NO";
            }
        }

        public bool IsBreachDetected
        {
            get
            {
                if (is_breach_detected)
                {
                    return true;
                }

                string text =
                    breach?.Trim() ?? "";

                return text.Equals("YES", StringComparison.OrdinalIgnoreCase) ||
                       text.Equals("Y", StringComparison.OrdinalIgnoreCase) ||
                       text.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
            }
        }

        public Visibility BreachPanelVisibility
        {
            get
            {
                return IsBreachDetected || show_breach_panel
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        public double SafeCompletionPercent
        {
            get
            {
                double value =
                    completion_percent ?? 0;

                if (value < 0)
                {
                    return 0;
                }

                if (value > 100)
                {
                    return 100;
                }

                return value;
            }
        }

        public Geometry CompletionArcData
        {
            get
            {
                double percent =
                    SafeCompletionPercent;

                if (percent <= 0)
                {
                    return new PathGeometry();
                }

                PathFigure figure =
                    new()
                    {
                        StartPoint = PointOnRing(-90),
                        IsClosed = false
                    };

                if (percent >= 100)
                {
                    figure.Segments.Add(
                        new ArcSegment(
                            PointOnRing(90),
                            new Size(RingRadius, RingRadius),
                            0,
                            false,
                            SweepDirection.Clockwise,
                            true));

                    figure.Segments.Add(
                        new ArcSegment(
                            PointOnRing(270),
                            new Size(RingRadius, RingRadius),
                            0,
                            false,
                            SweepDirection.Clockwise,
                            true));
                }
                else
                {
                    double sweepAngle =
                        360.0 * percent / 100.0;

                    figure.Segments.Add(
                        new ArcSegment(
                            PointOnRing(-90 + sweepAngle),
                            new Size(RingRadius, RingRadius),
                            0,
                            sweepAngle > 180,
                            SweepDirection.Clockwise,
                            true));
                }

                PathGeometry geometry =
                    new();

                geometry.Figures.Add(figure);

                return geometry;
            }
        }

        public Brush CompletionBrush
        {
            get
            {
                return new SolidColorBrush(
                    GetCompletionColor(SafeCompletionPercent));
            }
        }

        public string CompletionDisplay =>
            $"{SafeCompletionPercent:0.##}%";

        public string DaysRemainingDisplay
        {
            get
            {
                if (string.IsNullOrWhiteSpace(days_remaining))
                {
                    return "0 days";
                }

                string text =
                    days_remaining.Trim();

                if (text.Contains(
                        "day",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }

                return $"{text} days";
            }
        }

        private bool UseTonnes =>
            DisplayUnit.Equals(
                "Tonnes",
                StringComparison.OrdinalIgnoreCase);

        private string FormatWeight(
            double? value)
        {
            if (!value.HasValue)
            {
                return UseTonnes
                    ? "0 Tonnes"
                    : "0 Kg";
            }

            if (UseTonnes)
            {
                double tonnes =
                    value.Value / 1000.0;

                return $"{tonnes.ToString("#,0.###", CultureInfo.InvariantCulture)} Tonnes";
            }

            return $"{value.Value.ToString("#,0.##", CultureInfo.InvariantCulture)} Kg";
        }

        private static string FormatPlainNumber(
            double? value)
        {
            if (!value.HasValue)
            {
                return "0";
            }

            return value.Value.ToString(
                "0.##",
                CultureInfo.InvariantCulture);
        }

        private static Point PointOnRing(
            double angleDegrees)
        {
            double radians =
                Math.PI * angleDegrees / 180.0;

            return new Point(
                RingCenter + RingRadius * Math.Cos(radians),
                RingCenter + RingRadius * Math.Sin(radians));
        }

        private static Color GetCompletionColor(
            double percent)
        {
            Color start =
                Color.FromRgb(134, 239, 172);

            Color middle =
                Color.FromRgb(14, 165, 168);

            Color end =
                Color.FromRgb(29, 78, 216);

            if (percent <= 50)
            {
                return BlendColor(
                    start,
                    middle,
                    percent / 50.0);
            }

            return BlendColor(
                middle,
                end,
                (percent - 50.0) / 50.0);
        }

        private static Color BlendColor(
            Color from,
            Color to,
            double amount)
        {
            amount =
                Math.Max(0, Math.Min(1, amount));

            return Color.FromRgb(
                (byte)Math.Round(from.R + ((to.R - from.R) * amount)),
                (byte)Math.Round(from.G + ((to.G - from.G) * amount)),
                (byte)Math.Round(from.B + ((to.B - from.B) * amount)));
        }
    }
}