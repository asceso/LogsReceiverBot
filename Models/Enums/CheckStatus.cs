namespace Models.Enums
{
    public class CheckStatus
    {
        public enum ManualCheckStatus
        {
            Created, Error, FillingDb, SendedToSoftCheck, OnlyWebmail, NoAnyUnique, CopyingFiles, CheckedBySoft, SendToManualChecking, End, EndNoValid
        }

        public static string GetEnumValue(ManualCheckStatus checkStatus)
        {
            return checkStatus switch
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
                _ => "неизвестно",
            };
        }
    }
}