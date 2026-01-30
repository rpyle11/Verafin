using Microsoft.Extensions.Options;
using Serilog;
using System.Reflection;
using System.Timers;
using VerafinFileMover.Models;
using VerafinFileMover.Services;
using Timer = System.Timers.Timer;

namespace VerafinFileMover
{
    public class Worker(IOptions<AppSettings> settings, ILogService logService, IFileMoverService fileMoverService, IHostApplicationLifetime hostApplicationLifetime) : BackgroundService
    {
        private Timer? _svcTimer;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
               
                if (_svcTimer == null)
                {
                    TimerReset();

                    _svcTimer?.Enabled = true;
                }
               

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
        }

        private void TimerReset()
        {
            Log.Information("Resetting Timer");
            _svcTimer = new Timer();
            _svcTimer.Interval = settings.Value.TimerInterval;
            _svcTimer.AutoReset = true;
            _svcTimer.Elapsed += TimerElapsed;
        }

        private async void TimerElapsed(object? sender, ElapsedEventArgs elapsed)
        {
           
            try
            {
                _svcTimer?.Enabled = false;

                if (await fileMoverService.FileCopyProcess())
                {
                    if (settings.Value.AppWriteToFileLogging) Log.Information("File Copy process completed successfully");

                    _svcTimer?.Enabled = true;
                }
                else
                {
                    throw new ApplicationException("Unable to complete Verafin File mover process");
                }

            }
            catch (Exception ex)
            {
                var logMsg = $"Error: {ex.Message}";

                if (ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message))
                {
                    logMsg += $"Inner Message {ex.InnerException.Message}";
                }

                Log.Error("Error {logMsg}", logMsg);
                await logService.LogAlert(new AppLog
                {
                    LogMsg = $"{ex.GetType().Name} Error in {MethodName.GetMethodName(MethodBase.GetCurrentMethod())}, {logMsg}",
                    MessageType = AppLog.MessageTypeEnum.Error,
                    SendEmail = true,
                    AppUser = ServiceAcctName.GetServiceAccountName(settings.Value)

                });

                hostApplicationLifetime.StopApplication();
            }


           
        }
    }
}
