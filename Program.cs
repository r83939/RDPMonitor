using System;
using System.ServiceProcess;

namespace RdpMonitor
{
    class Program
    {
        static void Main(string[] args)
        {
            // Проверяем, запущен ли в режиме консоли или службы
            if (Environment.UserInteractive)
            {
                // Консольный режим
                Console.WriteLine("🚀 Запуск RDP Monitor в консольном режиме...");
                Console.WriteLine("Для остановки нажмите Ctrl+C");
                
                var monitor = new RdpMonitor();
                monitor.Initialize();
                monitor.Start();
                
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    monitor.Stop();
                    Environment.Exit(0);
                };
                
                // Ожидаем бесконечно
                System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
            }
            else
            {
                // Режим службы
                ServiceBase[] servicesToRun = new ServiceBase[]
                {
                    new RdpMonitorService()
                };
                ServiceBase.Run(servicesToRun);
            }
        }
    }
}