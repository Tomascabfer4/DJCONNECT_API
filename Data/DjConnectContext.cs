using Microsoft.EntityFrameworkCore;
using API_DJCONNECT.Modelos;

namespace API_DJCONNECT.Data;

public class DjConnectContext : DbContext
{
    public DjConnectContext(DbContextOptions<DjConnectContext> options)
        : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<DjPerfil> DjPerfiles => Set<DjPerfil>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<DjPortfolioItem> DjPortfolioItems => Set<DjPortfolioItem>(); // 1. Nueva tabla
    public DbSet<Valoracion> Valoraciones { get; set; }
    public DbSet<Mensaje> Mensajes => Set<Mensaje>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ===============================
        // TABLAS
        // ===============================
        modelBuilder.Entity<Usuario>().ToTable("usuarios");
        modelBuilder.Entity<DjPerfil>().ToTable("dj_perfiles");
        modelBuilder.Entity<Reserva>().ToTable("reservas");
        modelBuilder.Entity<DjPortfolioItem>().ToTable("dj_portfolio_items");

        // ===============================
        // USUARIO
        // ===============================
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(u => u.Id);

            // FILTRO GLOBAL PADRE
            entity.HasQueryFilter(u => u.Activo);

            entity.Property(u => u.Nombre).IsRequired();
            entity.Property(u => u.Email).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.PasswordHash).IsRequired();
        });

        // ===============================
        // DJ PERFIL (1 - 1)
        // ===============================
        modelBuilder.Entity<DjPerfil>(entity =>
        {
            entity.HasKey(d => d.Id);

            // CORRECCIÓN 1: Si el usuario está inactivo, ocultamos el perfil
            entity.HasQueryFilter(d => d.Usuario.Activo);

            entity.Property(d => d.Generos).IsRequired();
            entity.Property(d => d.PrecioPorHora).HasPrecision(10, 2);
            entity.HasIndex(d => d.UsuarioId).IsUnique();

            entity.HasOne(d => d.Usuario)
                .WithOne(u => u.DjPerfil)
                .HasForeignKey<DjPerfil>(d => d.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ===============================
        // RESERVAS
        // ===============================
        modelBuilder.Entity<Reserva>(entity =>
        {
            entity.HasKey(r => r.Id);
            // NOTA: Aquí NO ponemos filtro para mantener el historial.

            entity.Property(r => r.Horario).IsRequired();
            entity.Property(r => r.TipoEvento).IsRequired();
            entity.Property(r => r.UbicacionEvento).IsRequired();
            entity.Property(r => r.Estado).IsRequired();
            entity.Property(r => r.PrecioAcordado).HasPrecision(10, 2);

            entity.HasOne(r => r.Dj)
                .WithMany(u => u.ReservasComoDj)
                .HasForeignKey(r => r.DjId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Cliente)
                .WithMany(u => u.ReservasComoCliente)
                .HasForeignKey(r => r.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(r => r.DjId);
            entity.HasIndex(r => r.ClienteId);
            entity.HasIndex(r => r.FechaEvento);
        });

        // ===============================
        // PORTFOLIO (1 - N)
        // ===============================
        modelBuilder.Entity<DjPortfolioItem>(entity =>
        {
            entity.HasKey(p => p.Id);

            // CORRECCIÓN 2: Si el usuario está inactivo, ocultamos el portfolio
            entity.HasQueryFilter(p => p.Usuario.Activo);

            entity.Property(p => p.Tipo).IsRequired();
            entity.Property(p => p.Url).IsRequired();
            entity.Property(p => p.PublicId).IsRequired();

            entity.HasOne(p => p.Usuario)
                  .WithMany(u => u.Portfolio)
                  .HasForeignKey(p => p.UsuarioId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ===============================
        // VALORACIONES
        // ===============================
        modelBuilder.Entity<Valoracion>(entity =>
        {
            entity.ToTable("valoraciones"); // Nombre de la tabla en minúsculas
            entity.HasKey(v => v.Id);

            entity.Property(v => v.Puntuacion).IsRequired();
            entity.Property(v => v.Comentario).HasMaxLength(500); // Límite opcional

            // Relaciones con Usuario (DJ y Cliente)
            entity.HasOne(v => v.Dj)
                .WithMany() // Podrías añadir una ICollection<Valoracion> en Usuario si quieres
                .HasForeignKey(v => v.DjId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.Cliente)
                .WithMany()
                .HasForeignKey(v => v.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);
            // Relación 1-1 con Reserva: Una reserva solo puede valorarse una vez
            entity.HasOne(v => v.Reserva)
                .WithOne()
                .HasForeignKey<Valoracion>(v => v.ReservaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ===============================
        // MENSAJES
        // ===============================
        modelBuilder.Entity<Mensaje>(entity =>
        {
            entity.ToTable("mensajes");
            entity.HasKey(m => m.Id);

            // Configuración: Si se borra la reserva, se borran los mensajes (Cascade)
            entity.HasOne(m => m.Reserva)
                .WithMany()
                .HasForeignKey(m => m.ReservaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configuración: Si se borra un usuario, NO borramos sus mensajes antiguos (Restrict)
            // Esto mantiene el historial del chat
            entity.HasOne(m => m.Emisor)
                .WithMany()
                .HasForeignKey(m => m.EmisorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}