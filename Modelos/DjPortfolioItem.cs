using System.Text.Json.Serialization;

namespace API_DJCONNECT.Modelos
{
    public class DjPortfolioItem
    {
        public int Id { get; set; }

        public int UsuarioId { get; set; } // FK al DJ

        public string Tipo { get; set; } = null!; // "imagen", "video", "musica"
        public string Url { get; set; } = null!;  // La URL de Cloudinary
        public string PublicId { get; set; } = null!; // ID interno de Cloudinary (para borrarlo luego)
        public string? Titulo { get; set; } // Opcional: "Mi sesión en Ibiza"
        public DateTime FechaSubida { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public Usuario Usuario { get; set; } = null!;
    }
}