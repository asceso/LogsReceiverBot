namespace Extensions
{
    public static class StringExtension
    {
        public static bool IsNullOrEmpty(this string source) => string.IsNullOrEmpty(source);

        public static string ToPascalCase(this string source)
        {
            string result = string.Empty;
            for (int i = 0; i < source.Length; i++)
            {
                result += i == 0 ? source[i].ToString().ToUpper() : source[i].ToString().ToLower();
            }
            return result;
        }

        public static bool IsAnyEqual(this string source, params string[] keys)
        {
            if (keys is not null)
            {
                if (keys.Any(k => k == source))
                {
                    return true;
                }
            }
            return false;
        }
    }
}