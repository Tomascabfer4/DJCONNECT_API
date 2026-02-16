using System.ComponentModel.DataAnnotations;

namespace API_DJCONNECT.DTOs
{
    public class PerfilFotoDto
    {
        [Required]
        public IFormFile File { get; set; } = null!;
    }
}