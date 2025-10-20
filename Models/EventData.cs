using System;
using System.Diagnostics;

namespace RdpMonitor.Models
{
    public class EventData
    {
        public string UserName { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string Workstation { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public DateTime Time { get; set; }
        public string EventType { get; set; } = string.Empty;

        public static EventData FromEventLogEntry(EventLogEntry entry)
        {
            var eventData = new EventData
            {
                Time = entry.TimeGenerated,
                EventType = "RDP Login"
            };

            // Парсим информацию из сообщения события
            ParseEventMessage(entry.Message, eventData);
            
            return eventData;
        }

        private static void ParseEventMessage(string message, EventData eventData)
        {
            try
            {
                string[] lines = message.Split('\n');
                foreach (string line in lines)
                {
                    if (line.Contains("Имя пользователя:") || line.Contains("User Name:"))
                    {
                        eventData.UserName = line.Split(':')[1].Trim();
                    }
                    else if (line.Contains("Домен:") || line.Contains("Domain:"))
                    {
                        eventData.Domain = line.Split(':')[1].Trim();
                    }
                    else if (line.Contains("Workstation Name:") || line.Contains("Имя рабочей станции:"))
                    {
                        eventData.Workstation = line.Split(':')[1].Trim();
                    }
                    else if (line.Contains("Адрес сети:") || line.Contains("Network Address:"))
                    {
                        eventData.IpAddress = line.Split(':')[1].Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                // Используем прямое логирование вместо статического метода
                try
                {
                    string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [EVENTDATA] Ошибка парсинга сообщения события: {ex.Message}";
                    File.AppendAllText(logFile, logEntry + Environment.NewLine);
                }
                catch
                {
                    // Игнорируем ошибки логирования
                }
            }
        }

        public override string ToString()
        {
            return $"User: {UserName}, Domain: {Domain}, Workstation: {Workstation}, IP: {IpAddress}, Time: {Time}";
        }
    }
}