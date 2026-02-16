using System.ComponentModel.DataAnnotations;

namespace API_DJCONNECT.DTOs
{
    public class PortfolioUploadDto
    {
        [Required]
        public IFormFile File { get; set; } = null!;

        [Required]
        public string Tipo { get; set; } = null!; // "imagen", "video", "musica"

        public string? Titulo { get; set; }
    }
}