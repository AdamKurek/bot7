using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace bot7
{
    internal class VoiceRecognition
    {
        public static string ExtractText(string jsonResult)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonResult);
                return doc.RootElement.TryGetProperty("text", out var textElement)
                    ? textElement.GetString() ?? ""
                    : "";
            }
            catch
            {
                return "";
            }
        }

        public static string ExtractPartialText(string jsonPartial)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonPartial);
                return doc.RootElement.TryGetProperty("partial", out var partialElement)
                    ? partialElement.GetString() ?? ""
                    : "";
            }
            catch
            {
                return "";
            }
        }
    }
}
