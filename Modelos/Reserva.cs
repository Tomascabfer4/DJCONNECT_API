namespace API_DJCONNECT.Modelos;

public class Reserva
{
    public int Id { get; set; }

    public int DjId { get; set; }
    public Usuario Dj { get; set; } = null!;

    public int ClienteId { get; set; }
    public Usuario Cliente { get; set; } = null!;

    public DateTime FechaEvento { get; set; }
    public string Horario { get; set; } = null!;
    public string TipoEvento { get; set; } = null!;
    public string UbicacionEvento { get; set; } = null!;
    public decimal PrecioAcordado { get; set; }
    public string Estado { get; set; } = "pendiente";
}
