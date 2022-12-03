using BotMainApp.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace BotMainApp.Views
{
    /// <summary>
    /// Логика взаимодействия для LogsView.xaml
    /// </summary>
    public partial class DublicatesView : UserControl
    {
        public DublicatesView()
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
            if (DataContext is DublicatesViewModel vm)
            {
                await vm.ReloadByPage(e.Info);
                if (DataView.ItemsPanel.GetType() == typeof(VirtualizingStackPanel))
                {
                }
            }
        }
    }
}