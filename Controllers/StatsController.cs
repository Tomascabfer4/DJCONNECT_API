using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API_DJCONNECT.Data;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System;

namespace API_DJCONNECT.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "dj")] // Solo para DJs
    public class StatsController : ControllerBase
    {
        private readonly DjConnectContext _context;

        public StatsController(DjConnectContext context)
        {
            _context = context;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var userIdStr = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            int djId = int.Parse(userIdStr);

            var ingresosTotales = await _context.Reservas
                .Where(r => r.DjId == djId && (r.Estado == "finalizada" || r.Estado == "aceptada"))
                .SumAsync(r => r.PrecioAcordado);

            var reservasPendientes = await _context.Reservas
                .CountAsync(r => r.DjId == djId && r.Estado == "pendiente");

            var totalBolos = await _context.Reservas
                .CountAsync(r => r.DjId == djId && (r.Estado == "aceptada" || r.Estado == "finalizada"));

            // ✅ LEEMOS DIRECTAMENTE DEL PERFIL (Rápido y sencillo)
            var perfil = await _context.DjPerfiles
                .FirstOrDefaultAsync(p => p.UsuarioId == djId);

            double notaMedia = (double)(perfil?.ValoracionPromedio ?? 0);

            var proximoEvento = await _context.Reservas
                .Where(r => r.DjId == djId && r.Estado == "aceptada" && r.FechaEvento >= DateTime.UtcNow)
                .OrderBy(r => r.FechaEvento)
                .Select(r => new {
                    r.FechaEvento,
                    r.UbicacionEvento,
                    ClienteNombre = r.Cliente.Nombre
                })
                .FirstOrDefaultAsync();

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