using System;
using System.Diagnostics;
using System.Text.RegularExpressions; //

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
                EventType = "RDP Login",
                Workstation = entry.MachineName
            };

            // Парсим информацию из сообщения события
            ParseEventMessage(entry.Message, eventData);
            
            return eventData;
        }

        private static void ParseEventMessage(string message, EventData eventData)
        {
            try
            {
                // Разделяем сообщение на строки
                string[] lines = message.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    
                    // Ищем имя пользователя (разные варианты для разных языков)
                    if (trimmedLine.StartsWith("Имя пользователя:") || 
                        trimmedLine.StartsWith("User Name:") ||
                        trimmedLine.StartsWith("Account Name:") ||
                        trimmedLine.Contains("Account Name:"))
                    {
                        eventData.UserName = ExtractValue(line);
                    }
                    // Ищем домен
                    else if (trimmedLine.StartsWith("Домен:") || 
                             trimmedLine.StartsWith("Domain:") ||
                             trimmedLine.StartsWith("Account Domain:") ||
                             trimmedLine.Contains("Account Domain:"))
                    {
                        eventData.Domain = ExtractValue(line);
                    }
                    // Ищем рабочую станцию
                    else if (trimmedLine.StartsWith("Workstation Name:") || 
                             trimmedLine.StartsWith("Имя рабочей станции:") ||
                             trimmedLine.Contains("Workstation Name:"))
                    {
                        eventData.Workstation = ExtractValue(line);
                    }
                    // Ищем IP адрес (разные варианты)
                    else if (trimmedLine.StartsWith("Адрес сети:") || 
                             trimmedLine.StartsWith("Network Address:") ||
                             trimmedLine.StartsWith("Source Network Address:") ||
                             trimmedLine.Contains("Source Network Address:") ||
                             trimmedLine.StartsWith("IP Address:") ||
                             trimmedLine.Contains("IP Address:"))
                    {
                        eventData.IpAddress = ExtractValue(line);
                    }
                    // Альтернативный поиск IP через ключевые слова
                    else if (trimmedLine.Contains("Source Network Address:") || trimmedLine.Contains("Network Address:"))
                    {
                        eventData.IpAddress = ExtractValue(line);
                    }
                }

                // Если IP не найден, попробуем найти в формате IPv4
                if (eventData.IpAddress == "N/A" || string.IsNullOrEmpty(eventData.IpAddress))
                {
                    eventData.IpAddress = FindIpAddressInText(message);
                }
            }
            catch (Exception ex)
            {
                // Логируем ошибку для отладки
                try
                {
                    string logFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [EVENTDATA] Ошибка парсинга: {ex.Message}";
                    System.IO.File.AppendAllText(logFile, logEntry + Environment.NewLine);
                }
                catch
                {
                    // Игнорируем ошибки логирования
                }
            }
        }

        private static string ExtractValue(string line)
        {
            try
            {
                // Разделяем строку по двоеточию и берем вторую часть
                string[] parts = line.Split(':', 2);
                if (parts.Length >= 2)
                {
                    return parts[1].Trim();
                }
                return "N/A";
            }
            catch
            {
                return "N/A";
            }
        }

        private static string FindIpAddressInText(string text)
{
    try
    {
        // Ищем IPv4 адрес в тексте
        var ipMatch = Regex.Match(
            text, 
            @"\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b"
        );
        
        if (ipMatch.Success && ipMatch.Value != "0.0.0.0")
        {
            return ipMatch.Value;
        }
    }
    catch
    {
        // Игнорируем ошибки
    }
    
    return "N/A";
}

        public override string ToString()
        {
            return $"User: {UserName}, Domain: {Domain}, Workstation: {Workstation}, IP: {IpAddress}, Time: {Time}";
        }
    }
}