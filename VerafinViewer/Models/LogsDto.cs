namespace VerafinViewer.Models
{
    public class LogsDto
    {
        public long Id { get; set; }

        public DateTime DateInserted { get; set; }

        public string? ProcessMessage { get; set; }

        public string? MessageType { get; set; }
    }
}
