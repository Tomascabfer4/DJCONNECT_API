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
                    FotoDj = r.Dj.FotoPerfil ?? "", 
                    FotoCliente = r.Cliente.FotoPerfil ?? "",
                    Lugar = r.UbicacionEvento,
                    Precio = r.PrecioAcordado,
                    Estado = r.Estado,
                    Horario = r.Horario
                }).ToListAsync();
        }

        // 2. CREAR RESERVA (Segura y sin auto-contratación)
        [HttpPost]
        public async Task<ActionResult> PostReserva(CrearReservaDto reservaDto)
        {
            try
            {
                var userIdStr = User.FindFirst("id")?.Value;
                if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
                var clienteId = int.Parse(userIdStr);

                // 🛡️ NUEVA VALIDACIÓN: Un DJ no puede reservarse a sí mismo
                if (reservaDto.DjId == clienteId)
                    return BadRequest("No puedes realizar una reserva para ti mismo.");

                // ✅ CORRECCIÓN POSTGRES Y ZONA HORARIA: 
                // Le decimos a C# que la fecha que entra (ej: 22:00) debe tratarse como UTC sin modificar su hora.
                // Esto soluciona el Error 500 en PostgreSQL y evita que te reste una hora.
                var fechaCorregida = DateTime.SpecifyKind(reservaDto.FechaEvento, DateTimeKind.Utc);

                // Validación: No reservar en el pasado (ahora comparamos con UtcNow para ser consistentes)
                if (fechaCorregida < DateTime.UtcNow)
                    return BadRequest("No puedes crear una reserva para una fecha pasada.");

                var dj = await _context.Usuarios
                    .Include(u => u.DjPerfil)
                    .FirstOrDefaultAsync(u => u.Id == reservaDto.DjId && u.TipoUsuario == "dj");

                if (dj == null) return BadRequest("El DJ seleccionado no existe o no es un perfil de DJ.");

                // 🛡️ VALIDACIÓN DE DISPONIBILIDAD
                bool ocupado = await _context.Reservas.AnyAsync(r =>
                    r.DjId == reservaDto.DjId &&
                    r.FechaEvento.Date == fechaCorregida.Date &&
                    r.Estado == "aceptada");

                if (ocupado)
                {
                    return BadRequest("El DJ ya tiene un compromiso confirmado para esta fecha.");
                }

                // 🛠️ CÁLCULO PROFESIONAL DEL PRECIO TOTAL
                // Sacamos el número de horas (que ahora envías como "2", "3" desde React)
                if (!int.TryParse(reservaDto.Horario, out int numHoras)) numHoras = 1;

                // Multiplicamos: PrecioPorHora * Número de horas
                decimal precioPorHora = dj.DjPerfil?.PrecioPorHora ?? 0;
                decimal precioTotal = precioPorHora * numHoras;

                var nuevaReserva = new Reserva
                {
                    DjId = reservaDto.DjId,
                    ClienteId = clienteId,
                    FechaEvento = fechaCorregida, // Guardamos la fecha corregida 
                    Horario = reservaDto.Horario,
                    TipoEvento = reservaDto.TipoEvento,
                    UbicacionEvento = reservaDto.UbicacionEvento,
                    PrecioAcordado = precioTotal, // Guardamos el Total (ej: 60€)
                    Estado = "pendiente"
                };

                _context.Reservas.Add(nuevaReserva);
                await _context.SaveChangesAsync();

                return Ok(new { mensaje = "Reserva enviada con éxito", id = nuevaReserva.Id, total = precioTotal });
            }
            catch (Exception ex)
            {
                // 🛑 Si PostgreSQL falla, ahora verás el error real en consola y no un error de CORS
                return StatusCode(500, new { mensaje = $"Error interno del servidor: {ex.InnerException?.Message ?? ex.Message}" });
            }
        }



        // 3. MODIFICAR RESERVA (Solo Cliente y si está pendiente)
        [HttpPut("{id}")]
        [Authorize(Roles = "client")] // Solo clientes pueden editar sus peticiones
        public async Task<IActionResult> UpdateReserva(int id, ReservaUpdateDto reservaDto)
        {
            // Usamos la misma lógica de extracción de ID que en tus otros métodos para evitar errores
            var userIdStr = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            int userId = int.Parse(userIdStr);

            var reserva = await _context.Reservas.FindAsync(id);

            if (reserva == null) return NotFound();

            // Seguridad: ¿Es tu reserva?
            if (reserva.ClienteId != userId) return Forbid();

            // Solo editable si no ha sido procesada
            if (reserva.Estado != "pendiente")
                return BadRequest("No puedes editar una reserva que ya ha sido aceptada o rechazada.");

            // ✅ SOLUCIÓN AL DESFASE: Eliminamos .ToUniversalTime() 
            // Ahora, si el cliente cambia la fecha a las 20:00, se guardarán las 20:00 exactas.
            reserva.FechaEvento = reservaDto.FechaEvento;

            reserva.UbicacionEvento = reservaDto.UbicacionEvento;
            reserva.TipoEvento = reservaDto.TipoEvento;

            await _context.SaveChangesAsync();
            return Ok(new { mensaje = "Reserva actualizada" });
        }

        // 4. CAMBIAR ESTADO (Solo DJ: Aceptar/Rechazar)
        [HttpPut("{id}/estado")]
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