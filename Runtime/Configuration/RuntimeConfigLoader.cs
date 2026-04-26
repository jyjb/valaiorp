namespace Valaiorp.Runtime.Configuration
{
    using System.Text.Json;
    using Valaiorp.Configuration.Config;

    public static class RuntimeConfigLoader
    {
        public static AgenticAIConfig LoadFromFile(string filePath)
        {
            var json = System.IO.File.ReadAllText(filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };
            return JsonSerializer.Deserialize<AgenticAIConfig>(json, options)
                ?? throw new InvalidOperationException("Failed to deserialize configuration.");
        }

        public static AgenticAIConfig LoadDefault()
        {
            return new AgenticAIConfig();
        }
    }
}