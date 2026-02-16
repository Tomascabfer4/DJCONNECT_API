namespace API_DJCONNECT.Modelos;
public class DjPerfil
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }

    public string? NombreArtistico { get; set; }
    public string? Bio { get; set; }
    public string Generos { get; set; } = null!;
    public decimal PrecioPorHora { get; set; }
    public int AniosExperiencia { get; set; }
    public double ValoracionPromedio { get; set; } = 0;

    public Usuario Usuario { get; set; } = null!;
}
