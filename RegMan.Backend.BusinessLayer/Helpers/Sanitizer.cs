using System.Text.RegularExpressions;

namespace RegMan.Backend.BusinessLayer.Helpers
{
    public static class Sanitizer
    {
        // Basic HTML tag removal and encoding
        public static string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            // Remove script/style tags and encode angle brackets
            string sanitized = Regex.Replace(input, "<.*?>", string.Empty);
            sanitized = sanitized.Replace("<", "&lt;").Replace(">", "&gt;");
            sanitized = sanitized.Replace("\"", "&quot;").Replace("'", "&#39;");
            return sanitized;
        }
    }
}
