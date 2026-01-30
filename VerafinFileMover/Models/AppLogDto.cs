// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace VerafinFileMover.Models
{
    public class AppLogDto
    {
        public string? AppName { get; set; }


        public string? AppVersion { get; set; }


        public string? AppUser { get; set; }

        public DateTime? LogDate { get; set; }

        public string? LogMessage { get; set; }

        public string? MessageType { get; set; }

        public string? FromAddress { get; set; }


        public string? SendEmailAddressList { get; set; }


        public string? EmailSubject { get; set; }
    }
}
