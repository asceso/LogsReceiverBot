using Prism.Commands;
using Prism.Mvvm;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models.Database
{
    [Table("valid")]
    public class ValidModel : BindableBase
    {
        private int id;
        private string data;
        private string category;
        private long uploadedByUserId;
        private string uploadedByUsername;

        [Column("id")]
        public int Id { get => id; set => SetProperty(ref id, value); }

        [Column("data")]
        public string Data { get => data; set => SetProperty(ref data, value); }

        [Column("category")]
        public string Category { get => category; set => SetProperty(ref category, value); }

        [Column("uploaded_by_user_id")]
        public long UploadedByUserId { get => uploadedByUserId; set => SetProperty(ref uploadedByUserId, value); }

        [Column("uploaded_by_username")]
        public string UploadedByUsername { get => uploadedByUsername; set => SetProperty(ref uploadedByUsername, value); }

        [NotMapped]
        public DelegateCommand<string> OnCopyCommand { get; set; }
    }
}