namespace API_DJCONNECT.DTOs
{

    /// <summary>
    /// DTO (Data Transfer Object) que representa la ficha pública de un DJ.
    /// <para>
    /// Sirve para filtrar la información que se envía al Frontend, ocultando datos sensibles
    /// (como PasswordHash, Email privado, ID interno) y combinando datos de las tablas
    /// 'Usuarios' y 'DjPerfiles' en un solo objeto simple.
    /// </para>
    /// </summary>
    public class DjPublicoDto
    {
        public string NombreArtistico { get; set; } = string.Empty;
        public string Ciudad { get; set; } = string.Empty;
        public string GenerosMusicales { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public string? Foto { get; set; }
    }
}
