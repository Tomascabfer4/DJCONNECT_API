namespace API_DJCONNECT.DTOs
{
    public class UpdateClienteDto
    {
        public string Nombre { get; set; } = null!;
        public string? Telefono { get; set; }
        public string? Ubicacion { get; set; }
        public string? FotoPerfil { get; set; }
    }
}