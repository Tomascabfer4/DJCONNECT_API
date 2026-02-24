using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API_DJCONNECT.Data;
using API_DJCONNECT.Modelos;
using API_DJCONNECT.DTOs;
using API_DJCONNECT.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR; 

namespace API_DJCONNECT.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly DjConnectContext _context;
        private readonly IHubContext<ChatHub> _hubContext; 

        public ChatController(DjConnectContext context, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // ==========================================
        // Vista 'Chat.jsx' (Botón de Enviar o tecla Enter).
        // FLUJO: React envía el texto del mensaje y el ID de la reserva.
        // C# comprueba que no seas un intruso, guarda el mensaje en la base de datos para el historial,
        // y se usa SignalR (WebSockets) para disparar el mensaje en tiempo real a la pantalla de la otra persona.
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> EnviarMensaje(CrearMensajeDto dto)
        {
            var userId = int.Parse(User.FindFirst("id")?.Value);

            // Para cortar acceso a otros
            var reserva = await _context.Reservas
                .FirstOrDefaultAsync(r => r.Id == dto.ReservaId && (r.ClienteId == userId || r.DjId == userId));

            if (reserva == null) return Forbid("No tienes acceso a este chat.");

            // Guardar en la BBDD
            var mensaje = new Mensaje
            {
                ReservaId = dto.ReservaId,
                EmisorId = userId,
                Contenido = dto.Contenido,
                FechaEnvio = DateTime.UtcNow
            };
            _context.Mensajes.Add(mensaje);
            await _context.SaveChangesAsync();

            // Cargamos el nombre de quien envía para decírselo al frontend
            await _context.Entry(mensaje).Reference(m => m.Emisor).LoadAsync();

            // Emitir en vivo a todos los conectados a la "Sala" (Grupo) de esta reserva
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

        // ==========================================
        // Vista 'Chat.jsx' (Al entrar a la pantalla).
        // FLUJO: Antes de conectar los WebSockets, React necesita saber de qué hablabais ayer.
        // Este endpoint busca todos los mensajes antiguos de esa reserva, los ordena por fecha, 
        // y le dice a React cuáles son "Tuyos" (EsMio = true) para pintarlos a la derecha o a la izquierda.
        // ==========================================
        [HttpGet("{reservaId}")]
        public async Task<ActionResult> GetHistorial(int reservaId)
        {
            var userId = int.Parse(User.FindFirst("id")?.Value);
            
            var reserva = await _context.Reservas
                .FirstOrDefaultAsync(r => r.Id == reservaId && (r.ClienteId == userId || r.DjId == userId));

            if (reserva == null) return Forbid("No tienes acceso a este chat.");

            var mensajes = await _context.Mensajes
               .Where(m => m.ReservaId == reservaId)
               .OrderBy(m => m.FechaEnvio)
               .Select(m => new {
                   m.Id,
                   m.Contenido,
                   m.FechaEnvio,
                   EmisorNombre = m.Emisor.Nombre,
                   EsMio = m.EmisorId == userId // Para saber si es tuyo el mensaje
               })
               .ToListAsync();

            return Ok(mensajes);
        }
    }
}