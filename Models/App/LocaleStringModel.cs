using Extensions;
using Telegram.Bot.Types.ReplyMarkups;

namespace Models.App
{
    public class LocaleStringModel
    {
        public string Key { get; set; }
        public string EnString { get; set; }
        public string RuString { get; set; }

        public string ToString(string locale = "en")
        {
            if (locale == "ru")
            {
                return RuString;
            }
            else
            {
                return EnString;
            }
        }
    }

    public static class LocalesExtension
    {
        public static string GetByKey(this List<LocaleStringModel> list, string key, string locale)
        {
            var target = list.FirstOrDefault(k => k.Key == key);
            if (target != null)
            {
                return target.ToString(locale);
            }
            else
            {
                return string.Empty;
            }
        }

        public static ReplyKeyboardMarkup GetByLocale(this Dictionary<string, ReplyKeyboardMarkup> dictionary, string key, string locale, bool payoutEnabled)
        {
            string targetKey = key + locale.ToPascalCase();
            if (key == "Main" && payoutEnabled)
            {
                targetKey = "MainWithPayment" + locale.ToPascalCase();
            }
            if (dictionary.ContainsKey(targetKey))
            {
                return dictionary[targetKey];
            }
            return null;
        }
    }
}