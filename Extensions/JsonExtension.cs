using Newtonsoft.Json.Linq;

namespace Extensions
{
    public static class JsonExtension
    {
        public static bool TryParseToJObject(this string source, out JObject jobject)
        {
            try
            {
                jobject = JObject.Parse(source);
                return true;
            }
            catch (Exception)
            {
                jobject = null;
                return false;
            }
        }
    }
}