using System.Windows.Controls;

namespace BotMainApp.Views
{
    /// <summary>
    /// Логика взаимодействия для WpLoginView.xaml
    /// </summary>
    public partial class WpLoginView : UserControl
    {
        public WpLoginView()
        {
            InitializeComponent();
        }

        private void ScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            DataScroller.ScrollToHorizontalOffset(e.HorizontalOffset);
        }
    }
}