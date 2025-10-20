using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace RdpMonitor
{
    public class RdpMonitor
    {
        private bool _isMonitoring;
        private EventLogMonitor _eventLogMonitor;
        private TelegramBot _telegramBot;
        private HashSet<string> _processedEvents;
        private Timer _cleanupTimer;

        public void Initialize()
        {
            try
            {
                LogMessage("Инициализация RDP монитора...");
                
                // Загрузка конфигурации
                Config config = Config.Load();
                _telegramBot = new TelegramBot(config.BotToken, config.ChatId);
                
                _processedEvents = new HashSet<string>();
                _eventLogMonitor = new EventLogMonitor("Security");
                
                // Настройка обработчика событий
                _eventLogMonitor.OnEventLogged += (entry) =>
                {
                    if (entry.InstanceId == 4624L) // RDP вход
                    {
                        ProcessRdpLoginEvent(entry);
                    }
                };

                // Таймер для очистки старых событий (каждые 30 минут)
                _cleanupTimer = new Timer(CleanupProcessedEvents, null, 
                    TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));

                LogMessage("RDP монитор инициализирован успешно");
                
                // Отправка тестового сообщения
                _telegramBot.SendMessage("✅ RDP Monitor запущен в режиме службы");
            }
            catch (Exception ex)
            {
                LogMessage($"Ошибка инициализации: {ex.Message}");
                throw;
            }
        }

        public void Start()
        {
            if (_isMonitoring) return;
            
            _isMonitoring = true;
            _eventLogMonitor?.StartMonitoring();
            LogMessage("Мониторинг событий запущен");
        }

        public void Stop()
        {
            if (!_isMonitoring) return;
            
            _isMonitoring = false;
            _eventLogMonitor?.StopMonitoring();
            _cleanupTimer?.Dispose();
            LogMessage("Мониторинг событий остановлен");
        }

        public bool IsRunning()
        {
            return _isMonitoring && _eventLogMonitor != null;
        }

        private void ProcessRdpLoginEvent(EventLogEntry entry)
        {
            try
            {
                string eventSignature = $"{entry.TimeGenerated:yyyyMMddHHmm}_{entry.InstanceId}_{entry.Index}";
                
                if (_processedEvents.Contains(eventSignature))
                    return;

                _processedEvents.Add(eventSignature);

                string message = $"🔐 Новый RDP вход:\n" +
                               $"🕐 Время: {entry.TimeGenerated:dd.MM.yyyy HH:mm:ss}\n" +
                               $"👤 Пользователь: {ExtractUserName(entry)}\n" +
                               $"💻 Компьютер: {ExtractComputerName(entry)}\n" +
                               $"📍 IP адрес: {ExtractIpAddress(entry)}";

                _telegramBot.SendMessage(message);
                LogMessage($"Отправлено уведомление о RDP входе: {ExtractUserName(entry)}");
            }
            catch (Exception ex)
            {
                LogMessage($"Ошибка обработки события RDP: {ex.Message}");
            }
        }

        private void CleanupProcessedEvents(object state)
        {
            try
            {
                // Оставляем только события за последние 24 часа
                var cutoffTime = DateTime.Now.AddHours(-24);
                _processedEvents.RemoveWhere(eventId => 
                    DateTime.TryParse(eventId.Split('_')[0], "yyyyMMddHHmm", null, 
                    System.Globalization.DateTimeStyles.None, out DateTime eventTime) && 
                    eventTime < cutoffTime);
                
                LogMessage($"Очистка обработанных событий завершена. Осталось: {_processedEvents.Count}");
            }
            catch (Exception ex)
            {
                LogMessage($"Ошибка очистки событий: {ex.Message}");
            }
        }

        private string ExtractUserName(EventLogEntry entry) => "N/A";
        private string ExtractComputerName(EventLogEntry entry) => "N/A"; 
        private string ExtractIpAddress(EventLogEntry entry) => "N/A";

        public static void LogMessage(string message)
        {
            try
            {
                string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [MONITOR] {message}";
                File.AppendAllText(logFile, logEntry + Environment.NewLine);
            }
            catch
            {
                // Игнорируем ошибки логирования
            }
        }
    }
}