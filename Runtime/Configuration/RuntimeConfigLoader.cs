namespace Valaiorp.Runtime.Configuration
{
    using System.Text.Json;
    using Valaiorp.Configuration.Config;

    public static class RuntimeConfigLoader
    {
        public static ValaiorpConfig LoadFromFile(string filePath)
        {
            var json = System.IO.File.ReadAllText(filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            return JsonSerializer.Deserialize<ValaiorpConfig>(json, options)
                ?? throw new InvalidOperationException("Failed to deserialize configuration.");
        }

        public static ValaiorpConfig LoadDefault()
        {
            return new ValaiorpConfig();
        }
    }
}