using Microsoft.AspNetCore.Mvc;
using API_DJCONNECT.Data;
using API_DJCONNECT.Modelos;
using API_DJCONNECT.Services;
using API_DJCONNECT.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CloudinaryDotNet.Actions;

namespace API_DJCONNECT.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PortfolioController : ControllerBase
    {
        private readonly DjConnectContext _context;
        private readonly CloudinaryService _cloudinaryService;

        public PortfolioController(DjConnectContext context, CloudinaryService cloudinaryService)
        {
            _context = context;
            _cloudinaryService = cloudinaryService;
        }

        // ==========================================
        // Vista 'MiPerfil' (Para el DJ).
        // FLUJO: El DJ sube un archivo (Foto/Vídeo/Audio). Se envía en un FormData (PortfolioUploadDto).
        // C# lo sube a la nube de Cloudinary, Cloudinary devuelve una URL pública segura, 
        // y C# guarda esa URL en la base de datos atada al perfil del DJ.
        // ==========================================
        [Authorize(Roles = "dj")]
        [HttpPost("upload")]
        public async Task<ActionResult<DjPortfolioItem>> UploadItem([FromForm] PortfolioUploadDto dto)
        {
            var userId = int.Parse(User.FindFirst("id")?.Value);

            if (dto.File == null || dto.File.Length == 0)
                return BadRequest("No has subido ningún archivo.");

            string url = "";
            string publicId = "";

            if (dto.Tipo.ToLower() == "imagen")
            {
                var result = await _cloudinaryService.UploadImageAsync(dto.File);
                if (result.Error != null) return BadRequest(result.Error.Message);
                url = result.SecureUrl.AbsoluteUri;
                publicId = result.PublicId;
            }
            else if (dto.Tipo.ToLower() == "video" || dto.Tipo.ToLower() == "musica")
            {
                var result = await _cloudinaryService.UploadVideoOrAudioAsync(dto.File);
                if (result.Error != null) return BadRequest(result.Error.Message);
                url = result.SecureUrl.AbsoluteUri;
                publicId = result.PublicId;
            }
            else
            {
                return BadRequest("Tipo no válido. Usa: imagen, video o musica.");
            }

            var item = new DjPortfolioItem
            {
                UsuarioId = userId,
                Tipo = dto.Tipo.ToLower(),
                Url = url,
                PublicId = publicId,
                Titulo = dto.Titulo
            };

            _context.DjPortfolioItems.Add(item);
            await _context.SaveChangesAsync();

            return Ok(item);
        }

        // ==========================================
        // Vistas 'DJDetail.jsx' y 'MiPerfil.jsx'.
        // FLUJO: Obtiene la galería completa de un DJ (fotos, vídeos, etc) para mostrarlos
        // al cliente como "Escaparate" o al propio DJ para que gestione sus archivos.
        // ==========================================
        [HttpGet("{djId}")]
        [AllowAnonymous] // El portfolio es público
        public async Task<ActionResult<IEnumerable<DjPortfolioItem>>> GetPortfolio(int djId)
        {
            return await _context.DjPortfolioItems
                                 .Where(p => p.UsuarioId == djId)
                                 .OrderByDescending(p => p.FechaSubida)
                                 .ToListAsync();
        }

        // ==========================================
        // Vista 'MiPerfil' (Botón de la Papelera en una foto/video).
        // FLUJO: El DJ decide borrar un archivo. El Backend primero pide a Cloudinary
        // que destruya el archivo físico (para no pagar almacenamiento fantasma), y luego 
        // borra el registro en la base de datos.
        // ==========================================
        [Authorize(Roles = "dj")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteItem(int id)
        {
            var userId = int.Parse(User.FindFirst("id")?.Value);
            var item = await _context.DjPortfolioItems.FindAsync(id);

            if (item == null) return NotFound();
            if (item.UsuarioId != userId) return Forbid(); // Seguridad: Solo el dueño lo puede borrar

            // Borrar de Cloudinary
            var tipoRecurso = item.Tipo == "imagen" ? ResourceType.Image : ResourceType.Video;
            await _cloudinaryService.DeleteFileAsync(item.PublicId, tipoRecurso);

            // Borrar de DB
            _context.DjPortfolioItems.Remove(item);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Elemento eliminado" });
        }
    }
}