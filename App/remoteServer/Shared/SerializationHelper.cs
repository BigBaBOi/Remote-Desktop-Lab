using System.Text.Json;

namespace Shared.Utils
{
    public static class SerializationHelper
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        public static byte[] Serialize<T>(T obj)
        {
            return JsonSerializer.SerializeToUtf8Bytes(obj, _options);
        }

        public static T? Deserialize<T>(byte[] data)
        {
            return JsonSerializer.Deserialize<T>(data, _options);
        }
        
        public static string SerializeToString<T>(T obj)
        {
            return JsonSerializer.Serialize(obj, _options);
        }

        public static T? DeserializetimString<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, _options);
        }
    }
}
