using System.ComponentModel.DataAnnotations;

namespace VerafinFileMover.Models
{
    public  class AppSettings
    {
        public double TimerInterval { get; init; }
        public string? ServiceName { get; init; }
        public string? AppLogEmailSubject { get; init; }
        [MaxLength(500)]
        public string? AppLogFromEmail { get; init; }
        [MaxLength(500)]
        public string? AppLogNotifyEmail { get; init; }

        public bool AppWriteToFileLogging { get; init; }

        public bool CopyOldFiles { get; init; }

        public string? OldFileDropoffLocation { get; init; }

        public DateTime RecoveryBeginning { get; set; }

        public DateTime RecoveryEnding { get; set; }
    }
}
