using Models.Enums;
using Prism.Commands;
using Prism.Mvvm;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models.Database
{
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

        public int Id { get => id; set => SetProperty(ref id, value); }
        public DateTime StartDateTime { get => startDateTime; set => SetProperty(ref startDateTime, value); }
        public long FromUserId { get => fromUserId; set => SetProperty(ref fromUserId, value); }
        public string FromUsername { get => fromUsername; set => SetProperty(ref fromUsername, value); }
        public string Method { get => method; set => SetProperty(ref method, value); }
        public int Ammount { get => ammount; set => SetProperty(ref ammount, value); }
        public string Requisites { get => requisites; set => SetProperty(ref requisites, value); }
        public PayoutStatus.PayoutStatusEnum Status { get => status; set => SetProperty(ref status, value); }

        [NotMapped]
        public DelegateCommand<string> OnCopyCommand { get; set; }

        [NotMapped]
        public DelegateCommand<PayoutModel> MarkClosed { get; set; }

        [NotMapped]
        public DelegateCommand<PayoutModel> MarkDenied { get; set; }
    }
}