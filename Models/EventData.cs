using System.Management;

namespace RdpMonitor.Models;

public class EventData
{
    public int EventId { get; set; }
    public DateTime TimeCreated { get; set; }
    public string LogName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    
    // Данные из события
    public string SubjectUserSid { get; set; } = string.Empty;
    public string SubjectUserName { get; set; } = string.Empty;
    public string SubjectDomainName { get; set; } = string.Empty;
    public string SubjectLogonId { get; set; } = string.Empty;
    
    public string TargetUserSid { get; set; } = string.Empty;
    public string TargetUserName { get; set; } = string.Empty;
    public string TargetDomainName { get; set; } = string.Empty;
    public string TargetLogonId { get; set; } = string.Empty;
    
    public string LogonType { get; set; } = string.Empty;
    public string WorkstationName { get; set; } = string.Empty;
    public string SourceNetworkAddress { get; set; } = string.Empty;
    public string SourcePort { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string ProcessId { get; set; } = string.Empty;
    public string LogonProcessName { get; set; } = string.Empty;
    public string AuthenticationPackageName { get; set; } = string.Empty;

    public static EventData FromManagementObject(ManagementBaseObject targetInstance)
    {
        var eventData = new EventData
        {
            EventId = Convert.ToInt32(targetInstance["EventCode"]),
            TimeCreated = ManagementDateTimeConverter.ToDateTime(targetInstance["TimeGenerated"].ToString()),
            LogName = targetInstance["LogFile"]?.ToString() ?? string.Empty,
            Source = targetInstance["SourceName"]?.ToString() ?? string.Empty
        };

        var insertionStrings = targetInstance["InsertionStrings"] as string[];
        
        if (insertionStrings != null && insertionStrings.Length >= 20)
        {
            eventData.SubjectUserSid = GetSafeString(insertionStrings, 0);
            eventData.SubjectUserName = GetSafeString(insertionStrings, 1);
            eventData.SubjectDomainName = GetSafeString(insertionStrings, 2);
            eventData.SubjectLogonId = GetSafeString(insertionStrings, 3);
            
            eventData.TargetUserSid = GetSafeString(insertionStrings, 4);
            eventData.TargetUserName = GetSafeString(insertionStrings, 5);
            eventData.TargetDomainName = GetSafeString(insertionStrings, 6);
            eventData.TargetLogonId = GetSafeString(insertionStrings, 7);
            
            eventData.LogonType = GetSafeString(insertionStrings, 8);
            eventData.LogonProcessName = GetSafeString(insertionStrings, 9);
            eventData.AuthenticationPackageName = GetSafeString(insertionStrings, 10);
            eventData.WorkstationName = GetSafeString(insertionStrings, 11);
            eventData.SourceNetworkAddress = GetSafeString(insertionStrings, 18);
            eventData.SourcePort = GetSafeString(insertionStrings, 19);
            
            eventData.ProcessId = GetSafeString(insertionStrings, 16);
            eventData.ProcessName = GetSafeString(insertionStrings, 17);
        }

        return eventData;
    }

    private static string GetSafeString(string[] array, int index)
    {
        return array != null && index < array.Length ? array[index] ?? "N/A" : "N/A";
    }

    public string ToTelegramMessage()
    {
        var logonTypeDesc = GetLogonTypeDescription(LogonType);
        
        var message = "🚨 **Успешный RDP вход на сервер!**\n\n" +
                     $"📅 **Время:** {TimeCreated:dd.MM.yyyy HH:mm:ss}\n" +
                     $"👤 **Пользователь:** {TargetDomainName}\\{TargetUserName}\n" +
                     $"🌐 **IP адрес:** {SourceNetworkAddress}\n" +
                     $"💻 **Рабочая станция:** {WorkstationName}\n" +
                     $"🔧 **Тип входа:** {logonTypeDesc} ({LogonType})\n" +
                     $"🆔 **ID входа:** {TargetLogonId}\n";

        if (!string.IsNullOrEmpty(ProcessName) && ProcessName != "N/A" && ProcessName != "-")
        {
            message += $"⚙️ **Процесс:** {ProcessName} (PID: {ProcessId})\n";
        }

        message += $"🔐 **Пакет аутентификации:** {AuthenticationPackageName}\n" +
                  $"📝 **Процесс входа:** {LogonProcessName}";

        return message;
    }

    public string ToConsoleLog()
    {
        var logonTypeDesc = GetLogonTypeDescription(LogonType);
        return $"[{TimeCreated:HH:mm:ss}] Event {EventId} - User: {TargetDomainName}\\{TargetUserName}, " +
               $"IP: {SourceNetworkAddress}, Workstation: {WorkstationName}, LogonType: {LogonType} ({logonTypeDesc})";
    }

    public string ToDebugLog()
    {
        return $"[{TimeCreated:HH:mm:ss}] DEBUG - LogonType: '{LogonType}', " +
               $"Workstation: '{WorkstationName}', IP: '{SourceNetworkAddress}', " +
               $"AuthPackage: '{AuthenticationPackageName}', Process: '{ProcessName}'";
    }

    private static string GetLogonTypeDescription(string logonType)
    {
        return logonType switch
        {
            "2" => "Интерактивный (локальный)",
            "3" => "Сетевой",
            "4" => "Пакетный",
            "5" => "Служба",
            "7" => "Разблокировка",
            "8" => "NetworkCleartext",
            "9" => "NewCredentials",
            "10" => "RemoteInteractive (RDP)",
            "11" => "Кэшированные учетные данные",
            "12" => "RemoteInteractive (кэшированный)",
            "13" => "RemoteInteractive (Unlock)",
            _ => $"Неизвестный ({logonType})"
        };
    }

    public bool IsRdpLogin()
    {
        // 1. Прямой RDP вход (тип 10)
        if (LogonType == "10")
            return true;
        
        // 2. Сетевой вход (тип 3) с внешним IP - ОСНОВНОЙ КРИТЕРИЙ ДЛЯ RDP
        if (LogonType == "3")
        {
            // Проверяем, что это не системный пользователь
            bool isValidUser = IsValidUserLogin();
            
            // Проверяем, что есть внешний IP адрес
            bool hasExternalIp = !string.IsNullOrEmpty(SourceNetworkAddress) &&
                                SourceNetworkAddress != "-" &&
                                SourceNetworkAddress != "127.0.0.1" &&
                                SourceNetworkAddress != "::1" &&
                                SourceNetworkAddress != "N/A";
            
            // Дополнительные признаки RDP
            bool hasRdpIndicators = WorkstationName?.Contains("DESKTOP-") == true ||
                                   WorkstationName?.Contains("TERMSRV") == true ||
                                   AuthenticationPackageName?.Contains("NTLM") == true ||
                                   LogonProcessName?.Contains("NtLmSsp") == true;
            
            Console.WriteLine($"[DEBUG] RDP Check - User: {isValidUser}, IP: {hasExternalIp}, Indicators: {hasRdpIndicators}");
            
            return isValidUser && hasExternalIp && hasRdpIndicators;
        }
        
        return false;
    }

    public bool IsValidUserLogin()
    {
        // Фильтруем системные учетные записи
        var systemUsers = new[] { "СИСТЕМА", "SYSTEM", "LOCAL SERVICE", "NETWORK SERVICE", "ANONYMOUS LOGON" };
        
        return !systemUsers.Contains(TargetUserName.ToUpper()) &&
               !string.IsNullOrEmpty(TargetUserName) &&
               TargetUserName != "-" &&
               TargetUserName != "N/A";
    }
}