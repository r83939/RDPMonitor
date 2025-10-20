using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;

namespace RdpMonitor.Services;

public class TelegramService : ITelegramService
{
    private readonly string _botToken;
    private readonly string _chatId;
    private readonly TelegramBotClient _botClient;

    public TelegramService(string botToken, string chatId)
    {
        _botToken = botToken ?? throw new ArgumentNullException(nameof(botToken));
        _chatId = chatId ?? throw new ArgumentNullException(nameof(chatId));
        _botClient = new TelegramBotClient(_botToken);
    }

    public async Task<bool> SendMessageAsync(string message)
    {
        try
        {
            // Проверяем, является ли chatId числовым ID или @username
            if (long.TryParse(_chatId, out long chatIdLong))
            {
                await _botClient.SendTextMessageAsync(
                    chatId: chatIdLong,
                    text: message,
                    parseMode: ParseMode.Markdown,
                    disableNotification: false
                );
            }
            else if (_chatId.StartsWith("@"))
            {
                await _botClient.SendTextMessageAsync(
                    chatId: _chatId,
                    text: message,
                    parseMode: ParseMode.Markdown,
                    disableNotification: false
                );
            }
            else
            {
                throw new ArgumentException($"Некорректный формат Chat ID: {_chatId}");
            }
            
            return true;
        }
        catch (ApiRequestException ex) when (ex.ErrorCode == 400)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ Ошибка: Некорректный Chat ID или бот не добавлен в чат");
            return false;
        }
        catch (ApiRequestException ex) when (ex.ErrorCode == 404)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ Ошибка: Неверный токен бота");
            return false;
        }
        catch (ApiRequestException ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ Ошибка Telegram API ({ex.ErrorCode}): {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ Ошибка отправки в Telegram: {ex.Message}");
            return false;
        }
    }
}