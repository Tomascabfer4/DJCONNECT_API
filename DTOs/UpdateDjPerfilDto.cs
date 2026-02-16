namespace API_DJCONNECT.DTOs
{
    public class UpdateDjPerfilDto
    {
        public string? NombreArtistico { get; set; }
        public string? Bio { get; set; }
        public string Generos { get; set; } = null!;
        public decimal PrecioPorHora { get; set; }
        public int AniosExperiencia { get; set; }
    }
}