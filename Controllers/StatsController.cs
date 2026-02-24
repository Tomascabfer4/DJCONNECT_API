using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API_DJCONNECT.Data;
using Microsoft.AspNetCore.Authorization;

namespace API_DJCONNECT.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "dj")] // Estas métricas son privadas para el creador
    public class StatsController : ControllerBase
    {
        private readonly DjConnectContext _context;

        public StatsController(DjConnectContext context)
        {
            _context = context;
        }

        // ==========================================
        // Vista 'DJDashboard.jsx'.
        // FLUJO: Es la primera petición que hace el DJ al entrar.
        // Hace varias consultas rápidas a la DB para sumar el dinero ganado, contar peticiones pendientes
        // y buscar cuál es el próximo bolo en el calendario para ponerlo en el Banner principal.
        // Devuelve todo empaquetado en un único JSON para no saturar la red con múltiples peticiones.
        // ==========================================
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var userIdStr = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            int djId = int.Parse(userIdStr);

            // 1. Suma el dinero de los eventos finalizados o aceptados
            var ingresosTotales = await _context.Reservas
                .Where(r => r.DjId == djId && (r.Estado == "finalizada" || r.Estado == "aceptada"))
                .SumAsync(r => r.PrecioAcordado);

            // 2. Notificaciones de eventos pendientes
            var reservasPendientes = await _context.Reservas
                .CountAsync(r => r.DjId == djId && r.Estado == "pendiente");

            // 3. Número de bolos logrados
            var totalBolos = await _context.Reservas
                .CountAsync(r => r.DjId == djId && (r.Estado == "aceptada" || r.Estado == "finalizada"));

            // 4. Leer la nota media de su perfil
            var perfil = await _context.DjPerfiles
                .FirstOrDefaultAsync(p => p.UsuarioId == djId);
            double notaMedia = (double)(perfil?.ValoracionPromedio ?? 0);

            // 5. Buscar el evento aceptado más próximo en fecha
            var proximoEvento = await _context.Reservas
                .Where(r => r.DjId == djId && r.Estado == "aceptada" && r.FechaEvento >= DateTime.UtcNow)
                .OrderBy(r => r.FechaEvento)
                .Select(r => new {
                    r.FechaEvento,
                    r.UbicacionEvento,
                    ClienteNombre = r.Cliente.Nombre
                })
                .FirstOrDefaultAsync();

            // Devolver todo el paquete listo para las tarjetas del Dashboard
            return Ok(new
            {
                Ingresos = ingresosTotales,
                Pendientes = reservasPendientes,
                TotalEventos = totalBolos,
                Valoracion = notaMedia,
                ProximoEvento = proximoEvento
            });
        }
    }
}