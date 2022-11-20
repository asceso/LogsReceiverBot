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
        }

        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }
    }
}