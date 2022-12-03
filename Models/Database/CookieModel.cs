using Prism.Mvvm;

namespace Models.Database
{
    public class CookieModel : BindableBase
    {
        private int id;
        private string fileLink;
        private string folderPath;
        private string category;
        private string filesize;
        private string unit;
        private long uploadedByUserId;
        private string uploadedByUsername;

        public int Id { get => id; set => SetProperty(ref id, value); }
        public string FileLink { get => fileLink; set => SetProperty(ref fileLink, value); }
        public string FolderPath { get => folderPath; set => SetProperty(ref folderPath, value); }
        public string Category { get => category; set => SetProperty(ref category, value); }
        public string Filesize { get => filesize; set => SetProperty(ref filesize, value); }
        public string Unit { get => unit; set => SetProperty(ref unit, value); }
        public long UploadedByUserId { get => uploadedByUserId; set => SetProperty(ref uploadedByUserId, value); }
        public string UploadedByUsername { get => uploadedByUsername; set => SetProperty(ref uploadedByUsername, value); }
    }
}