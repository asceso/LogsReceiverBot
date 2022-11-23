namespace Models.Enums
{
    public class CheckStatus
    {
        public enum ManualCheckStatus
        {
            Created, Error, FillingDb, DublicateDeleted, NoAnyUnique, CopyingFiles, CheckedBySoft, SendToManualChecking, End
        }

        public static string GetEnumValue(ManualCheckStatus checkStatus)
        {
            return checkStatus switch
            {
                ManualCheckStatus.Created => "cоздана",
                ManualCheckStatus.Error => "ошибка",
                ManualCheckStatus.FillingDb => "заполняется база",
                ManualCheckStatus.DublicateDeleted => "удалены дубликаты",
                ManualCheckStatus.NoAnyUnique => "нет уникальных строк",
                ManualCheckStatus.CopyingFiles => "копируются файлы",
                ManualCheckStatus.CheckedBySoft => "проверена софтом",
                ManualCheckStatus.SendToManualChecking => "обрабатывается",
                ManualCheckStatus.End => "завершена",
                _ => "неизвестно",
            };
        }
    }
}