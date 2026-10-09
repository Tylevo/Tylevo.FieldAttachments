using System;
using System.Globalization;
using System.Text;

namespace Tylevo.FieldAttachments.Core
{
    // Small explicit JSON encoder; never reflects over the player's profile.
    public static class JsonText
    {
        public static string Quote(string? value)
        {
            if (value == null) return "null";
            var b = new StringBuilder("\"");
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default:
                        if (c < 32 || char.IsSurrogate(c)) b.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else b.Append(c);
                        break;
                }
            }
            return b.Append('"').ToString();
        }
        public static string Bool(bool? value) { return value.HasValue ? (value.Value ? "true" : "false") : "null"; }
    }
}
