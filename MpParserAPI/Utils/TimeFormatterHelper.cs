
namespace MpParserAPI.Utils
{
    public static class TimeFormatterHelper
    {
        public static string ToHumanReadableStringThisSeconds(this TimeSpan timeSpan)
        {
            var parts = new List<string>();

            if (timeSpan.Days > 0)
                parts.Add($"{timeSpan.Days}д");

            if (timeSpan.Hours > 0)
                parts.Add($"{timeSpan.Hours}ч");

            if (timeSpan.Minutes > 0)
                parts.Add($"{timeSpan.Minutes}м");

            if (timeSpan.Seconds > 0)
                parts.Add($"{timeSpan.Seconds}с");
            
            return string.Join(" ", parts);
        }
        public static string ToHumanReadableString(this TimeSpan timeSpan)
        {
            var parts = new List<string>();

            if (timeSpan.Days > 0)
                parts.Add($"{timeSpan.Days}д");

            if (timeSpan.Hours > 0)
                parts.Add($"{timeSpan.Hours}ч");

            if (timeSpan.Minutes > 0)
                parts.Add($"{timeSpan.Minutes}м");


            return string.Join(" ", parts);
        }
    }
}
