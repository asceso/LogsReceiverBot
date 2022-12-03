using Prism.Commands;
using Prism.Mvvm;
using System.ComponentModel.DataAnnotations.Schema;
using static Models.Enums.CheckStatus;

namespace Models.Database
{
    public class CookieModel : BindableBase, ICloneable
    {
        private int id;
        private CookieCheckStatus status;
        private DateTime uploadedDateTime;
        private string fileLink;
        private string folderPath;
        private string category;
        private string filesize;
        private string unit;
        private long uploadedByUserId;
        private string uploadedByUsername;
        private double validFound;
        private double balanceToUser;
        private string sizeWithUnit;
        private string endStatusTooltip;

        public int Id { get => id; set => SetProperty(ref id, value); }
        public CookieCheckStatus Status { get => status; set => SetProperty(ref status, value); }
        public DateTime UploadedDateTime { get => uploadedDateTime; set => SetProperty(ref uploadedDateTime, value); }
        public string FileLink { get => fileLink; set => SetProperty(ref fileLink, value); }
        public string FolderPath { get => folderPath; set => SetProperty(ref folderPath, value); }
        public string Category { get => category; set => SetProperty(ref category, value); }
        public string Filesize { get => filesize; set => SetProperty(ref filesize, value); }
        public string Unit { get => unit; set => SetProperty(ref unit, value); }
        public long UploadedByUserId { get => uploadedByUserId; set => SetProperty(ref uploadedByUserId, value); }
        public string UploadedByUsername { get => uploadedByUsername; set => SetProperty(ref uploadedByUsername, value); }
        public double ValidFound { get => validFound; set => SetProperty(ref validFound, value); }
        public double BalanceToUser { get => balanceToUser; set => SetProperty(ref balanceToUser, value); }

        [NotMapped]
        public string SizeWithUnit
        {
            get
            {
                sizeWithUnit = $"{Filesize} ({Unit.ToLower()})";
                return sizeWithUnit;
            }

            set => SetProperty(ref sizeWithUnit, value);
        }

        [NotMapped]
        public string EndStatusTooltip
        {
            get
            {
                if (Status == CookieCheckStatus.End)
                {
                    endStatusTooltip = $"Найдено валида: {ValidFound} пользователю начислено: {BalanceToUser}";
                }
                if (Status == CookieCheckStatus.EndNoValid)
                {
                    endStatusTooltip = $"Не было найдено валида";
                }
                return endStatusTooltip;
            }

            set => SetProperty(ref endStatusTooltip, value);
        }

        [NotMapped]
        public DelegateCommand<string> OnCopyCommand { get; set; }

        [NotMapped]
        public DelegateCommand<CookieModel> OpenCheckCommand { get; set; }

        [NotMapped]
        public DelegateCommand<CookieModel> OpenFolderCommand { get; set; }

        [NotMapped]
        public DelegateCommand<CookieModel> OpenUrlCommand { get; set; }

        [NotMapped]
        public DelegateCommand<CookieModel> DeleteCommand { get; set; }

        public object Clone() => MemberwiseClone();
    }
}