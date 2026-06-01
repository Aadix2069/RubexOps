using System;
using System.Windows;
using System.Windows.Controls;

namespace RubexOps
{
    public partial class SalesPage : Page
    {
        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        public SalesPage()
        {
            InitializeComponent();
        }



        // =====================================================
        // CONTRACT MANAGEMENT NAVIGATION
        // =====================================================

        private void CreateContract_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new CreateSalesContractPage());
        }

        private void ViewContracts_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new ViewEditSalesContractsPage());
        }



        // =====================================================
        // DATA MANAGEMENT NAVIGATION
        // =====================================================

        private void EnterSalesData_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new EnterSalesDataPage());
        }

        private void ViewEditSalesData_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new ViewEditSalesDataPage());
        }



        // =====================================================
        // SAFE NAVIGATION
        // =====================================================

        private void NavigateToPage(
            Page page)
        {
            System.Windows.Navigation.NavigationService navigationService =
                System.Windows.Navigation.NavigationService.GetNavigationService(this);

            if (navigationService == null)
            {
                MessageBox.Show(
                    "Navigation service is not available for this page.",
                    "Navigation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            navigationService.Navigate(page);
        }
    }
}