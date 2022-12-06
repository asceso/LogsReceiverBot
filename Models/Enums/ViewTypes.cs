namespace Models.Enums
{
    public class ViewsPayload
    {
        public enum ViewTypes
        {
            None, Users, Logs, Valid, CpanelWhmChecks, Cookies, Payouts
        }

        public static ViewTypes GetByName(string name)
        {
            if (Enum.TryParse(name, out ViewTypes result))
            {
                return result;
            }
            return ViewTypes.None;
        }
    }
}