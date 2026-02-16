using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API_DJCONNECT.Data;
using API_DJCONNECT.Modelos;
using API_DJCONNECT.DTOs;
using API_DJCONNECT.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR; // Importante para IHubContext

namespace API_DJCONNECT.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly DjConnectContext _context;
        private readonly IHubContext<ChatHub> _hubContext; // Inyectamos el Hub

        public ChatController(DjConnectContext context, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpPost]
        public async Task<IActionResult> EnviarMensaje(CrearMensajeDto dto)
        {
            var userId = int.Parse(User.FindFirst("id")?.Value);

            // 1. Seguridad: ¿Participas en esta reserva?
            var reserva = await _context.Reservas
                .FirstOrDefaultAsync(r => r.Id == dto.ReservaId && (r.ClienteId == userId || r.DjId == userId));

            if (reserva == null) return Forbid("No tienes acceso a este chat.");

            // 2. Guardar en Base de Datos
            var mensaje = new Mensaje
            {
                ReservaId = dto.ReservaId,
                EmisorId = userId,
                Contenido = dto.Contenido,
                FechaEnvio = DateTime.UtcNow
            };
            _context.Mensajes.Add(mensaje);
            await _context.SaveChangesAsync();

            // 3. Cargar nombre del usuario (para mostrarlo en el chat)
            await _context.Entry(mensaje).Reference(m => m.Emisor).LoadAsync();

            // 4. SIGNALR: Enviar evento a la sala específica
            await _hubContext.Clients.Group(dto.ReservaId.ToString())
                .SendAsync("RecibirMensaje", new
                {
                    mensaje.Id,
                    mensaje.Contenido,
                    mensaje.FechaEnvio,
                    EmisorNombre = mensaje.Emisor.Nombre,
                    EmisorId = mensaje.EmisorId
                });

            return Ok(mensaje);
        }

        [HttpGet("{reservaId}")]
        public async Task<ActionResult> GetHistorial(int reservaId)
        {
            var userId = int.Parse(User.FindFirst("id")?.Value);
            // ... (Lógica de validación igual que arriba) ...

            var mensajes = await _context.Mensajes
               .Where(m => m.ReservaId == reservaId)
               .OrderBy(m => m.FechaEnvio)
               .Select(m => new {
                   m.Id,
                   m.Contenido,
                   m.FechaEnvio,
                   EmisorNombre = m.Emisor.Nombre,
                   EsMio = m.EmisorId == userId
               })
               .ToListAsync();

            return Ok(mensajes);
        }
    }
}