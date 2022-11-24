namespace Models.Enums
{
    public class PayoutStatus
    {
        public enum PayoutStatusEnum
        {
            Created, Denied, Completed
        }

        public static string GetEnumValue(PayoutStatusEnum status)
        {
            return status switch
            {
                PayoutStatusEnum.Created => "cоздана",
                PayoutStatusEnum.Denied => "отклонена",
                PayoutStatusEnum.Completed => "завершена",
                _ => "неизвестно",
            };
        }
    }
}