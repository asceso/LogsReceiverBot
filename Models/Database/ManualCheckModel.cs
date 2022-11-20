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
        private int uniqueFoundedCount;
        private int dublicateFoundedCount;
        private int cpFoundedCount;
        private int whmFoundedCount;
        private string uniqueFilePath;
        private string dublicateFilePath;
        private string cpanelFilePath;
        private string whmFilePath;

        public int Id { get => id; set => SetProperty(ref id, value); }
        public ManualCheckStatus Status { get => status; set => SetProperty(ref status, value); }
        public TimeSpan CheckingTimeEllapsed { get => checkingTimeEllapsed; set => SetProperty(ref checkingTimeEllapsed, value); }
        public DateTime StartDateTime { get => startDateTime; set => SetProperty(ref startDateTime, value); }
        public DateTime EndDateTime { get => endDateTime; set => SetProperty(ref endDateTime, value); }
        public long FromUserId { get => fromUserId; set => SetProperty(ref fromUserId, value); }
        public string FromUsername { get => fromUsername; set => SetProperty(ref fromUsername, value); }
        public int UniqueFoundedCount { get => uniqueFoundedCount; set => SetProperty(ref uniqueFoundedCount, value); }
        public int DublicateFoundedCount { get => dublicateFoundedCount; set => SetProperty(ref dublicateFoundedCount, value); }
        public int CpFoundedCount { get => cpFoundedCount; set => SetProperty(ref cpFoundedCount, value); }
        public int WhmFoundedCount { get => whmFoundedCount; set => SetProperty(ref whmFoundedCount, value); }
        public string UniqueFilePath { get => uniqueFilePath; set => SetProperty(ref uniqueFilePath, value); }
        public string DublicateFilePath { get => dublicateFilePath; set => SetProperty(ref dublicateFilePath, value); }
        public string CpanelFilePath { get => cpanelFilePath; set => SetProperty(ref cpanelFilePath, value); }
        public string WhmFilePath { get => whmFilePath; set => SetProperty(ref whmFilePath, value); }

        [NotMapped]
        public DelegateCommand<string> OnCopyCommand { get; set; }

        [NotMapped]
        public DelegateCommand<ManualCheckModel> OpenManualCheckCommand { get; set; }
    }
}