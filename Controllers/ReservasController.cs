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
    [Authorize] // Todo aquí requiere estar logueado
    public class ReservasController : ControllerBase
    {
        private readonly DjConnectContext _context;

        public ReservasController(DjConnectContext context)
        {
            _context = context;
        }

        // ==========================================
        // Vistas 'MyReservations.jsx' y 'Chats.jsx'.
        // FLUJO: React llama a este endpoint al cargar esas páginas. C# lee el token del usuario,
        // deduce si es DJ o Cliente, y le devuelve una lista de tarjetas (ReservaDto) que incluyen
        // la foto y el nombre de la "otra persona" para pintar la bandeja de entrada y los tickets.
        // ==========================================
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

            // Filtramos dependiendo de quién pregunta (Un DJ no ve las reservas de otros DJs)
            if (rol == "dj")
                query = query.Where(r => r.DjId == userId && r.Estado != "cancelada");
            else
                query = query.Where(r => r.ClienteId == userId && r.Estado != "cancelada");

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

        // ==========================================
        // Vista 'DJDetail.jsx' (Botón "Solicitar Reserva").
        // FLUJO: El cliente selecciona fecha, horas y lugar. React lo manda en un 'CrearReservaDto'.
        // C# comprueba que el DJ no esté ocupado ese día, calcula matemáticamente el precio final (horas * tarifa)
        // y guarda el contrato en estado "Pendiente".
        // ==========================================
        [HttpPost]
        public async Task<ActionResult> PostReserva(CrearReservaDto reservaDto)
        {
            try
            {
                var userIdStr = User.FindFirst("id")?.Value;
                if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
                var clienteId = int.Parse(userIdStr);

                if (reservaDto.DjId == clienteId)
                    return BadRequest("No puedes realizar una reserva para ti mismo.");

                var fechaCorregida = DateTime.SpecifyKind(reservaDto.FechaEvento, DateTimeKind.Utc);

                if (fechaCorregida < DateTime.UtcNow)
                    return BadRequest("No puedes crear una reserva para una fecha pasada.");

                var dj = await _context.Usuarios
                    .Include(u => u.DjPerfil)
                    .FirstOrDefaultAsync(u => u.Id == reservaDto.DjId && u.TipoUsuario == "dj");

                if (dj == null) return BadRequest("El DJ seleccionado no existe.");

                // El DJ no puede aceptar dos bolos el mismo día
                bool ocupado = await _context.Reservas.AnyAsync(r =>
                    r.DjId == reservaDto.DjId &&
                    r.FechaEvento.Date == fechaCorregida.Date &&
                    r.Estado == "aceptada");

                if (ocupado) return BadRequest("El DJ ya tiene un compromiso confirmado para esta fecha.");

                // Cálculo automático del presupuesto
                if (!int.TryParse(reservaDto.Horario, out int numHoras)) numHoras = 1;
                decimal precioPorHora = dj.DjPerfil?.PrecioPorHora ?? 0;
                decimal precioTotal = precioPorHora * numHoras;

                var nuevaReserva = new Reserva
                {
                    DjId = reservaDto.DjId,
                    ClienteId = clienteId,
                    FechaEvento = fechaCorregida,
                    Horario = reservaDto.Horario,
                    TipoEvento = reservaDto.TipoEvento,
                    UbicacionEvento = reservaDto.UbicacionEvento,
                    PrecioAcordado = precioTotal,
                    Estado = "pendiente" // El DJ decide si aceptar o no el evento
                };

                _context.Reservas.Add(nuevaReserva);
                await _context.SaveChangesAsync();

                return Ok(new { mensaje = "Reserva enviada con éxito", id = nuevaReserva.Id, total = precioTotal });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = $"Error interno: {ex.InnerException?.Message ?? ex.Message}" });
            }
        }

        // ==========================================
        // ESTO AUN NO SE USA, SE DEJA EL ENDPOINT PARA UN FUTURO
        // FLUJO: Permite a un Cliente editar los detalles del evento (ej: cambió el local) 
        // siempre y cuando el DJ aún no haya aceptado el contrato ("Pendiente").
        // ==========================================
        [HttpPut("{id}")]
        [Authorize(Roles = "client")]
        public async Task<IActionResult> UpdateReserva(int id, ReservaUpdateDto reservaDto)
        {
            var userIdStr = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            int userId = int.Parse(userIdStr);

            var reserva = await _context.Reservas.FindAsync(id);
            if (reserva == null) return NotFound();

            if (reserva.ClienteId != userId) return Forbid();
            if (reserva.Estado != "pendiente") return BadRequest("No puedes editar una reserva ya procesada.");

            reserva.FechaEvento = reservaDto.FechaEvento;
            reserva.UbicacionEvento = reservaDto.UbicacionEvento;
            reserva.TipoEvento = reservaDto.TipoEvento;

            await _context.SaveChangesAsync();
            return Ok(new { mensaje = "Reserva actualizada" });
        }

        // ==========================================
        // Vista 'MyReservations.jsx' (Botones verdes y rojos del DJ).
        // FLUJO: Cuando el DJ pulsa "Aceptar" o "Rechazar", React manda ese texto (string).
        // C# cambia el estado en la base de datos, lo que hace que desaparezcan los botones en React
        // y se actualice el Dashboard del DJ para sumar el dinero.
        // ==========================================
        [HttpPut("{id}/estado")]
        public async Task<IActionResult> CambiarEstado(int id, [FromBody] string nuevoEstado)
        {
            var userIdStr = User.FindFirst("id")?.Value;
            var rol = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;

            if (rol != "dj") return Forbid("Solo los DJs pueden aceptar o rechazar reservas.");

            var reserva = await _context.Reservas.FindAsync(id);
            if (reserva == null) return NotFound();

            if (reserva.DjId != int.Parse(userIdStr)) return Forbid("No tienes permiso sobre esta reserva.");

            var estadoLimpiado = nuevoEstado.ToLower().Trim();
            var estadosValidos = new[] { "aceptada", "rechazada" };

            if (!estadosValidos.Contains(estadoLimpiado))
                return BadRequest("Estado no válido. Usa: 'aceptada' o 'rechazada'.");

            reserva.Estado = estadoLimpiado;
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = $"La reserva ahora está: {reserva.Estado}" });
        }

        // ==========================================
        // ESTO AUN NO SE USA, SE DEJA EL ENDPOINT PARA UN FUTURO
        // FLUJO: Un botón de "Cancelar evento". En lugar de borrar la fila de la DB (lo que rompería
        // facturas y chats antiguos), lo marcamos como "cancelada" (Soft Delete).
        // ==========================================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReserva(int id)
        {
            var userId = int.Parse(User.FindFirst("id")?.Value);
            var reserva = await _context.Reservas.FindAsync(id);

            if (reserva == null) return NotFound();

            if (reserva.ClienteId != userId && reserva.DjId != userId)
                return Forbid("No tienes permiso para cancelar esta reserva.");

            reserva.Estado = "cancelada";
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "La reserva ha sido cancelada correctamente.", estado = reserva.Estado });
        }
    }
}