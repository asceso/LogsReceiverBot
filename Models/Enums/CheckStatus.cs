namespace Models.Enums
{
    public class CheckStatus
    {
        public enum ManualCheckStatus
        {
            Created, Error, FillingDb, DublicateDeleted, OnlyWebmail, NoAnyUnique, CopyingFiles, CheckedBySoft, SendToManualChecking, End, EndNoValid
        }

        public static string GetEnumValue(ManualCheckStatus checkStatus)
        {
            return checkStatus switch
            {
                ManualCheckStatus.Created => "cоздана",
                ManualCheckStatus.Error => "ошибка",
                ManualCheckStatus.FillingDb => "заполняется база",
                ManualCheckStatus.DublicateDeleted => "удалены дубликаты",
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