using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API_DJCONNECT.Data;
using API_DJCONNECT.Modelos;
using API_DJCONNECT.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace API_DJCONNECT.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ValoracionesController : ControllerBase
    {
        private readonly DjConnectContext _context;

        public ValoracionesController(DjConnectContext context)
        {
            _context = context;
        }

        // POST: api/Valoraciones
        [HttpPost]
        public async Task<IActionResult> PostValoracion(CrearValoracionDto dto)
        {
            var clienteId = int.Parse(User.FindFirst("id")?.Value);

            // 1. Validar que la reserva existe, es del cliente y está confirmada
            var reserva = await _context.Reservas
                .FirstOrDefaultAsync(r => r.Id == dto.ReservaId && r.ClienteId == clienteId);

            if (reserva == null) return NotFound("Reserva no encontrada.");
            if (reserva.Estado != "aceptada" && reserva.Estado != "finalizada")
                return BadRequest("Solo puedes valorar reservas aceptadas o finalizadas.");

            // 2. Validar que no se haya valorado ya (1 a 1)
            var existe = await _context.Valoraciones.AnyAsync(v => v.ReservaId == dto.ReservaId);
            if (existe) return BadRequest("Esta reserva ya ha sido valorada.");

            // 3. Crear la valoración
            var valoracion = new Valoracion
            {
                ReservaId = dto.ReservaId,
                ClienteId = clienteId,
                DjId = reserva.DjId,
                Puntuacion = Math.Clamp(dto.Puntuacion, 1, 5), // Asegura 1-5
                Comentario = dto.Comentario
            };

            _context.Valoraciones.Add(valoracion);
            await _context.SaveChangesAsync();

            // 4. Actualizar el promedio en el perfil del DJ
            await RecalcularPromedioDj(reserva.DjId);

            return Ok(new { mensaje = "Valoración guardada correctamente" });
        }

        // GET: api/Valoraciones/dj/5
        [HttpGet("dj/{djId}")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ValoracionRespuestaDto>>> GetValoracionesPorDj(int djId)
        {
            var valoraciones = await _context.Valoraciones
                .Include(v => v.Cliente)
                .Where(v => v.DjId == djId)
                .OrderByDescending(v => v.FechaCreacion) 
                .ToListAsync();

            var resultado = valoraciones.Select(v => new ValoracionRespuestaDto
            {
                Id = v.Id,
                Puntuacion = v.Puntuacion,
                Comentario = v.Comentario,
                ClienteNombre = v.Cliente.Nombre,
                Fecha = v.FechaCreacion
            }).ToList();

            return Ok(resultado);
        }

        private async Task RecalcularPromedioDj(int djId)
        {
            var promedio = await _context.Valoraciones
                .Where(v => v.DjId == djId)
                .AverageAsync(v => (double)v.Puntuacion);

            var perfil = await _context.DjPerfiles
                .FirstOrDefaultAsync(p => p.UsuarioId == djId);

            if (perfil != null)
            {
                perfil.ValoracionPromedio = Math.Round(promedio, 1);
                await _context.SaveChangesAsync();
            }
        }
    }
}