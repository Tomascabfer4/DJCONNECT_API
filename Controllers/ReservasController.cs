using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API_DJCONNECT.Data;
using API_DJCONNECT.Modelos;
using API_DJCONNECT.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace API_DJCONNECT.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Solo usuarios logueados pueden acceder a cualquier método
    public class ReservasController : ControllerBase
    {
        private readonly DjConnectContext _context;

        public ReservasController(DjConnectContext context)
        {
            _context = context;
        }

        // 1. LISTAR MIS RESERVAS
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ReservaDto>>> GetReservas()
        {
            var userIdStr = User.FindFirst("id")?.Value;
            var rol = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;

            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            int userId = int.Parse(userIdStr);

            var query = _context.Reservas
                .Include(r => r.Dj)
                .Include(r => r.Cliente)
                .AsQueryable();

            if (rol == "dj")
                query = query.Where(r => r.DjId == userId && r.Estado != "cancelada");
            else
                query = query.Where(r => r.ClienteId == userId && r.Estado != "cancelada");

            // Ordenamos por fecha más cercana
            return await query
                .OrderBy(r => r.FechaEvento)
                .Select(r => new ReservaDto
                {
                    Id = r.Id,
                    Fecha = r.FechaEvento.ToString("dd/MM/yyyy HH:mm"),
                    NombreDj = r.Dj.Nombre,
                    NombreCliente = r.Cliente.Nombre,
                    Lugar = r.UbicacionEvento,
                    Precio = r.PrecioAcordado,
                    Estado = r.Estado
                }).ToListAsync();
        }

        // 2. CREAR RESERVA (Segura y sin auto-contratación)
        [HttpPost]
        public async Task<ActionResult> PostReserva(CrearReservaDto reservaDto)
        {
            var userIdStr = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            var clienteId = int.Parse(userIdStr);

            // 🛡️ NUEVA VALIDACIÓN: Un DJ no puede reservarse a sí mismo
            if (reservaDto.DjId == clienteId)
                return BadRequest("No puedes realizar una reserva para ti mismo.");

            // Validación: No reservar en el pasado
            if (reservaDto.FechaEvento < DateTime.Now)
                return BadRequest("No puedes crear una reserva para una fecha pasada.");

            var dj = await _context.Usuarios
                .Include(u => u.DjPerfil)
                .FirstOrDefaultAsync(u => u.Id == reservaDto.DjId && u.TipoUsuario == "dj");

            if (dj == null) return BadRequest("El DJ seleccionado no existe o no es un perfil de DJ.");

            // 🛡️ VALIDACIÓN DE DISPONIBILIDAD
            // No permitimos reservar si el DJ ya tiene algo ACEPTADO ese día
            bool ocupado = await _context.Reservas.AnyAsync(r =>
                r.DjId == reservaDto.DjId &&
                r.FechaEvento.Date == reservaDto.FechaEvento.Date &&
                r.Estado == "aceptada");

            if (ocupado)
            {
                return BadRequest("El DJ ya tiene un compromiso confirmado para esta fecha.");
            }

            var nuevaReserva = new Reserva
            {
                DjId = reservaDto.DjId,
                ClienteId = clienteId,
                FechaEvento = reservaDto.FechaEvento.ToUniversalTime(),
                Horario = reservaDto.Horario,
                TipoEvento = reservaDto.TipoEvento,
                UbicacionEvento = reservaDto.UbicacionEvento,
                PrecioAcordado = dj.DjPerfil?.PrecioPorHora ?? 0,
                Estado = "pendiente"
            };

            _context.Reservas.Add(nuevaReserva);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Reserva enviada con éxito", id = nuevaReserva.Id });
        }

        // 3. MODIFICAR RESERVA (Solo Cliente y si está pendiente)
        [HttpPut("{id}")]
        [Authorize(Roles = "client")] // Solo clientes pueden editar sus peticiones
        public async Task<IActionResult> UpdateReserva(int id, ReservaUpdateDto reservaDto)
        {
            var userId = int.Parse(User.FindFirst("id")?.Value);
            var reserva = await _context.Reservas.FindAsync(id);

            if (reserva == null) return NotFound();

            // Seguridad: ¿Es tu reserva?
            if (reserva.ClienteId != userId) return Forbid();

            // Solo editable si no ha sido procesada
            if (reserva.Estado != "pendiente")
                return BadRequest("No puedes editar una reserva que ya ha sido aceptada o rechazada.");

            reserva.FechaEvento = reservaDto.FechaEvento.ToUniversalTime();
            reserva.UbicacionEvento = reservaDto.UbicacionEvento;
            reserva.TipoEvento = reservaDto.TipoEvento;

            await _context.SaveChangesAsync();
            return Ok(new { mensaje = "Reserva actualizada" });
        }

        // 4. CAMBIAR ESTADO (Solo DJ: Aceptar/Rechazar)
        [HttpPatch("{id}/estado")]
        // Quitamos el Role="dj" estricto aquí para manejarlo manualmente si el token usa "role" en vez de "Role"
        public async Task<IActionResult> CambiarEstado(int id, [FromBody] string nuevoEstado)
        {
            var userIdStr = User.FindFirst("id")?.Value;
            var rol = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;

            if (rol != "dj") return Forbid("Solo los DJs pueden aceptar o rechazar reservas.");

            var reserva = await _context.Reservas.FindAsync(id);
            if (reserva == null) return NotFound();

            if (reserva.DjId != int.Parse(userIdStr))
                return Forbid("No tienes permiso sobre esta reserva.");

            var estadoLimpiado = nuevoEstado.ToLower().Trim();
            var estadosValidos = new[] { "aceptada", "rechazada" };

            if (!estadosValidos.Contains(estadoLimpiado))
                return BadRequest("Estado no válido. Usa: 'aceptada' o 'rechazada'.");

            reserva.Estado = estadoLimpiado;
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = $"La reserva ahora está: {reserva.Estado}" });
        }

        // 5. ELIMINAR / CANCELAR RESERVA
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReserva(int id)
        {
            // 1. Obtener ID del usuario que hace la petición
            var userId = int.Parse(User.FindFirst("id")?.Value);

            // 2. Buscar la reserva
            var reserva = await _context.Reservas.FindAsync(id);

            if (reserva == null) return NotFound();

            // 3. Seguridad: Solo el Cliente o el DJ implicados pueden cancelar
            if (reserva.ClienteId != userId && reserva.DjId != userId)
                return Forbid("No tienes permiso para cancelar esta reserva.");

            // 4. Lógica de negocio: Cambiar estado en lugar de borrar
            reserva.Estado = "cancelada";

            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "La reserva ha sido cancelada correctamente.", estado = reserva.Estado });
        }
    }
}