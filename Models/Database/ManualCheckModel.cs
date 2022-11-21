using Prism.Commands;
using Prism.Mvvm;
using System.ComponentModel.DataAnnotations.Schema;
using static Models.Enums.CheckStatus;

namespace Models.Database
{
    public class ManualCheckModel : BindableBase
    {
        private int id;
        private ManualCheckStatus status;
        private TimeSpan checkingTimeEllapsed;
        private DateTime startDateTime;
        private DateTime endDateTime;
        private long fromUserId;
        private string fromUsername;
        private int dublicateFoundedCount;
        private int webmailFoundedCount;
        private int cpanelGoodCount;
        private int cpanelBadCount;
        private int whmGoodCount;
        private int whmBadCount;
        private string dublicateFilePath;
        private string webmailFilePath;
        private string cpanelGoodFilePath;
        private string cpanelBadFilePath;
        private string whmGoodFilePath;
        private string whmBadFilePath;

        public int Id { get => id; set => SetProperty(ref id, value); }
        public ManualCheckStatus Status { get => status; set => SetProperty(ref status, value); }
        public TimeSpan CheckingTimeEllapsed { get => checkingTimeEllapsed; set => SetProperty(ref checkingTimeEllapsed, value); }
        public DateTime StartDateTime { get => startDateTime; set => SetProperty(ref startDateTime, value); }
        public DateTime EndDateTime { get => endDateTime; set => SetProperty(ref endDateTime, value); }
        public long FromUserId { get => fromUserId; set => SetProperty(ref fromUserId, value); }
        public string FromUsername { get => fromUsername; set => SetProperty(ref fromUsername, value); }
        public int DublicateFoundedCount { get => dublicateFoundedCount; set => SetProperty(ref dublicateFoundedCount, value); }
        public int WebmailFoundedCount { get => webmailFoundedCount; set => SetProperty(ref webmailFoundedCount, value); }
        public int CpanelGoodCount { get => cpanelGoodCount; set => SetProperty(ref cpanelGoodCount, value); }
        public int CpanelBadCount { get => cpanelBadCount; set => SetProperty(ref cpanelBadCount, value); }
        public int WhmGoodCount { get => whmGoodCount; set => SetProperty(ref whmGoodCount, value); }
        public int WhmBadCount { get => whmBadCount; set => SetProperty(ref whmBadCount, value); }
        public string DublicateFilePath { get => dublicateFilePath; set => SetProperty(ref dublicateFilePath, value); }
        public string WebmailFilePath { get => webmailFilePath; set => SetProperty(ref webmailFilePath, value); }
        public string CpanelGoodFilePath { get => cpanelGoodFilePath; set => SetProperty(ref cpanelGoodFilePath, value); }
        public string CpanelBadFilePath { get => cpanelBadFilePath; set => SetProperty(ref cpanelBadFilePath, value); }
        public string WhmGoodFilePath { get => whmGoodFilePath; set => SetProperty(ref whmGoodFilePath, value); }
        public string WhmBadFilePath { get => whmBadFilePath; set => SetProperty(ref whmBadFilePath, value); }

        [NotMapped]
        public DelegateCommand<string> OnCopyCommand { get; set; }

        [NotMapped]
        public DelegateCommand<ManualCheckModel> OpenManualCheckCommand { get; set; }
    }
}