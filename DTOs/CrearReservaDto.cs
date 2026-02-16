namespace API_DJCONNECT.DTOs
{
    public class CrearReservaDto
    {
        public int DjId { get; set; } // A quién contrato
        public DateTime FechaEvento { get; set; }
        public string Horario { get; set; } = string.Empty;
        public string TipoEvento { get; set; } = string.Empty;
        public string UbicacionEvento { get; set; } = string.Empty;
        // El precio y el estado NO se ponen aquí, los calculas tú en el Backend
    }
}