using Prism.Commands;
using Prism.Mvvm;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models.Database
{
    [Table("users")]
    public class UserModel : BindableBase, ICloneable
    {
        private long id;
        private string username;
        private string firstname;
        private string lastname;
        private string language;
        private string banReason;
        private bool isAccepted;
        private bool isBanned;
        private DateTime registrationDate;
        private int logsUploaded;
        private double balance;
        private bool isSelected;

        [Column("id")]
        public long Id { get => id; set => SetProperty(ref id, value); }

        [Column("username")]
        public string Username { get => username; set => SetProperty(ref username, value); }

        [Column("firstname")]
        public string Firstname { get => firstname; set => SetProperty(ref firstname, value); }

        [Column("lastname")]
        public string Lastname { get => lastname; set => SetProperty(ref lastname, value); }

        [Column("lang")]
        public string Language { get => language; set => SetProperty(ref language, value); }

        [Column("ban_reason")]
        public string BanReason { get => banReason; set => SetProperty(ref banReason, value); }

        [Column("is_accepted")]
        public bool IsAccepted { get => isAccepted; set => SetProperty(ref isAccepted, value); }

        [Column("is_banned")]
        public bool IsBanned { get => isBanned; set => SetProperty(ref isBanned, value); }

        [Column("registered_at")]
        public DateTime RegistrationDate { get => registrationDate; set => SetProperty(ref registrationDate, value); }

        [Column("logs_uploaded_count")]
        public int LogsUploaded { get => logsUploaded; set => SetProperty(ref logsUploaded, value); }

        [Column("balance")]
        public double Balance { get => balance; set => SetProperty(ref balance, value); }

        [NotMapped]
        public bool IsSelected { get => isSelected; set => SetProperty(ref isSelected, value); }

        [NotMapped]
        public DelegateCommand<string> OnCopyCommand { get; set; }

        [NotMapped]
        public DelegateCommand<UserModel> AcceptAccessCommand { get; set; }

        [NotMapped]
        public DelegateCommand<UserModel> MoveToBLCommand { get; set; }

        [NotMapped]
        public DelegateCommand<UserModel> MoveFromBLCommand { get; set; }

        [NotMapped]
        public DelegateCommand<UserModel> SendMailCommand { get; set; }

        [NotMapped]
        public DelegateCommand<UserModel> ChangeCashCommand { get; set; }

        public string GetShortName()
        {
            if (!string.IsNullOrEmpty(Username))
            {
                return Username;
            }
            else
            {
                return $"{Firstname}_{Lastname}";
            }
        }

        public object Clone() => MemberwiseClone();
    }
}