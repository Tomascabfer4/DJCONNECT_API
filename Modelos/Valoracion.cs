namespace API_DJCONNECT.Modelos
{
    public class Valoracion
    {
        public int Id { get; set; }
        public int ReservaId { get; set; } // Vinculada a una reserva específica
        public int ClienteId { get; set; }
        public int DjId { get; set; }

        public int Puntuacion { get; set; } // De 1 a 5 estrellas
        public string Comentario { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        // Relaciones de navegación
        public Reserva Reserva { get; set; } = null!;
        public Usuario Cliente { get; set; } = null!;
        public Usuario Dj { get; set; } = null!;
    }
}
