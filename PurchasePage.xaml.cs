using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        // SET LOADING CURSOR
        // =====================================================

        private void StartLoadingCursor()
        {
            Mouse.OverrideCursor = Cursors.Wait;
        }



        // =====================================================
        // RESET CURSOR
        // =====================================================

        private void StopLoadingCursor()
        {
            Mouse.OverrideCursor = null;
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
                StartLoadingCursor();



                await Task.Delay(120);



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
                StopLoadingCursor();
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
                StartLoadingCursor();



                await Task.Delay(120);



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
                StopLoadingCursor();
            }
        }



        // =====================================================
        // ENTER PURCHASE DATA
        // =====================================================

        private async void EnterPurchaseData_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                StartLoadingCursor();



                await Task.Delay(120);



                NavigationService?.Navigate(
                    new EnterPurchaseDataPage()
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
                StopLoadingCursor();
            }
        }



        // =====================================================
        // VIEW / EDIT PURCHASE DATA
        // =====================================================

        private async void ViewEditPurchaseData_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                StartLoadingCursor();



                await Task.Delay(120);



                NavigationService?.Navigate(
                    new ViewEditPurchaseDataPage()
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
                StopLoadingCursor();
            }
        }
    }
}