using Services.Interfaces;
using System.Drawing;

namespace Services.Implementation
{
    public class CaptchaService : ICaptchaService
    {
        private string GetDateTimeString() => DateTime.Now.ToString("ddMMyyyy") + DateTime.Now.ToString("HHmmss");

        private int[] offsets = new int[] { 0, 5, 10 };
        private int captchaLength = 5;
        private Brush[] brushes = new Brush[] { Brushes.Red, Brushes.Pink, Brushes.Yellow, Brushes.Cyan, Brushes.Aqua };
        private string chars = "0123456789";

        public Tuple<string, string> CreateCaptchaForUser()
        {
            int width = 200;
            int height = 30;

            string word = string.Empty;
            Random rnd = new();
            for (int i = 0; i < captchaLength; i++)
            {
                word += chars[rnd.Next(chars.Length)];
            }

            Bitmap bitmap = new(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bitmap);
            g.DrawRectangle(new Pen(Brushes.Black, 200), new Rectangle(0, 0, width, height));
            for (int i = 0; i < captchaLength; i++)
            {
                g.DrawString(word[i].ToString(),
                    new Font("Courier New", 14, FontStyle.Bold),
                    brushes[rnd.Next(brushes.Length)],
                    new RectangleF(width / captchaLength * i, offsets[rnd.Next(offsets.Length)], width / captchaLength, height)
                    );
            }
            g.Dispose();

            string filename = Environment.CurrentDirectory + "/temp/" + GetDateTimeString() + ".jpg";
            bitmap.Save(filename);
            return Tuple.Create(word, filename);
        }
    }
}