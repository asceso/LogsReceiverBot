using Prism.Mvvm;

namespace Models.Database
{
    public class LogModel : BindableBase
    {
        private int id;
        private string url;
        private string login;
        private string password;
        private long uploadedByUserId;
        private string uploadedByUsername;

        public int Id { get => id; set => SetProperty(ref id, value); }
        public string Url { get => url; set => SetProperty(ref url, value); }
        public string Login { get => login; set => SetProperty(ref login, value); }
        public string Password { get => password; set => SetProperty(ref password, value); }
        public long UploadedByUserId { get => uploadedByUserId; set => SetProperty(ref uploadedByUserId, value); }
        public string UploadedByUsername { get => uploadedByUsername; set => SetProperty(ref uploadedByUsername, value); }

        public override string ToString() => $"{url}|{login}|{password}";
    }
}