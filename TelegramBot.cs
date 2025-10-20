using System;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace RdpMonitor
{
    public class TelegramBot
    {
        private readonly string _botToken;
        private readonly string _chatId;
        private readonly TelegramBotClient _botClient;

        public TelegramBot(string botToken, string chatId)
        {
            _botToken = botToken;
            _chatId = chatId;
            _botClient = new TelegramBotClient(_botToken);
        }

        public void SendMessage(string message)
        {
            try
            {
                _botClient.SendTextMessageAsync(_chatId, message).Wait();
                RdpMonitor.LogMessage("Сообщение отправлено в Telegram");
            }
            catch (Exception ex)
            {
                RdpMonitor.LogMessage($"Ошибка отправки сообщения в Telegram: {ex.Message}");
            }
        }
    }
}