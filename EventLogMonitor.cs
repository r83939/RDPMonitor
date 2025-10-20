using System;
using System.Diagnostics;
using System.Threading;

namespace RdpMonitor
{
    public class EventLogMonitor
    {
        private readonly string _logName;
        private bool _isMonitoring;
        private EventLog _eventLog;

        public event Action<EventLogEntry> OnEventLogged;

        public EventLogMonitor(string logName)
        {
            _logName = logName;
        }

        public void StartMonitoring()
        {
            if (_isMonitoring) return;

            try
            {
                _eventLog = new EventLog(_logName);
                _eventLog.EntryWritten += OnEntryWritten;
                _eventLog.EnableRaisingEvents = true;
                _isMonitoring = true;
                
                RdpMonitor.LogMessage($"Мониторинг журнала событий '{_logName}' запущен");
            }
            catch (Exception ex)
            {
                RdpMonitor.LogMessage($"Ошибка запуска мониторинга событий: {ex.Message}");
                throw;
            }
        }

        public void StopMonitoring()
        {
            if (!_isMonitoring) return;

            try
            {
                _eventLog.EntryWritten -= OnEntryWritten;
                _eventLog.EnableRaisingEvents = false;
                _eventLog.Dispose();
                _eventLog = null;
                _isMonitoring = false;
                
                RdpMonitor.LogMessage("Мониторинг журнала событий остановлен");
            }
            catch (Exception ex)
            {
                RdpMonitor.LogMessage($"Ошибка остановки мониторинга событий: {ex.Message}");
            }
        }

        private void OnEntryWritten(object sender, EntryWrittenEventArgs e)
        {
            OnEventLogged?.Invoke(e.Entry);
        }
    }
}