namespace Extensions
{
    public static class DateTimeExtension
    {
        public static string GetFilenameTimestamp(this DateTime dateTime) => dateTime.ToString("dd_MM_yyyyy_HH_mm_ss");
    }
}