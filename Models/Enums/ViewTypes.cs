namespace Models.Enums
{
    public class ViewsPayload
    {
        public enum ViewTypes
        {
            None, Users, Logs, Valid, ManualChecks, Payouts
        }

        public static ViewTypes GetByName(string name)
        {
            if (Enum.TryParse<ViewTypes>(name, out ViewTypes result))
            {
                return result;
            }
            return ViewTypes.None;
        }
    }
}