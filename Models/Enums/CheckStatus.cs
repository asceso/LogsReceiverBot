namespace Models.Enums
{
    public class CheckStatus
    {
        public enum ManualCheckStatus
        {
            Created, Error, FillingDb, SendedToSoftCheck, OnlyWebmail, NoAnyUnique, CopyingFiles, CheckedBySoft, SendToManualChecking, End, EndNoValid, WainInQueue
        }

        public enum CookieCheckStatus
        {
            Created, Uploaded, Proceed, End, EndNoValid
        }

        public static string GetEnumValue(ManualCheckStatus status)
        {
            return status switch
            {
                ManualCheckStatus.Created => "cоздана",
                ManualCheckStatus.Error => "ошибка",
                ManualCheckStatus.FillingDb => "заполняется база",
                ManualCheckStatus.SendedToSoftCheck => "отправлено в чекер",
                ManualCheckStatus.OnlyWebmail => "только webmail",
                ManualCheckStatus.NoAnyUnique => "нет уникальных строк",
                ManualCheckStatus.CopyingFiles => "копируются файлы",
                ManualCheckStatus.CheckedBySoft => "проверена софтом",
                ManualCheckStatus.SendToManualChecking => "обрабатывается",
                ManualCheckStatus.End => "завершена",
                ManualCheckStatus.EndNoValid => "завершена, нет валида",
                ManualCheckStatus.WainInQueue => "в очереди для проверки",
                _ => "неизвестно",
            };
        }

        public static string GetEnumValue(CookieCheckStatus status)
        {
            return status switch
            {
                CookieCheckStatus.Created => "созданы",
                CookieCheckStatus.Uploaded => "загружены",
                CookieCheckStatus.Proceed => "обрабатываются",
                CookieCheckStatus.End => "проверены",
                CookieCheckStatus.EndNoValid => "проверены, нет валида",
                _ => "неизвестно",
            };
        }
    }
}