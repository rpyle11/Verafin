namespace VerafinViewer.Models
{
    public class AppSettings
    {
        public string? LogAlertSubject { get; set; }
        public string? LogAlertFromEmail { get; set; }
        public string? LogAlertToEmailList { get; set; }

        public int DefaultDateRange { get; set; }
    }
}
