using Models.Enums;
using Prism.Commands;
using Prism.Mvvm;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models.Database
{
    [Table("payouts")]
    public class PayoutModel : BindableBase
    {
        private int id;
        private DateTime startDateTime;
        private long fromUserId;
        private string fromUsername;
        private string method;
        private int ammount;
        private string requisites;
        private PayoutStatus.PayoutStatusEnum status;

        [Column("id")]
        public int Id { get => id; set => SetProperty(ref id, value); }

        [Column("start_at")]
        public DateTime StartDateTime { get => startDateTime; set => SetProperty(ref startDateTime, value); }

        [Column("from_user_id")]
        public long FromUserId { get => fromUserId; set => SetProperty(ref fromUserId, value); }

        [Column("from_username")]
        public string FromUsername { get => fromUsername; set => SetProperty(ref fromUsername, value); }

        [Column("method")]
        public string Method { get => method; set => SetProperty(ref method, value); }

        [Column("amount")]
        public int Ammount { get => ammount; set => SetProperty(ref ammount, value); }

        [Column("requisites")]
        public string Requisites { get => requisites; set => SetProperty(ref requisites, value); }

        [Column("status")]
        public PayoutStatus.PayoutStatusEnum Status { get => status; set => SetProperty(ref status, value); }

        [NotMapped]
        public DelegateCommand<string> OnCopyCommand { get; set; }

        [NotMapped]
        public DelegateCommand<PayoutModel> MarkClosed { get; set; }

        [NotMapped]
        public DelegateCommand<PayoutModel> MarkDenied { get; set; }
    }
}