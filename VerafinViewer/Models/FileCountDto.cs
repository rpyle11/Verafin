namespace VerafinViewer.Models
{
    public class FileCountDto
    {
        public long Id { get; set; }

        public string? PickupLocation { get; set; }

        public int PickupLocationCount { get; set; }

        public int CopiedCount { get; set; }

        public DateTime DateInserted { get; set; }
 

    }
}
