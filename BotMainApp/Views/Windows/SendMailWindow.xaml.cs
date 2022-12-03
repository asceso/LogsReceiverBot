using Microsoft.Win32;
using System;
using System.Linq;
using System.Windows;

namespace BotMainApp.Views.Windows
{
    /// <summary>
    /// Логика взаимодействия для SendMailWindow.xaml
    /// </summary>
    public partial class SendMailWindow : Window, IDisposable
    {
        public SendMailWindow(string targetUsername)
        {
            InitializeComponent();
            HeaderBox.Text = targetUsername;
        }

        private void AppendFileClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new()
            {
                Filter = " Изображения|*.jpg;*.jpeg;*.png",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            };
            bool? result = ofd.ShowDialog();
            if (result.HasValue && result.Value)
            {
                AttachmentTextBox.Text = ofd.FileName;
            }
        }

        private void OkButtonClick(object sender, RoutedEventArgs e)
        {
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

        private void AttachmentTextBoxDrop(object sender, DragEventArgs e)
        {
            string filename = ((string[])e.Data.GetData("FileDrop")).FirstOrDefault();
            AttachmentTextBox.Text = filename;
        }
    }
}