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

        // ==========================================
        // Vista 'MyReservations.jsx' (Botón Amarillo de "Valorar").
        // FLUJO: El cliente manda las estrellas y el texto. El backend verifica que el evento
        // haya sido "aceptado" o "finalizado" y que no se haya valorado ya. Luego llama a la función
        // privada para recalcular la nota media global del DJ.
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> PostValoracion(CrearValoracionDto dto)
        {
            try
            {
                var userIdStr = User.FindFirst("id")?.Value;
                if (string.IsNullOrEmpty(userIdStr))
                    return Unauthorized(new { mensaje = "Usuario no autorizado o token inválido." });

                var clienteId = int.Parse(userIdStr);

                var reserva = await _context.Reservas
                    .FirstOrDefaultAsync(r => r.Id == dto.ReservaId && r.ClienteId == clienteId);

                if (reserva == null)
                    return NotFound(new { mensaje = "Reserva no encontrada o no te pertenece." });

                // Solo puedes valorar si has reservado
                if (reserva.Estado != "aceptada" && reserva.Estado != "finalizada")
                    return BadRequest(new { mensaje = "Solo puedes valorar reservas aceptadas o finalizadas." });

                // 1 Reserva = 1 Valoración
                var existe = await _context.Valoraciones.AnyAsync(v => v.ReservaId == dto.ReservaId);
                if (existe)
                    return BadRequest(new { mensaje = "Esta reserva ya ha sido valorada." });

                var valoracion = new Valoracion
                {
                    ReservaId = dto.ReservaId,
                    ClienteId = clienteId,
                    DjId = reserva.DjId,
                    Puntuacion = Math.Clamp(dto.Puntuacion, 1, 5),
                    Comentario = dto.Comentario
                };

                _context.Valoraciones.Add(valoracion);
                await _context.SaveChangesAsync();

                // Se llama a la funcion para hacer la media
                await RecalcularPromedioDj(reserva.DjId);

                return Ok(new { mensaje = "Valoración guardada correctamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = $"Error interno: {ex.InnerException?.Message ?? ex.Message}" });
            }
        }

        // ==========================================
        // Vista 'DJDetail.jsx' (Sección de Opiniones).
        // FLUJO: Cuando cargas el perfil de un DJ, React pide todas las reseñas (texto y estrellas)
        // para pintarlas en formato de lista bajo la biografía.
        // ==========================================
        [HttpGet("dj/{djId}")]
        [AllowAnonymous] // Cualquiera puede leer las valoraciones
        public async Task<ActionResult<IEnumerable<ValoracionRespuestaDto>>> GetValoracionesPorDj(int djId)
        {
            var valoraciones = await _context.Valoraciones
                .Include(v => v.Cliente)
                .Where(v => v.DjId == djId)
                .OrderByDescending(v => v.FechaCreacion) // Las más recientes primero
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

        // ==========================================
        // ESTO NO ES UN ENDPOINT SOLO SE USA PARA CALCULAR LA MEDIA DE VALORACIONES
        // FLUJO: Coge todas las valoraciones de un DJ, saca la media matemática y la guarda 
        // en el campo 'ValoracionPromedio' del perfil del DJ. Así el Dashboard la lee al instante.
        // ==========================================
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