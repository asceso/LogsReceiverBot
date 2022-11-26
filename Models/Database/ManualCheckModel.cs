using Prism.Commands;
using Prism.Mvvm;
using System.ComponentModel.DataAnnotations.Schema;
using static Models.Enums.CheckStatus;

namespace Models.Database
{
    public class ManualCheckModel : BindableBase, ICloneable
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
        private bool isManualCheckEnd;

        public int Id { get => id; set => SetProperty(ref id, value); }
        public ManualCheckStatus Status { get => status; set => SetProperty(ref status, value); }
        public TimeSpan CheckingTimeEllapsed { get => checkingTimeEllapsed; set => SetProperty(ref checkingTimeEllapsed, value); }
        public DateTime StartDateTime { get => startDateTime; set => SetProperty(ref startDateTime, value); }
        public DateTime EndDateTime { get => endDateTime; set => SetProperty(ref endDateTime, value); }
        public long FromUserId { get => fromUserId; set => SetProperty(ref fromUserId, value); }
        public string FromUsername { get => fromUsername; set => SetProperty(ref fromUsername, value); }
        public int DublicateFoundedCount { get => dublicateFoundedCount; set => SetProperty(ref dublicateFoundedCount, value); }
        public int DublicateFoundedCountManual { get => dublicateFoundedCountManual; set => SetProperty(ref dublicateFoundedCountManual, value); }
        public int WebmailFoundedCount { get => webmailFoundedCount; set => SetProperty(ref webmailFoundedCount, value); }
        public int WebmailFoundedCountManual { get => webmailFoundedCountManual; set => SetProperty(ref webmailFoundedCountManual, value); }
        public int CpanelGoodCount { get => cpanelGoodCount; set => SetProperty(ref cpanelGoodCount, value); }
        public int CpanelGoodCountManual { get => cpanelGoodCountManual; set => SetProperty(ref cpanelGoodCountManual, value); }
        public int CpanelBadCount { get => cpanelBadCount; set => SetProperty(ref cpanelBadCount, value); }
        public int CpanelBadCountManual { get => cpanelBadCountManual; set => SetProperty(ref cpanelBadCountManual, value); }
        public int WhmGoodCount { get => whmGoodCount; set => SetProperty(ref whmGoodCount, value); }
        public int WhmGoodCountManual { get => whmGoodCountManual; set => SetProperty(ref whmGoodCountManual, value); }
        public int WhmBadCount { get => whmBadCount; set => SetProperty(ref whmBadCount, value); }
        public int WhmBadCountManual { get => whmBadCountManual; set => SetProperty(ref whmBadCountManual, value); }
        public string DublicateFilePath { get => dublicateFilePath; set => SetProperty(ref dublicateFilePath, value); }
        public string WebmailFilePath { get => webmailFilePath; set => SetProperty(ref webmailFilePath, value); }
        public string CpanelGoodFilePath { get => cpanelGoodFilePath; set => SetProperty(ref cpanelGoodFilePath, value); }
        public string CpanelBadFilePath { get => cpanelBadFilePath; set => SetProperty(ref cpanelBadFilePath, value); }
        public string WhmGoodFilePath { get => whmGoodFilePath; set => SetProperty(ref whmGoodFilePath, value); }
        public string WhmBadFilePath { get => whmBadFilePath; set => SetProperty(ref whmBadFilePath, value); }
        public bool IsManualCheckEnd { get => isManualCheckEnd; set => SetProperty(ref isManualCheckEnd, value); }

        [NotMapped]
        public DelegateCommand<string> OnCopyCommand { get; set; }

        [NotMapped]
        public DelegateCommand<ManualCheckModel> OpenManualCheckCommand { get; set; }

        [NotMapped]
        public DelegateCommand<ManualCheckModel> DeleteCheckCommand { get; set; }

        public ManualCheckModel()
        {
            DublicateFilePath = "";
            WebmailFilePath = "";
            CpanelGoodFilePath = "";
            CpanelBadFilePath = "";
            WhmGoodFilePath = "";
            WhmBadFilePath = "";
        }

        public object Clone() => MemberwiseClone();
    }
}