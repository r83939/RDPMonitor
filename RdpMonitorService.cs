using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;

namespace RdpMonitor
{
    public class RdpMonitorService : ServiceBase
    {
        private RdpMonitorServiceLogic? _serviceLogic;
        private Thread? _serviceThread;

        public RdpMonitorService()
        {
            ServiceName = "RdpMonitorService";
            CanStop = true;
            CanPauseAndContinue = false;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            LogMessage("Служба RDP Monitor запускается...");
            
            _serviceLogic = new RdpMonitorServiceLogic();
            _serviceThread = new Thread(_serviceLogic.StartMonitoring);
            _serviceThread.IsBackground = true;
            _serviceThread.Start();
            
            LogMessage("Служба RDP Monitor успешно запущена");
        }

        protected override void OnStop()
        {
            LogMessage("Служба RDP Monitor останавливается...");
            
            _serviceLogic?.StopMonitoring();
            _serviceThread?.Join(5000);
            
            LogMessage("Служба RDP Monitor остановлена");
        }

        protected override void OnShutdown()
        {
            LogMessage("Служба RDP Monitor завершает работу (shutdown)...");
            _serviceLogic?.StopMonitoring();
            base.OnShutdown();
        }

        private void LogMessage(string message)
        {
            try
            {
                string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [SERVICE] {message}";
                File.AppendAllText(logFile, logEntry + Environment.NewLine);
                
                EventLog.WriteEntry(ServiceName, message, EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(ServiceName, $"Ошибка логирования: {ex.Message}", EventLogEntryType.Error);
            }
        }
    }
}