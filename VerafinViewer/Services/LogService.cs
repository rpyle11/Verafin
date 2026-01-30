using Microsoft.Extensions.Options;
using VerafinViewer.Models;

namespace VerafinViewer.Services
{
    public class LogService(HttpClient httpClient, IOptions<AppSettings> settings) : ILogService
    {
        public async Task<bool> LogAlert(AppLog appLog)
        {
            var log = new AppLogDto
            {
                AppName = typeof(Program).Namespace,
                AppUser = appLog.AppUser,
                AppVersion = typeof(LogService).Assembly.GetName().Version?.ToString(),
                EmailSubject = settings.Value.LogAlertSubject,
                FromAddress = settings.Value.LogAlertFromEmail,
                LogDate = DateTime.Now,
                LogMessage = appLog.LogMsg,
                MessageType = appLog.MessageType.ToString(),
                SendEmailAddressList = appLog.SendEmail ? settings.Value.LogAlertToEmailList : string.Empty,
            };
            return await SendLog(log);
        }

        private async Task<bool> SendLog(AppLogDto log)
        {

            httpClient.DefaultRequestHeaders.Clear();
            var response = await httpClient.PostAsJsonAsync(httpClient.BaseAddress, log);
            return response.IsSuccessStatusCode;

        }
    }
}
