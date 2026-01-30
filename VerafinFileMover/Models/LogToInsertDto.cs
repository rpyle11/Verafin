using System.ComponentModel.DataAnnotations;

namespace VerafinFileMover.Models
{
    public  class LogToInsertDto
    {
        [Required] public DateTime InsertDate { get; set; }

        [Required] public string? Message { get; set; }

        [Required] public string? AppUser { get; init; }

        [Required] public string? AppMethod { get; set; }
    }
}
