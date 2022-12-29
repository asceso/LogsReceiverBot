using Prism.Commands;
using Prism.Mvvm;
using System.ComponentModel.DataAnnotations.Schema;
using static Models.Enums.CheckStatus;

namespace Models.Database
{
    [Table("cpanel_whm_checks")]
    public class CpanelWhmCheckModel : BindableBase, ICloneable
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
        private int webmailFoundedCount;
        private int webmailFoundedCountManual;
        private int cpanelGoodCount;
        private int cpanelGoodCountManual;
        private int cpanelBadCount;
        private int cpanelBadCountManual;
        private int whmGoodCount;
        private int whmGoodCountManual;
        private int whmBadCount;
        private int whmBadCountManual;
        private string dublicateFilePath;
        private string webmailFilePath;
        private string cpanelGoodFilePath;
        private string cpanelBadFilePath;
        private string whmGoodFilePath;
        private string whmBadFilePath;
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

        [Column("webmail_founded_count")]
        public int WebmailFoundedCount { get => webmailFoundedCount; set => SetProperty(ref webmailFoundedCount, value); }

        [Column("webmail_founded_count_manual")]
        public int WebmailFoundedCountManual { get => webmailFoundedCountManual; set => SetProperty(ref webmailFoundedCountManual, value); }

        [Column("cpanel_good_count")]
        public int CpanelGoodCount { get => cpanelGoodCount; set => SetProperty(ref cpanelGoodCount, value); }

        [Column("cpanel_good_count_manual")]
        public int CpanelGoodCountManual { get => cpanelGoodCountManual; set => SetProperty(ref cpanelGoodCountManual, value); }

        [Column("cpanel_bad_count")]
        public int CpanelBadCount { get => cpanelBadCount; set => SetProperty(ref cpanelBadCount, value); }

        [Column("cpanel_bad_count_manual")]
        public int CpanelBadCountManual { get => cpanelBadCountManual; set => SetProperty(ref cpanelBadCountManual, value); }

        [Column("whm_good_count")]
        public int WhmGoodCount { get => whmGoodCount; set => SetProperty(ref whmGoodCount, value); }

        [Column("whm_good_count_manual")]
        public int WhmGoodCountManual { get => whmGoodCountManual; set => SetProperty(ref whmGoodCountManual, value); }

        [Column("whm_bad_count")]
        public int WhmBadCount { get => whmBadCount; set => SetProperty(ref whmBadCount, value); }

        [Column("whm_bad_count_manual")]
        public int WhmBadCountManual { get => whmBadCountManual; set => SetProperty(ref whmBadCountManual, value); }

        [Column("dublicate_file_path")]
        public string DublicateFilePath { get => dublicateFilePath; set => SetProperty(ref dublicateFilePath, value); }

        [Column("webmail_file_path")]
        public string WebmailFilePath { get => webmailFilePath; set => SetProperty(ref webmailFilePath, value); }

        [Column("cpanel_good_file_path")]
        public string CpanelGoodFilePath { get => cpanelGoodFilePath; set => SetProperty(ref cpanelGoodFilePath, value); }

        [Column("cpanel_bad_file_path")]
        public string CpanelBadFilePath { get => cpanelBadFilePath; set => SetProperty(ref cpanelBadFilePath, value); }

        [Column("whm_good_file_path")]
        public string WhmGoodFilePath { get => whmGoodFilePath; set => SetProperty(ref whmGoodFilePath, value); }

        [Column("whm_bad_file_path")]
        public string WhmBadFilePath { get => whmBadFilePath; set => SetProperty(ref whmBadFilePath, value); }

        [Column("original_file_path")]
        public string OriginalFilePath { get => originalFilePath; set => SetProperty(ref originalFilePath, value); }

        [Column("is_manual_check_end")]
        public bool IsManualCheckEnd { get => isManualCheckEnd; set => SetProperty(ref isManualCheckEnd, value); }

        [Column("is_dublicates_filled_to_db")]
        public bool IsDublicatesFilledToDb { get => isDublicatesFilledToDb; set => SetProperty(ref isDublicatesFilledToDb, value); }

        [NotMapped]
        public DelegateCommand<string> OnCopyCommand { get; set; }

        [NotMapped]
        public DelegateCommand<CpanelWhmCheckModel> ResendToSoftManualCommand { get; set; }

        [NotMapped]
        public DelegateCommand<CpanelWhmCheckModel> OpenManualCheckCommand { get; set; }

        [NotMapped]
        public DelegateCommand<CpanelWhmCheckModel> OpenOriginalFileCommand { get; set; }

        [NotMapped]
        public DelegateCommand<CpanelWhmCheckModel> DeleteCheckCommand { get; set; }

        public CpanelWhmCheckModel()
        {
            DublicateFilePath = "";
            WebmailFilePath = "";
            CpanelGoodFilePath = "";
            CpanelBadFilePath = "";
            WhmGoodFilePath = "";
            WhmBadFilePath = "";
            OriginalFilePath = "";
        }

        public object Clone() => MemberwiseClone();
    }
}