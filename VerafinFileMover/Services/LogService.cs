using Microsoft.Extensions.Options;
using VerafinFileMover.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace VerafinFileMover.Services
{
    public class LogService(IOptions<AppSettings> settings, HttpClient httpClient) : ILogService
    {
        public Task<bool> LogAlert(AppLog appLog)
        {
            var log = new AppLogDto
            {
                AppName = typeof(Program).Namespace,
                AppUser = appLog.AppUser,
                AppVersion = typeof(Program).Assembly.GetName().Version?.ToString(),
                EmailSubject = settings.Value.AppLogEmailSubject,
                FromAddress = settings.Value.AppLogFromEmail,
                LogDate = DateTime.Now,
                LogMessage = appLog.LogMsg,
                MessageType = appLog.MessageType.ToString(),
                SendEmailAddressList = appLog.SendEmail ? settings.Value.AppLogNotifyEmail : string.Empty,
            };
            return SendLog(log);
        }

        private async Task<bool> SendLog(AppLogDto log)
        {
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var response = await httpClient.PostAsJsonAsync(string.Empty, log);

            return response.IsSuccessStatusCode;
        }
    }
}
