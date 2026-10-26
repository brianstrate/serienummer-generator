using System.Collections.Generic;
using System.Windows;

namespace seriel_number_generator
{
    public partial class TemplateHelpWindow : Window
    {
        public TemplateHelpWindow()
        {
            InitializeComponent();
            LoadTokenHelp();
        }

        private void LoadTokenHelp()
        {
            var tokens = new List<TokenInfo>
            {
                new("<<serienumber>>", "Det genererede serienummer (YYYYMM + tæller)", "202610100"),
                new("<<standard>>", "Navn fra den valgte standard", "DS/EN ISO 13485"),
                new("<<udi>>", "UDI-kode fra valgt UDI-række", "(01)05712345678901"),
                new("<<ref>>", "REF-nummer fra valgt REF-række", "REF-7890"),
                new("<<raps_batch>>", "Batchnummer fra rapsfrø-tabellen", "RB-2026-A1"),
                new("<<textile_batch>>", "Batchnummer fra tekstil-tabellen", "TB-8842"),
                new("<<wash_temp>>", "Vasketemperatur / vaskeanvisning", "60°C"),
                new("<<created>>", "Oprettelsesdato og tidspunkt", "2026-10-19 14:30:00"),
                new("<<note>>", "Fritekst-note fra formularen", "Hastetest godkendt")
            };

            GridTokens.ItemsSource = tokens;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        public record TokenInfo(string Token, string Description, string Example);
    }
}