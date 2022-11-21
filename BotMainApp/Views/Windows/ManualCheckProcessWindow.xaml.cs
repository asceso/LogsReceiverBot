using Models.Database;
using System;
using System.Windows;

namespace BotMainApp.Views.Windows
{
    /// <summary>
    /// Логика взаимодействия для ManualCheckProcessWindow.xaml
    /// </summary>
    public partial class ManualCheckProcessWindow : Window, IDisposable
    {
        public ManualCheckModel CheckingModel { get; set; }

        public ManualCheckProcessWindow(ManualCheckModel model)
        {
            InitializeComponent();
            CheckingModel = model;
            DataContext = CheckingModel;
        }

        private void OkButtonClick(object sender, RoutedEventArgs e)
        {
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