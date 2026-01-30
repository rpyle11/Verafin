namespace VerafinViewer.Models
{
    public abstract class AppLogPrep
    {
        public static AppLog AppLogSetup(string? appUser, string url, string? methodName, Exception ex)
        {
            var appLog = new AppLog
            {
                AppUser = appUser,
                MessageType = AppLog.MessageTypeEnum.Error,
                SendEmail = true,
                LogMsg =
                    $"{ex.GetType().Name} in {methodName} at url {url}, Error Message: {ex.Message}, User: {appUser}"
            };

            if (ex.InnerException != null)
                appLog.LogMsg += $" Inner Exception: {ex.InnerException.Message}";
            return appLog;
        }

        public static AppLog AppErrorLogSetup(string? appUser, string methodName, Exception ex)
        {
            var appLog = new AppLog
            {
                AppUser = appUser,
                MessageType = AppLog.MessageTypeEnum.Error,
                SendEmail = true,
                LogMsg =
                    $"{ex.GetType().Name} in {methodName}, Error Message: {ex.Message}, User: {appUser}"
            };

            if (ex.InnerException != null)
                appLog.LogMsg += $" Inner Exception: {ex.InnerException.Message}";
            return appLog;
        }

    }
}
