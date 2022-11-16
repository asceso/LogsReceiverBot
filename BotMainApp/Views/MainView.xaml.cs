using BotMainApp.ViewModels;
using HandyControl.Controls;
using System.Composition;

namespace BotMainApp.Views
{
    /// <summary>
    /// Логика взаимодействия для MainView.xaml
    /// </summary>
    [Export]
    public partial class MainView : Window
    {
        public MainView()
        {
            InitializeComponent();
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.TrayIconVisibility = System.Windows.Visibility.Visible;
                vm.ShowInTaskbar = false;
                vm.CurrentWindowState = System.Windows.WindowState.Minimized;
                e.Cancel = true;
            }
        }

        private void SideMenuSelectionChanged(object sender, HandyControl.Data.FunctionEventArgs<object> e)
        {
            if (DataContext is MainViewModel vm)
            {
            }
        }
    }
}