using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace RubexOps
{
    public partial class MainWindow : Window
    {
        // =====================================================
        // CLOCK TIMER
        // =====================================================

        private DispatcherTimer? clockTimer;



        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        public MainWindow()
        {
            InitializeComponent();

            StartClock();

            NavigateToPage(
                new HomePage(),
                HomeButton
            );
        }



        // =====================================================
        // START CLOCK
        // =====================================================

        private void StartClock()
        {
            clockTimer = new DispatcherTimer();

            clockTimer.Interval =
                TimeSpan.FromSeconds(1);



            clockTimer.Tick += (sender, e) =>
            {
                DateTime now =
                    DateTime.Now;



                CurrentTimeText.Text =
                    now.ToString("dddd, hh:mm tt");



                CurrentDateText.Text =
                    now.ToString("dd MMMM yyyy");
            };



            clockTimer.Start();
        }



        // =====================================================
        // NAVIGATION HELPER
        // =====================================================

        private void NavigateToPage(
            Page page,
            Button activeButton)
        {
            MainFrame.Navigate(page);

            ResetNavigationStyles();

            ApplyActiveStyle(activeButton);
        }



        // =====================================================
        // RESET ALL BUTTON STYLES
        // =====================================================

        private void ResetNavigationStyles()
        {
            // MAIN BUTTONS

            HomeButton.Style =
                (Style)FindResource(
                    "NavButtonStyle"
                );

            PurchaseButton.Style =
                (Style)FindResource(
                    "NavButtonStyle"
                );

            SalesButton.Style =
                (Style)FindResource(
                    "NavButtonStyle"
                );

            ProductionButton.Style =
                (Style)FindResource(
                    "NavButtonStyle"
                );



            // BOTTOM BUTTONS

            SettingsButton.Style =
                (Style)FindResource(
                    "BottomNavButtonStyle"
                );

            AboutButton.Style =
                (Style)FindResource(
                    "BottomNavButtonStyle"
                );
        }



        // =====================================================
        // APPLY ACTIVE STYLE
        // =====================================================

        private void ApplyActiveStyle(
            Button button)
        {
            if (
                button == SettingsButton
                ||
                button == AboutButton
            )
            {
                button.Style =
                    (Style)FindResource(
                        "ActiveBottomNavButtonStyle"
                    );
            }
            else
            {
                button.Style =
                    (Style)FindResource(
                        "ActiveNavButtonStyle"
                    );
            }
        }



        // =====================================================
        // HOME
        // =====================================================

        private void HomeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            NavigateToPage(
                new HomePage(),
                HomeButton
            );
        }



        // =====================================================
        // PURCHASE
        // =====================================================

        private void PurchaseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            NavigateToPage(
                new PurchasePage(),
                PurchaseButton
            );
        }



        // =====================================================
        // SALES
        // =====================================================

        private void SalesButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            NavigateToPage(
                new SalesPage(),
                SalesButton
            );
        }



        // =====================================================
        // PRODUCTION
        // =====================================================

        private void ProductionButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            NavigateToPage(
                new ProductionPage(),
                ProductionButton
            );
        }



        // =====================================================
        // SETTINGS
        // =====================================================

        private void SettingsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            NavigateToPage(
                new SettingsPage(),
                SettingsButton
            );
        }



        // =====================================================
        // ABOUT
        // =====================================================

        private void AboutButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            NavigateToPage(
                new AboutPage(),
                AboutButton
            );
        }
    }
}