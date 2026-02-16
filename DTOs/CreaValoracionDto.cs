namespace API_DJCONNECT.DTOs
{
    public class CrearValoracionDto
    {
        public int ReservaId { get; set; }
        public int Puntuacion { get; set; } // 1 a 5 estrellas
        public string Comentario { get; set; } = string.Empty;
    }
}
