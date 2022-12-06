using Prism.Commands;
using Prism.Mvvm;
using System.ComponentModel.DataAnnotations.Schema;
using static Models.Enums.CheckStatus;

namespace Models.Database
{
    [Table("cookies")]
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

        [Column("id")]
        public int Id { get => id; set => SetProperty(ref id, value); }

        [Column("status")]
        public CookieCheckStatus Status { get => status; set => SetProperty(ref status, value); }

        [Column("uploaded_date_time")]
        public DateTime UploadedDateTime { get => uploadedDateTime; set => SetProperty(ref uploadedDateTime, value); }

        [Column("file_link")]
        public string FileLink { get => fileLink; set => SetProperty(ref fileLink, value); }

        [Column("folder_path")]
        public string FolderPath { get => folderPath; set => SetProperty(ref folderPath, value); }

        [Column("category")]
        public string Category { get => category; set => SetProperty(ref category, value); }

        [Column("filesize")]
        public string Filesize { get => filesize; set => SetProperty(ref filesize, value); }

        [Column("unit")]
        public string Unit { get => unit; set => SetProperty(ref unit, value); }

        [Column("uploaded_by_user_id")]
        public long UploadedByUserId { get => uploadedByUserId; set => SetProperty(ref uploadedByUserId, value); }

        [Column("uploaded_by_username")]
        public string UploadedByUsername { get => uploadedByUsername; set => SetProperty(ref uploadedByUsername, value); }

        [Column("valid_found")]
        public double ValidFound { get => validFound; set => SetProperty(ref validFound, value); }

        [Column("balance_to_user")]
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