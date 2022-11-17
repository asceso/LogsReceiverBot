using Prism.Commands;
using Prism.Mvvm;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models.Database
{
    public class UserModel : BindableBase, ICloneable
    {
        private long id;
        private string username;
        private string firstname;
        private string lastname;
        private string language;
        private bool isAccepted;
        private bool isBanned;
        private DateTime registrationDate;
        private int logsUploaded;
        private double balance;

        public long Id { get => id; set => SetProperty(ref id, value); }
        public string Username { get => username; set => SetProperty(ref username, value); }
        public string Firstname { get => firstname; set => SetProperty(ref firstname, value); }
        public string Lastname { get => lastname; set => SetProperty(ref lastname, value); }
        public string Language { get => language; set => SetProperty(ref language, value); }
        public bool IsAccepted { get => isAccepted; set => SetProperty(ref isAccepted, value); }
        public bool IsBanned { get => isBanned; set => SetProperty(ref isBanned, value); }
        public DateTime RegistrationDate { get => registrationDate; set => SetProperty(ref registrationDate, value); }
        public int LogsUploaded { get => logsUploaded; set => SetProperty(ref logsUploaded, value); }
        public double Balance { get => balance; set => SetProperty(ref balance, value); }

#if DEBUG

        public void FillRandom()
        {
            List<string> words = new() { "home", "dead", "moralez", "ooo", "exact" };
            List<string> langs = new() { "en", "ru" };

            Id = Random.Shared.Next(100000000, 999999999);
            Username = words[Random.Shared.Next(words.Count)];
            Firstname = words[Random.Shared.Next(words.Count)];
            Lastname = words[Random.Shared.Next(words.Count)];
            Language = langs[Random.Shared.Next(langs.Count)];
            RegistrationDate = DateTime.Now.AddDays(Random.Shared.Next(10, 90)).AddMinutes(Random.Shared.Next(1, 99));
            IsAccepted = Random.Shared.NextDouble() > 0.5;
            IsBanned = Random.Shared.NextDouble() > 0.5;
        }

#endif

        private bool isSelected;

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