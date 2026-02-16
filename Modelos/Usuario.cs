using System.Text.Json.Serialization;

namespace API_DJCONNECT.Modelos;

public class Usuario
{
    public int Id { get; set; }

    public string Nombre { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string? TipoUsuario { get; set; }
    public string? FotoPerfil { get; set; }
    // Para encontrar en cloudinary donde esta la foto antigua para poder eliminarla
    public string? FotoPerfilPublicId { get; set; }
    public string? Ubicacion { get; set; }
    public string? Telefono { get; set; }
    public bool Activo { get; set; } = true;
    public DjPerfil? DjPerfil { get; set; }
    [JsonIgnore]
    public ICollection<Reserva> ReservasComoDj { get; set; } = new List<Reserva>();
    [JsonIgnore]
    public ICollection<Reserva> ReservasComoCliente { get; set; } = new List<Reserva>();
    public ICollection<DjPortfolioItem> Portfolio { get; set; } = new List<DjPortfolioItem>();
}
