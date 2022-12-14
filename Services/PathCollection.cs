namespace Services
{
    public static class PathCollection
    {
        public static string ConfigPath = Environment.CurrentDirectory + "/config/config.json";
        public static string OperationsPath = Environment.CurrentDirectory + "/config/operations.json";
        public static string LocalesPath = Environment.CurrentDirectory + "/config/locales.json";

        public static string TempFolderPath = Environment.CurrentDirectory + "/temp/";
        public static string CpanelAndWhmFolderPath = Environment.CurrentDirectory + "/cpanel_whm/";
        public static string WpLoginFolderPath = Environment.CurrentDirectory + "/wp-login/";
        public static string CookiesFolderPath = Environment.CurrentDirectory + "/cookies/";
        public static string CheckerBinPath = Environment.CurrentDirectory + "/checker_bin";
    }
}