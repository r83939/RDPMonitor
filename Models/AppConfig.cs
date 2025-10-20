namespace RdpMonitor.Models;

public class AppConfig
{
    public TelegramConfig Telegram { get; set; } = new();
    public MonitoringConfig Monitoring { get; set; } = new();
}

public class TelegramConfig
{
    public string BotToken { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
}

public class MonitoringConfig
{
    public string LogName { get; set; } = "Security";
    public int TargetEventId { get; set; } = 4624;
    public int CheckIntervalSeconds { get; set; } = 2;
}