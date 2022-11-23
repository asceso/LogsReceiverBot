namespace Services.Interfaces
{
    public interface ICaptchaService
    {
        public Tuple<string, string> CreateCaptchaForUser();
    }
}