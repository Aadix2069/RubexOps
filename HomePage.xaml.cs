using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RubexOps
{
    /// <summary>
    /// Interaction logic for HomePage.xaml
    /// </summary>

    public partial class HomePage : Page
    {
        // =====================================================
        // UNIT MODE
        // =====================================================

        private bool isTonnes = true;



        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        public HomePage()
        {
            InitializeComponent();
        }



        // =====================================================
        // TONNES TOGGLE
        // =====================================================

        private void TonnesToggle_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (isTonnes)
                return;

            isTonnes = true;



            // =====================================================
            // TOGGLE BACKGROUND
            // =====================================================

            TonnesToggle.Background =
                new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#0B9AA0"));



            KgsToggle.Background =
                Brushes.Transparent;



            // =====================================================
            // UPDATE UNIT TEXT
            // =====================================================

            NRFCUnitText.Text = "Tonnes";

            ISNRUnitText.Text = "Tonnes";



            // =====================================================
            // PLACEHOLDER VALUES
            // =====================================================

            NRFCValueText.Text = "128.40";

            ISNRValueText.Text = "84.60";
        }



        // =====================================================
        // KGS TOGGLE
        // =====================================================

        private void KgsToggle_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (!isTonnes)
                return;

            isTonnes = false;



            // =====================================================
            // TOGGLE BACKGROUND
            // =====================================================

            KgsToggle.Background =
                new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#0B9AA0"));



            TonnesToggle.Background =
                Brushes.Transparent;



            // =====================================================
            // UPDATE UNIT TEXT
            // =====================================================

            NRFCUnitText.Text = "Kgs";

            ISNRUnitText.Text = "Kgs";



            // =====================================================
            // PLACEHOLDER VALUES
            // =====================================================

            NRFCValueText.Text = "128400";

            ISNRValueText.Text = "84600";
        }
    }
}