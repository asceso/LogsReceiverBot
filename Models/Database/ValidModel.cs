using Prism.Commands;
using Prism.Mvvm;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models.Database
{
    public class ValidModel : BindableBase
    {
        private int id;
        private string data;
        private string category;
        private long uploadedByUserId;
        private string uploadedByUsername;

        public int Id { get => id; set => SetProperty(ref id, value); }
        public string Data { get => data; set => SetProperty(ref data, value); }
        public string Category { get => category; set => SetProperty(ref category, value); }
        public long UploadedByUserId { get => uploadedByUserId; set => SetProperty(ref uploadedByUserId, value); }
        public string UploadedByUsername { get => uploadedByUsername; set => SetProperty(ref uploadedByUsername, value); }

        [NotMapped]
        public DelegateCommand<string> OnCopyCommand { get; set; }
    }
}