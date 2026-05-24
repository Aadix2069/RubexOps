using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace RubexOps
{
    /// <summary>
    /// Interaction logic for PurchasePage.xaml
    /// </summary>
    public partial class PurchasePage : Page
    {
        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        public PurchasePage()
        {
            InitializeComponent();

            EnsureBackendFolderExists();
        }



        // =====================================================
        // ENSURE BACKEND FOLDER EXISTS
        // =====================================================

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
                    Directory.CreateDirectory(
                        backendFolder
                    );
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



        // =====================================================
        // SHOW LOADING
        // =====================================================

        private void ShowLoading()
        {
            LoadingOverlay.Visibility =
                Visibility.Visible;
        }



        // =====================================================
        // HIDE LOADING
        // =====================================================

        private void HideLoading()
        {
            LoadingOverlay.Visibility =
                Visibility.Collapsed;
        }



        // =====================================================
        // CREATE CONTRACT
        // =====================================================

        private async void CreateContractButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                ShowLoading();



                await Task.Delay(400);



                NavigationService?.Navigate(
                    new CreatePurchaseContractPage()
                );
            }

            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Navigation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }

            finally
            {
                HideLoading();
            }
        }



        // =====================================================
        // VIEW / EDIT CONTRACTS
        // =====================================================

        private async void ViewEditContractsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                ShowLoading();



                await Task.Delay(400);



                NavigationService?.Navigate(
                    new ViewEditContractsPage()
                );
            }

            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Navigation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }

            finally
            {
                HideLoading();
            }
        }



        // =====================================================
        // RENEW CONTRACT
        // =====================================================
        private void EnterPurchaseData_Click(
    object sender,
    RoutedEventArgs e)
        {
            NavigationService?.Navigate(
                new EnterPurchaseDataPage());
        }

        private void ViewEditPurchaseData_Click(
            object sender,
            RoutedEventArgs e)
        {
            NavigationService?.Navigate(
                new ViewEditPurchaseDataPage());
        }

    }
}