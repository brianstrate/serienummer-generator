using System.Windows;

namespace seriel_number_generator
{
    public partial class InputDialog : Window
    {
        public int NumberOfSerials { get; private set; } = 1;

        public InputDialog()
        {
            InitializeComponent();
            TxtCount.Focus();
            TxtCount.SelectAll();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtCount.Text.Trim(), out int count) && count > 0 && count <= 500)
            {
                NumberOfSerials = count;
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("Indtast venligst et gyldigt tal mellem 1 og 500.", "Ugyldigt antal", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}