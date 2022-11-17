using System;
using System.Windows;

namespace BotMainApp.Views.Windows
{
    /// <summary>
    /// Логика взаимодействия для SendMailWindow.xaml
    /// </summary>
    public partial class SendMailWindow : Window, IDisposable
    {
        public string OutputText { get; set; }

        public SendMailWindow(string targetUsername)
        {
            InitializeComponent();
            HeaderBox.Text = targetUsername;
        }

        private void OkButtonClick(object sender, RoutedEventArgs e)
        {
            OutputText = InputTextBox.Text;
            DialogResult = true;
        }

        private void CancelButtonClick(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }
    }
}