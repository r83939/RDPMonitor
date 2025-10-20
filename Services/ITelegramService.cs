namespace RdpMonitor.Services;

public interface ITelegramService
{
    Task<bool> SendMessageAsync(string message);
}