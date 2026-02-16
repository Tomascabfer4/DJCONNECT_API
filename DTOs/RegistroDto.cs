namespace API_DJCONNECT.DTOs
{
    public class RegistroDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty; // La contraseña plana
        public string? Telefono { get; set; }
        public string? Ubicacion { get; set; }
    }
}