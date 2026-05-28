using System.Windows;
using System.Windows.Controls;

namespace RubexOps
{
    public partial class ProductionPage : Page
    {
        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        public ProductionPage()
        {
            InitializeComponent();
        }



        // =====================================================
        // NAVIGATION
        // =====================================================

   



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

