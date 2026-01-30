namespace VerafinFileMover.Models
{
    public class AppLog
    {
        public string? LogMsg { get; init; }

        public string? AppUser { get; init; }

        public MessageTypeEnum MessageType { get; init; }

        public bool SendEmail { get; init; }

        public enum MessageTypeEnum
        {
            Error, Warning
        }
    }
}
