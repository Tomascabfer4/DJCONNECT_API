using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_DJCONNECT.Modelos
{
    public class Mensaje
    {
        [Key]
        public int Id { get; set; }

        public int ReservaId { get; set; } // ¿A qué chat pertenece?
        public int EmisorId { get; set; }  // ¿Quién lo escribió?

        [Required]
        public string Contenido { get; set; } = string.Empty;

        public DateTime FechaEnvio { get; set; } = DateTime.UtcNow;

        // Relaciones de navegación
        [ForeignKey("ReservaId")]
        public Reserva Reserva { get; set; } = null!; // Relación con la reserva

        [ForeignKey("EmisorId")]
        public Usuario Emisor { get; set; } = null!;  // Relación con el usuario
    }
}