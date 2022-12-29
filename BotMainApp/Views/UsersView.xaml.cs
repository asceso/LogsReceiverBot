using BotMainApp.ViewModels;
using System.Windows.Controls;

namespace BotMainApp.Views
{
    /// <summary>
    /// Логика взаимодействия для UsersView.xaml
    /// </summary>
    public partial class UsersView : UserControl
    {
        public UsersView()
        {
            InitializeComponent();
        }

        private void DataUserCheckedOrUnchecked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is UsersViewModel vm)
            {
                vm.UpdateHasSelectedDataUsers();
            }
        }

        private void DataUserAreaMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.RightButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                if (sender is Border border)
                {
                    if (border.Child is CheckBox cb)
                    {
                        cb.IsChecked = true;
                    }
                }
            }
        }

        private void ScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            DataScroller.ScrollToHorizontalOffset(e.HorizontalOffset);
        }

        private void FilterTextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is UsersViewModel vm)
            {
                vm.DataUsersView.Refresh();
            }
        }
    }
}