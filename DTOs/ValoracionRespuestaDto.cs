namespace API_DJCONNECT.DTOs
{
    public class ValoracionRespuestaDto
    {
        public int Id { get; set; }
        public string ClienteNombre { get; set; } = string.Empty;
        public int Puntuacion { get; set; }
        public string Comentario { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
    }
}