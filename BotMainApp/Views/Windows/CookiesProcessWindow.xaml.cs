using Models.Database;
using Notification.Wpf;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace BotMainApp.Views.Windows
{
    /// <summary>
    /// Логика взаимодействия для CookiesProcessWindow.xaml
    /// </summary>
    public partial class CookiesProcessWindow : Window, INotifyPropertyChanged, IDisposable
    {
        #region services

        private readonly NotificationManager notificationManager;

        #endregion services

        #region notify

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        #endregion notify

        #region fields

        private CookieModel checkingModel;
        private bool isEndButtonEnabled;

        #endregion fields

        #region props

        public CookieModel CheckingModel
        {
            get => checkingModel;
            set
            {
                checkingModel = value;
                OnPropertyChanged(nameof(CheckingModel));
            }
        }

        public bool IsEndButtonEnabled
        {
            get => isEndButtonEnabled;
            set
            {
                isEndButtonEnabled = value;
                OnPropertyChanged(nameof(IsEndButtonEnabled));
            }
        }

        #endregion props

        #region ctor

        public CookiesProcessWindow(CookieModel model, NotificationManager notificationManager, string currency)
        {
            InitializeComponent();
            CurrencyTextBlock.Text = currency;
            this.notificationManager = notificationManager;
            CheckingModel = (CookieModel)model.Clone();
            DataContext = this;
        }

        private void BorderMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }

        #endregion ctor

        #region buttons

        private void OkButtonClick(object sender, RoutedEventArgs e)
        {
            CheckingModel.Status = Models.Enums.CheckStatus.CookieCheckStatus.End;
            DialogResult = true;
        }

        private void OkNoValidButtonClick(object sender, RoutedEventArgs e)
        {
            CheckingModel.Status = Models.Enums.CheckStatus.CookieCheckStatus.EndNoValid;
            DialogResult = true;
        }

        private void CancelButtonClick(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        private void NumericValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            IsEndButtonEnabled = CheckingModel.ValidFound != 0 && CheckingModel.BalanceToUser != 0;
        }

        #endregion buttons
    }
}