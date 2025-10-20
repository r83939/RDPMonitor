using System;
using System.IO;
using System.Text.Json;

namespace RdpMonitor
{
    public class Config
    {
        public string BotToken { get; set; } = string.Empty;
        public string ChatId { get; set; } = string.Empty;

        public static Config Load()
        {
            try
            {
                string configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                
                if (!File.Exists(configFile))
                {
                    // Создаем конфиг по умолчанию
                    var defaultConfig = new Config
                    {
                        BotToken = "YOUR_BOT_TOKEN",
                        ChatId = "YOUR_CHAT_ID"
                    };
                    
                    string json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(configFile, json);
                    
                    RdpMonitor.LogMessage("Создан файл config.json с настройками по умолчанию");
                    return defaultConfig;
                }

                string jsonContent = File.ReadAllText(configFile);
                return JsonSerializer.Deserialize<Config>(jsonContent) ?? new Config();
            }
            catch (Exception ex)
            {
                RdpMonitor.LogMessage($"Ошибка загрузки конфигурации: {ex.Message}");
                throw;
            }
        }
    }
}