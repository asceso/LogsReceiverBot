using Models.Database;
using System;
using System.Windows;
using System.Windows.Media;

namespace BotMainApp.Views.Windows
{
    /// <summary>
    /// Логика взаимодействия для ChangeBalanceWindow.xaml
    /// </summary>
    public partial class ChangeBalanceWindow : Window, IDisposable
    {
        private readonly SolidColorBrush defaultBrush = new(Colors.Black);
        private readonly SolidColorBrush lessBrush = new(Colors.Red);
        private readonly SolidColorBrush moreBrush = new(Colors.Green);
        private readonly UserModel user;
        public string OutputText { get; set; }
        public bool SendNotification { get; set; }

        public ChangeBalanceWindow(UserModel user, string currency)
        {
            InitializeComponent();
            this.user = user;
            IsSendNotification.IsChecked = true;
            HeaderBox.Text = user.Username;
            Balance.Text = user.Balance.ToString();
            Change.Value = 0;
            Total.Text = user.Balance.ToString();
            Total.Foreground = defaultBrush;
            BalanceCurrency.Text = currency;
            ChangeCurrency.Text = currency;
            TotalCurrency.Text = currency;
        }

        private void ChangeNumericValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            if (Change.Value == 0) Total.Foreground = defaultBrush;
            if (Change.Value < 0) Total.Foreground = lessBrush;
            if (Change.Value > 0) Total.Foreground = moreBrush;
            Total.Text = (user.Balance + Change.Value).ToString();
        }

        private void OkButtonClick(object sender, RoutedEventArgs e)
        {
            if (Change.Value == 0) OutputText = "none";
            if (Change.Value != 0) OutputText = Change.Value.ToString();
            SendNotification = IsSendNotification.IsChecked.Value;
            DialogResult = true;
        }

        private void CancelButtonClick(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }
    }
}