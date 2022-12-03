using BotMainApp.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace BotMainApp.Views
{
    /// <summary>
    /// Логика взаимодействия для ValidView.xaml
    /// </summary>
    public partial class ValidView : UserControl
    {
        public ValidView()
        {
            InitializeComponent();
        }

        private void ScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            DataScroller.ScrollToHorizontalOffset(e.HorizontalOffset);
        }

        private void VirtualizingWrapPanelPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is VirtualizingStackPanel vwp)
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
            if (DataContext is ValidViewModel vm)
            {
                await vm.ReloadByPage(e.Info);
                DataScroller.ScrollToTop();
            }
        }
    }
}