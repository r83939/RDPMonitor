using System.Management;
using Microsoft.Extensions.Configuration;
using RdpMonitor.Models;
using RdpMonitor.Services;

namespace RdpMonitor;

class Program
{
    private static ITelegramService? _telegramService;
    private static AppConfig _config = new();
    private static readonly HashSet<string> _processedLogonIds = new();
    private static readonly object _lockObject = new object();
    
    static async Task Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
           Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 💥 Критическая ошибка: {e.ExceptionObject}");
           Environment.Exit(1);
        };
        Console.WriteLine("🚀 Запуск мониторинга RDP входов...");
        Console.WriteLine("Для остановки нажмите Ctrl+C\n");

        // Загрузка конфигурации
        if (!LoadConfiguration())
        {
            Console.WriteLine("❌ Не удалось загрузить конфигурацию!");
            return;
        }

        // Инициализация сервиса Telegram
        _telegramService = new TelegramService(_config.Telegram.BotToken, _config.Telegram.ChatId);

        try
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📤 Отправка тестового сообщения...");
            var testMessage = await _telegramService.SendMessageAsync(
                "🔔 **Мониторинг RDP запущен!**\n" +
                $"Сервер начал отслеживание входов.\n" +
                $"📊 Журнал: {_config.Monitoring.LogName}\n" +
                $"🎯 EventID: {_config.Monitoring.TargetEventId}\n" +
                $"🔄 Дедупликация: включена"
            );

            if (testMessage)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ Тестовое сообщение отправлено");
            }

            await StartEventMonitoring();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 💥 Критическая ошибка: {ex.Message}");
        }
    }

    private static bool LoadConfiguration()
    {
        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            configuration.Bind(_config);

            // Валидация конфигурации
            if (string.IsNullOrEmpty(_config.Telegram.BotToken) || _config.Telegram.BotToken == "YOUR_BOT_TOKEN_HERE")
            {
                Console.WriteLine("❌ Не задан токен бота Telegram в appsettings.json");
                return false;
            }

            if (string.IsNullOrEmpty(_config.Telegram.ChatId) || _config.Telegram.ChatId == "YOUR_CHAT_ID_HERE")
            {
                Console.WriteLine("❌ Не задан Chat ID в appsettings.json");
                return false;
            }

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ Конфигурация загружена");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ Ошибка загрузки конфигурации: {ex.Message}");
            return false;
        }
    }

    private static async Task StartEventMonitoring()
    {
        try
        {
            var query = new WqlEventQuery(
                $"SELECT * FROM __InstanceCreationEvent WITHIN {_config.Monitoring.CheckIntervalSeconds} " +
                $"WHERE TargetInstance ISA 'Win32_NTLogEvent' " +
                $"AND TargetInstance.LogFile = '{_config.Monitoring.LogName}' " +
                $"AND TargetInstance.EventCode = '{_config.Monitoring.TargetEventId}'"
            );

            using var watcher = new ManagementEventWatcher(query);
            
            watcher.EventArrived += async (sender, e) =>
            {
                try
                {
                    await HandleEvent(e);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ Ошибка обработки события: {ex.Message}");
                }
            };

            watcher.Start();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📊 Мониторинг событий {_config.Monitoring.LogName} запущен...");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔍 Ожидание RDP входов (EventID: {_config.Monitoring.TargetEventId})...");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔄 Дедупликация активна - одинаковые события будут фильтроваться");

            // Очистка старых ID каждые 5 минут
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(5));
                    CleanupOldEntries();
                }
            });

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                watcher.Stop();
                Console.WriteLine("\n⏹️ Мониторинг остановлен.");
                Environment.Exit(0);
            };

            await Task.Delay(-1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ Ошибка запуска мониторинга: {ex.Message}");
        }
    }

    private static async Task HandleEvent(EventArrivedEventArgs e)
    {
        var targetInstance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
        var eventData = EventData.FromManagementObject(targetInstance);

        // Выводим информацию о событии
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📋 {eventData.ToConsoleLog()}");

        // Пропускаем системных пользователей сразу
        if (!eventData.IsValidUserLogin())
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔄 Пропущено (системный пользователь)");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ──────────────────────────");
            return;
        }

        // Детальная проверка для RDP
        if (!eventData.IsRdpLogin())
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔄 Пропущено (не RDP вход)");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ──────────────────────────");
            return;
        }

        // 🔄 ДЕДУПЛИКАЦИЯ - проверяем, не обрабатывали ли мы уже это событие
        var eventKey = CreateEventKey(eventData);
        
        lock (_lockObject)
        {
            if (_processedLogonIds.Contains(eventKey))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔄 Пропущено (дубликат)");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ──────────────────────────");
                return;
            }
            
            // Добавляем в обработанные
            _processedLogonIds.Add(eventKey);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📝 Добавлено в кэш: {eventKey}");
        }

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🎯 ОБНАРУЖЕН RDP ВХОД!");

        // Отправка в Telegram
        var telegramMessage = eventData.ToTelegramMessage();
        var sendResult = await _telegramService!.SendMessageAsync(telegramMessage);

        if (sendResult)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ RDP событие отправлено в Telegram");
        }
        else
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ Ошибка отправки RDP события");
        }
        
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ──────────────────────────");
    }

    /// <summary>
    /// Создает уникальный ключ для события для дедупликации
    /// </summary>
    private static string CreateEventKey(EventData eventData)
    {
        // Ключ основан на пользователе, IP и времени (с точностью до минуты)
        // Это позволяет группировать события одного подключения
        var timeKey = eventData.TimeCreated.ToString("yyyyMMddHHmm");
        return $"{eventData.TargetUserName}@{eventData.SourceNetworkAddress}@{timeKey}";
    }

    /// <summary>
    /// Очищает старые записи из кэша дедупликации
    /// </summary>
    private static void CleanupOldEntries()
    {
        lock (_lockObject)
        {
            // В реальном приложении здесь можно добавить логику очистки старых записей
            // Пока просто логируем размер кэша
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🧹 Размер кэша дедупликации: {_processedLogonIds.Count} записей");
            
            // Очищаем кэш если он слишком большой (на всякий случай)
            if (_processedLogonIds.Count > 1000)
            {
                _processedLogonIds.Clear();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🧹 Кэш дедупликации очищен");
            }
        }
    }
}