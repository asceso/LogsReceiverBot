using System.Windows.Controls;

namespace BotMainApp.Views
{
    /// <summary>
    /// Логика взаимодействия для PayoutsView.xaml
    /// </summary>
    public partial class PayoutsView : UserControl
    {
        public PayoutsView()
        {
            InitializeComponent();
        }

        private void ScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            DataScroller.ScrollToHorizontalOffset(e.HorizontalOffset);
        }
    }
}