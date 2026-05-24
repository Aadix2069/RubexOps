using System;
using System.Windows;
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

            MainFrame.Navigate(new HomePage());
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
        // HOME
        // =====================================================

        private void HomeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MainFrame.Navigate(new HomePage());
        }



        // =====================================================
        // PURCHASE
        // =====================================================

        private void PurchaseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MainFrame.Navigate(new PurchasePage());
        }



        // =====================================================
        // SALES
        // =====================================================

        private void SalesButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MainFrame.Navigate(new SalesPage());
        }



        // =====================================================
        // PRODUCTION
        // =====================================================

        private void ProductionButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MainFrame.Navigate(new ProductionPage());
        }



        // =====================================================
        // SETTINGS
        // =====================================================

        private void SettingsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MainFrame.Navigate(new SettingsPage());
        }



        // =====================================================
        // ABOUT
        // =====================================================

        private void AboutButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MainFrame.Navigate(new AboutPage());
        }
    }
}