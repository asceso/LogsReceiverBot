using System.Windows.Controls;

namespace BotMainApp.Views
{
    /// <summary>
    /// Логика взаимодействия для ManualChecksView.xaml
    /// </summary>
    public partial class ManualChecksView : UserControl
    {
        public ManualChecksView()
        {
            InitializeComponent();
        }

        private void ScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            DataScroller.ScrollToHorizontalOffset(e.HorizontalOffset);
        }
    }
}