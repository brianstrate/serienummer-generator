using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace seriel_number_generator
{
    public partial class ResultDialog : Window
    {
        private readonly string _folderPath;

        public ResultDialog(string message, string folderPath)
        {
            InitializeComponent();
            TxtMessage.Text = message;
            _folderPath = string.IsNullOrWhiteSpace(folderPath) ? "output" : folderPath;
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string fullPath = Path.GetFullPath(_folderPath);
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = fullPath,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kunne ikke åbne mappen: {ex.Message}", "Fejl", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}