using System.Windows.Controls;

namespace BotMainApp.Views
{
    /// <summary>
    /// Логика взаимодействия для CookiesView.xaml
    /// </summary>
    public partial class CookiesView : UserControl
    {
        public CookiesView()
        {
            InitializeComponent();
        }

        private void ScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            DataScroller.ScrollToHorizontalOffset(e.HorizontalOffset);
        }
    }
}