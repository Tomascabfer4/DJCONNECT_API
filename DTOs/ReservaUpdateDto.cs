namespace API_DJCONNECT.DTOs
{
    public class ReservaUpdateDto
    {
        public DateTime FechaEvento { get; set; }
        public string UbicacionEvento { get; set; } = null!;
        public string TipoEvento { get; set; } = null!;
        // No incluimos el precio ni el DJ, porque eso no debería cambiar 
        // una vez que se ha solicitado la reserva inicial.
    }
}