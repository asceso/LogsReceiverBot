using BotMainApp.External;
using Models.Database;
using Notification.Wpf;
using Services;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace BotMainApp.Views.Windows
{
    /// <summary>
    /// Логика взаимодействия для CpanelWhmCheckProcessWindow.xaml
    /// </summary>
    public partial class CpanelWhmCheckProcessWindow : Window, INotifyPropertyChanged, IDisposable
    {
        #region services

        private readonly string notepadPath;
        private readonly NotificationManager notificationManager;

        #endregion services

        #region notify

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string prop = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        #endregion notify

        #region fields

        private CpanelWhmCheckModel checkingModel;
        private bool isEditEnable;
        private bool isFieldReadonly;
        private int totalFoundedValid;
        private int addBalance;
        private bool isNoAnyValid;

        #endregion fields

        #region props

        public CpanelWhmCheckModel CheckingModel
        {
            get => checkingModel;
            set
            {
                checkingModel = value;
                OnPropertyChanged(nameof(CheckingModel));
            }
        }

        public bool IsEditEnable
        {
            get => isEditEnable;
            set
            {
                isEditEnable = value;
                OnPropertyChanged(nameof(IsEditEnable));
            }
        }

        public bool IsFieldReadonly
        {
            get => isFieldReadonly;
            set
            {
                isFieldReadonly = value;
                OnPropertyChanged(nameof(IsFieldReadonly));
            }
        }

        public int TotalFoundedValid
        {
            get => totalFoundedValid;
            set
            {
                totalFoundedValid = value;
                OnPropertyChanged(nameof(TotalFoundedValid));
            }
        }

        public int AddBalance
        {
            get => addBalance;
            set
            {
                addBalance = value;
                OnPropertyChanged(nameof(AddBalance));
            }
        }

        public bool IsNoAnyValid
        {
            get => isNoAnyValid;
            set
            {
                isNoAnyValid = value;
                OnPropertyChanged(nameof(IsNoAnyValid));
            }
        }

        #endregion props

        #region ctor

        public CpanelWhmCheckProcessWindow(CpanelWhmCheckModel model, string notepadPath, NotificationManager notificationManager)
        {
            InitializeComponent();
            this.notepadPath = notepadPath;
            this.notificationManager = notificationManager;
            CheckingModel = (CpanelWhmCheckModel)model.Clone();
            DataContext = this;
            if (model.Status == Models.Enums.CheckStatus.ManualCheckStatus.End || model.Status == Models.Enums.CheckStatus.ManualCheckStatus.EndNoValid)
            {
                IsEditEnable = false;
                IsFieldReadonly = true;
                OneButtonGrid.Visibility = Visibility.Visible;
                TwoButtonsGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                IsEditEnable = true;
                IsFieldReadonly = false;

                CheckingModel.DublicateFoundedCountManual = CheckingModel.DublicateFoundedCount;
                CheckingModel.WebmailFoundedCountManual = CheckingModel.WebmailFoundedCount;
                CheckingModel.CpanelGoodCountManual = CheckingModel.CpanelGoodCount;
                CheckingModel.CpanelBadCountManual = CheckingModel.CpanelBadCount;
                CheckingModel.WhmGoodCountManual = CheckingModel.WhmGoodCount;
                CheckingModel.WhmBadCountManual = CheckingModel.WhmBadCount;

                TotalFoundedValid = CheckingModel.WebmailFoundedCountManual +
                                    CheckingModel.CpanelGoodCountManual +
                                    CheckingModel.WhmGoodCountManual;

                TwoButtonsGrid.Visibility = Visibility.Visible;
                OneButtonGrid.Visibility = Visibility.Collapsed;
            }
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
            IsNoAnyValid = false;
            DialogResult = true;
        }

        private void OkNoValidButtonClick(object sender, RoutedEventArgs e)
        {
            IsNoAnyValid = true;
            DialogResult = true;
        }

        private void CancelButtonClick(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        private void OpenFileClick(object sender, RoutedEventArgs e)
        {
            if (e.Source is Button btn)
            {
                string filepath = btn.Tag switch
                {
                    "DublicateFile" => CheckingModel.DublicateFilePath,
                    "WebmailFile" => CheckingModel.WebmailFilePath,
                    "CpanelGoodFile" => CheckingModel.CpanelGoodFilePath,
                    "CpanelBadFile" => CheckingModel.CpanelBadFilePath,
                    "WhmGoodFile" => CheckingModel.WhmGoodFilePath,
                    "WhmBadFile" => CheckingModel.WhmBadFilePath,
                    _ => "",
                };
                if (filepath == "")
                {
                    notificationManager.Show("Ошибка", "Не найден файл", type: NotificationType.Error);
                }
                else
                {
                    Runner.RunTextFileInNotepad(notepadPath, filepath);
                }
            }
        }

        private void OpenCheckFolderClick(object sender, RoutedEventArgs e)
        {
            string folderPath = PathCollection.CpanelAndWhmFolderPath + $"{CheckingModel.Id}/";
            if (Directory.Exists(folderPath))
            {
                Runner.RunExplorerWithPath(folderPath.Replace("/", "\\"));
            }
            else
            {
                notificationManager.Show("Ошибка", "Не найден путь к папке", type: NotificationType.Error);
            }
        }

        private void CopyToManualCountClick(object sender, RoutedEventArgs e)
        {
            if (e.Source is Button btn)
            {
                switch (btn.Tag)
                {
                    case "DublicateFile":
                        CheckingModel.DublicateFoundedCountManual = CheckingModel.DublicateFoundedCount;
                        break;

                    case "WebmailFile":
                        CheckingModel.WebmailFoundedCountManual = CheckingModel.WebmailFoundedCount;
                        break;

                    case "CpanelGoodFile":
                        CheckingModel.CpanelGoodCountManual = CheckingModel.CpanelGoodCount;
                        break;

                    case "CpanelBadFile":
                        CheckingModel.CpanelBadCountManual = CheckingModel.CpanelBadCount;
                        break;

                    case "WhmGoodFile":
                        CheckingModel.WhmGoodCountManual = CheckingModel.WhmGoodCount;
                        break;

                    case "WhmBadFile":
                        CheckingModel.WhmBadCountManual = CheckingModel.WhmBadCount;
                        break;

                    default:
                        break;
                }
            }
        }

        #endregion buttons
    }
}