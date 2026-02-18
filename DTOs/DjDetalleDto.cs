namespace API_DJCONNECT.DTOs
{
    public class DjDetalleDto
    {
        public int Id { get; set; }
        public string NombreArtistico { get; set; } = string.Empty;
        public string Ciudad { get; set; } = string.Empty;
        public string GenerosMusicales { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public string? Foto { get; set; }

        // Campos extra que NO tiene el DjPublicoDto actual:
        public string? Bio { get; set; }
        public int AniosExperiencia { get; set; }
        public double ValoracionPromedio { get; set; }
        public int NumeroValoraciones { get; set; }
    }
}