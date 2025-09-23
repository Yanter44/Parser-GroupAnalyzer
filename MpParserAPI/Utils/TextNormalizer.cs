using System.Text;

namespace MpParserAPI.Utils
{
    public static class TextNormalizer
    {
        public static string NormalizeText(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return input.Normalize(NormalizationForm.FormC).Trim();
        }
    }

}
