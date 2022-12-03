using Prism.Commands;
using Prism.Mvvm;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models.Database
{
    public class DublicateModel : BindableBase
    {
        private int id;
        private string url;
        private string login;
        private string password;
        private string category;
        private long uploadedByUserId;
        private string uploadedByUsername;
        private string singleRow;

        public int Id { get => id; set => SetProperty(ref id, value); }
        public string Url { get => url; set => SetProperty(ref url, value); }
        public string Login { get => login; set => SetProperty(ref login, value); }
        public string Password { get => password; set => SetProperty(ref password, value); }
        public string Category { get => category; set => SetProperty(ref category, value); }
        public long UploadedByUserId { get => uploadedByUserId; set => SetProperty(ref uploadedByUserId, value); }
        public string UploadedByUsername { get => uploadedByUsername; set => SetProperty(ref uploadedByUsername, value); }

        [NotMapped]
        public string SingleRow
        {
            get
            {
                singleRow = ToString();
                return singleRow;
            }

            set => SetProperty(ref singleRow, value);
        }

        [NotMapped]
        public DelegateCommand<string> OnCopyCommand { get; set; }

        public override string ToString() => $"{url}|{login}|{password}";
    }
}