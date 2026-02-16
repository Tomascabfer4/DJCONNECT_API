namespace API_DJCONNECT.DTOs
{
    public class UsuarioPerfilDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Foto { get; set; }
        public string Rol { get; set; } = string.Empty;
        // Aquí puedes añadir datos extra si es DJ, como su caché
    }
}