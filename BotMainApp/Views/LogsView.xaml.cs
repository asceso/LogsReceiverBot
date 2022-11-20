using BotMainApp.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;
using WpfToolkit.Controls;

namespace BotMainApp.Views
{
    /// <summary>
    /// Логика взаимодействия для LogsView.xaml
    /// </summary>
    public partial class LogsView : UserControl
    {
        public LogsView()
        {
            InitializeComponent();
        }

        private void ScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            DataScroller.ScrollToHorizontalOffset(e.HorizontalOffset);
        }

        private void VirtualizingWrapPanelPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is VirtualizingWrapPanel vwp)
            {
                if (e.Delta < 0)
                {
                    vwp.LineDown();
                }
                else
                {
                    vwp.LineUp();
                }
            }
        }

        private async void PaginationPageUpdated(object sender, HandyControl.Data.FunctionEventArgs<int> e)
        {
            if (DataContext is LogsViewModel vm)
            {
                await vm.ReloadByPage(e.Info);
            }
        }
    }
}