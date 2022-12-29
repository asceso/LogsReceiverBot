using Prism.Commands;
using Prism.Mvvm;
using System.ComponentModel.DataAnnotations.Schema;
using static Models.Enums.CheckStatus;

namespace Models.Database
{
    [Table("wp_login_checks")]
    public class WpLoginCheckModel : BindableBase, ICloneable
    {
        private int id;
        private ManualCheckStatus status;
        private TimeSpan checkingTimeEllapsed;
        private DateTime startDateTime;
        private DateTime endDateTime;
        private long fromUserId;
        private string fromUsername;
        private int dublicateFoundedCount;
        private int dublicateFoundedCountManual;
        private int shellsFoundedCount;
        private int shellsFoundedCountManual;
        private int cpanelsResetedFoundedCount;
        private int cpanelsResetedFoundedCountManual;
        private int smtpsFoundedCount;
        private int smtpsFoundedCountManual;
        private int loggedWordpressFoundedCount;
        private int loggedWordpressFoundedCountManual;
        private string dublicateFilePath;
        private string shellsFilePath;
        private string cpanelsFilePath;
        private string smtpsFilePath;
        private string loggedWordpressFilePath;
        private string originalFilePath;
        private bool isManualCheckEnd;
        private bool isDublicatesFilledToDb;

        [Column("id")]
        public int Id { get => id; set => SetProperty(ref id, value); }

        [Column("status")]
        public ManualCheckStatus Status { get => status; set => SetProperty(ref status, value); }

        [Column("checking_time")]
        public TimeSpan CheckingTimeEllapsed { get => checkingTimeEllapsed; set => SetProperty(ref checkingTimeEllapsed, value); }

        [Column("start_at")]
        public DateTime StartDateTime { get => startDateTime; set => SetProperty(ref startDateTime, value); }

        [Column("end_at")]
        public DateTime EndDateTime { get => endDateTime; set => SetProperty(ref endDateTime, value); }

        [Column("from_user_id")]
        public long FromUserId { get => fromUserId; set => SetProperty(ref fromUserId, value); }

        [Column("from_username")]
        public string FromUsername { get => fromUsername; set => SetProperty(ref fromUsername, value); }

        [Column("dublicate_founded_count")]
        public int DublicateFoundedCount { get => dublicateFoundedCount; set => SetProperty(ref dublicateFoundedCount, value); }

        [Column("dublicate_founded_count_manual")]
        public int DublicateFoundedCountManual { get => dublicateFoundedCountManual; set => SetProperty(ref dublicateFoundedCountManual, value); }

        [Column("shells_founded_count")]
        public int ShellsFoundedCount { get => shellsFoundedCount; set => SetProperty(ref shellsFoundedCount, value); }

        [Column("shells_founded_count_manual")]
        public int ShellsFoundedCountManual { get => shellsFoundedCountManual; set => SetProperty(ref shellsFoundedCountManual, value); }

        [Column("cpanels_reseted_founded_count")]
        public int CpanelsResetedFoundedCount { get => cpanelsResetedFoundedCount; set => SetProperty(ref cpanelsResetedFoundedCount, value); }

        [Column("cpanels_reseted_founded_count_manual")]
        public int CpanelsResetedFoundedCountManual { get => cpanelsResetedFoundedCountManual; set => SetProperty(ref cpanelsResetedFoundedCountManual, value); }

        [Column("smtps_founded_count")]
        public int SmtpsFoundedCount { get => smtpsFoundedCount; set => SetProperty(ref smtpsFoundedCount, value); }

        [Column("smtps_founded_count_manual")]
        public int SmtpsFoundedCountManual { get => smtpsFoundedCountManual; set => SetProperty(ref smtpsFoundedCountManual, value); }

        [Column("logged_wordpress_founded_count")]
        public int LoggedWordpressFoundedCount { get => loggedWordpressFoundedCount; set => SetProperty(ref loggedWordpressFoundedCount, value); }

        [Column("logged_wordpress_founded_countmanual")]
        public int LoggedWordpressFoundedCountManual { get => loggedWordpressFoundedCountManual; set => SetProperty(ref loggedWordpressFoundedCountManual, value); }

        [Column("dublicate_file_path")]
        public string DublicateFilePath { get => dublicateFilePath; set => SetProperty(ref dublicateFilePath, value); }

        [Column("shells_file_path")]
        public string ShellsFilePath { get => shellsFilePath; set => SetProperty(ref shellsFilePath, value); }

        [Column("cpanels_file_path")]
        public string CpanelsFilePath { get => cpanelsFilePath; set => SetProperty(ref cpanelsFilePath, value); }

        [Column("smtps_file_path")]
        public string SmtpsFilePath { get => smtpsFilePath; set => SetProperty(ref smtpsFilePath, value); }

        [Column("logged_wordpress_file_path")]
        public string LoggedWordpressFilePath { get => loggedWordpressFilePath; set => SetProperty(ref loggedWordpressFilePath, value); }

        [Column("original_file_path")]
        public string OriginalFilePath { get => originalFilePath; set => SetProperty(ref originalFilePath, value); }

        [Column("is_manual_check_end")]
        public bool IsManualCheckEnd { get => isManualCheckEnd; set => SetProperty(ref isManualCheckEnd, value); }

        [Column("is_dublicates_filled_to_db")]
        public bool IsDublicatesFilledToDb { get => isDublicatesFilledToDb; set => SetProperty(ref isDublicatesFilledToDb, value); }

        [NotMapped]
        public DelegateCommand<string> OnCopyCommand { get; set; }

        [NotMapped]
        public DelegateCommand<WpLoginCheckModel> ResendToSoftManualCommand { get; set; }

        [NotMapped]
        public DelegateCommand<WpLoginCheckModel> OpenManualCheckCommand { get; set; }

        [NotMapped]
        public DelegateCommand<WpLoginCheckModel> OpenOriginalFileCommand { get; set; }

        [NotMapped]
        public DelegateCommand<WpLoginCheckModel> DeleteCheckCommand { get; set; }

        public WpLoginCheckModel()
        {
            DublicateFilePath = "";
            ShellsFilePath = "";
            CpanelsFilePath = "";
            SmtpsFilePath = "";
            LoggedWordpressFilePath = "";
            OriginalFilePath = "";
        }

        public object Clone() => MemberwiseClone();
    }
}