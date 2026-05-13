using System.Text.RegularExpressions;

namespace GDMENUCardManager.Core
{
    public static class RegularExpressions
    {
        public static readonly Regex GdiRegexp = new Regex(@"\d+ \d+ \d+ \d+ (track\d+.\w+) \d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static readonly Regex TosecnNameRegexp = new Regex(@"\s*[\(\[].*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
