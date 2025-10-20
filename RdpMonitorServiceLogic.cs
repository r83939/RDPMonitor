using System;
using System.IO;
using System.Threading;

namespace RdpMonitor
{
    public class RdpMonitorServiceLogic
    {
        private readonly RdpMonitor _monitor;
        private bool _isRunning;
        private readonly object _lockObject = new object();

        public RdpMonitorServiceLogic()
        {
            _monitor = new RdpMonitor();
        }

        public void StartMonitoring()
        {
            lock (_lockObject)
            {
                if (_isRunning) return;
                _isRunning = true;
            }

            try
            {
                LogMessage("Запуск мониторинга RDP входов...");
                _monitor.Initialize();
                LogMessage("Мониторинг запущен успешно");

                while (_isRunning)
                {
                    Thread.Sleep(1000);
                    
                    if (!_monitor.IsRunning())
                    {
                        LogMessage("Мониторинг остановился неожиданно, перезапуск...");
                        _monitor.Initialize();
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Критическая ошибка в мониторинге: {ex.Message}");
            }
        }

        public void StopMonitoring()
        {
            lock (_lockObject)
            {
                if (!_isRunning) return;
                _isRunning = false;
            }

            try
            {
                LogMessage("Остановка мониторинга...");
                _monitor?.Stop();
                LogMessage("Мониторинг остановлен");
            }
            catch (Exception ex)
            {
                LogMessage($"Ошибка при остановке мониторинга: {ex.Message}");
            }
        }

        private void LogMessage(string message)
        {
            try
            {
                string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [SERVICE] {message}";
                File.AppendAllText(logFile, logEntry + Environment.NewLine);
            }
            catch
            {
                // Игнорируем ошибки логирования
            }
        }
    }
}