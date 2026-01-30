namespace VerafinViewer.Models
{
    public class FilesProcessedDto
    {
        public long Id { get; set; }

        public DateTime DateCopied { get; set; }

        public string? FileName { get; set; }

        public string? NewFileName { get; set; }

        public string? Pickup { get; set; }
    }
}
