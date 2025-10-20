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
        private EventLogMonitor? _eventLogMonitor;
        private TelegramBot? _telegramBot;
        private HashSet<string>? _processedEvents;
        private Timer? _cleanupTimer;

        public void Initialize()
        {
            try
            {
                LogMessage("Инициализация RDP монитора...");
                
                Config config = Config.Load();
                _telegramBot = new TelegramBot(config.BotToken, config.ChatId);
                
                _processedEvents = new HashSet<string>();
                _eventLogMonitor = new EventLogMonitor("Security");
                
                _eventLogMonitor.OnEventLogged += (entry) =>
                {
                    if (entry.InstanceId == 4624L) // RDP вход
                    {
                        ProcessRdpLoginEvent(entry);
                    }
                };

                _cleanupTimer = new Timer(CleanupProcessedEvents!, null, 
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
                // Улучшенная сигнатура для дедупликации - используем время с точностью до секунды
                string eventSignature = $"{entry.TimeGenerated:yyyyMMddHHmmss}_{entry.InstanceId}";
                
                if (_processedEvents!.Contains(eventSignature))
                    return;

                _processedEvents.Add(eventSignature);

                // Извлекаем данные через EventData
                var eventData = Models.EventData.FromEventLogEntry(entry);
                
                // Логируем детали события для отладки
                LogMessage($"Обработка события: User={eventData.UserName}, Workstation={eventData.Workstation}, IP={eventData.IpAddress}");
                
                string message = $"🔐 Новый RDP вход:\n" +
                               $"🕐 Время: {eventData.Time:dd.MM.yyyy HH:mm:ss}\n" +
                               $"👤 Пользователь: {eventData.UserName}\n" +
                               $"💻 Компьютер: {eventData.Workstation}\n" +
                               $"📍 IP адрес: {eventData.IpAddress}";

                _telegramBot!.SendMessage(message);
                LogMessage($"Отправлено уведомление о RDP входе: {eventData.UserName} с IP: {eventData.IpAddress}");
            }
            catch (Exception ex)
            {
                LogMessage($"Ошибка обработки события RDP: {ex.Message}");
            }
        }

        private void CleanupProcessedEvents(object? state)
        {
            try
            {
                var cutoffTime = DateTime.Now.AddHours(-1); // Уменьшим до 1 часа для тестирования
                
                _processedEvents!.RemoveWhere(eventId => 
                {
                    if (eventId.Split('_').Length > 0)
                    {
                        string datePart = eventId.Split('_')[0];
                        if (DateTime.TryParseExact(datePart, "yyyyMMddHHmmss", 
                            null, System.Globalization.DateTimeStyles.None, out DateTime eventTime))
                        {
                            return eventTime < cutoffTime;
                        }
                    }
                    return false;
                });
                
                LogMessage($"Очистка обработанных событий завершена. Осталось: {_processedEvents.Count}");
            }
            catch (Exception ex)
            {
                LogMessage($"Ошибка очистки событий: {ex.Message}");
            }
        }

        private string ExtractUserName(EventLogEntry entry)
        {
            try
            {
                // Парсим имя пользователя из сообщения события
                string[] parts = entry.Message.Split('\n');
                foreach (string part in parts)
                {
                    if (part.Contains("Имя пользователя:") || part.Contains("User Name:"))
                        return part.Split(':')[1].Trim();
                }
                return "N/A";
            }
            catch
            {
                return "N/A";
            }
        }

        private string ExtractComputerName(EventLogEntry entry)
        {
            try
            {
                return entry.MachineName;
            }
            catch
            {
                return "N/A";
            }
        }

        private string ExtractIpAddress(EventLogEntry entry)
        {
            try
            {
                // Парсим IP адрес из сообщения события
                string[] parts = entry.Message.Split('\n');
                foreach (string part in parts)
                {
                    if (part.Contains("Адрес сети:") || part.Contains("Network Address:"))
                        return part.Split(':')[1].Trim();
                }
                return "N/A";
            }
            catch
            {
                return "N/A";
            }
        }

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