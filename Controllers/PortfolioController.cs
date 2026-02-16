using Microsoft.AspNetCore.Mvc;
using API_DJCONNECT.Data;
using API_DJCONNECT.Modelos;
using API_DJCONNECT.Services;
using API_DJCONNECT.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore; // Necesario para ToListAsync
using CloudinaryDotNet.Actions; // Necesario para ResourceType

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

        // POST: api/Portfolio/upload
        [Authorize(Roles = "dj")]
        [HttpPost("upload")]
        // CAMBIO AQUÍ: Ahora recibimos un solo objeto "dto" que tiene todo dentro
        public async Task<ActionResult<DjPortfolioItem>> UploadItem([FromForm] PortfolioUploadDto dto)
        {
            var userId = int.Parse(User.FindFirst("id")?.Value);

            // Usamos dto.File en lugar de file
            if (dto.File == null || dto.File.Length == 0) return BadRequest("No has subido ningún archivo.");

            string url = "";
            string publicId = "";

            // Usamos dto.Tipo
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
                Titulo = dto.Titulo // Usamos dto.Titulo
            };

            _context.DjPortfolioItems.Add(item);
            await _context.SaveChangesAsync();

            return Ok(item);
        }

        // GET: api/Portfolio/5
        [HttpGet("{djId}")]
        public async Task<ActionResult<IEnumerable<DjPortfolioItem>>> GetPortfolio(int djId)
        {
            return await _context.DjPortfolioItems
                                 .Where(p => p.UsuarioId == djId)
                                 .OrderByDescending(p => p.FechaSubida)
                                 .ToListAsync();
        }

        // DELETE: api/Portfolio/12
        [Authorize(Roles = "dj")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteItem(int id)
        {
            var userId = int.Parse(User.FindFirst("id")?.Value);
            var item = await _context.DjPortfolioItems.FindAsync(id);

            if (item == null) return NotFound();
            if (item.UsuarioId != userId) return Forbid(); // Solo el dueño borra

            // 1. Borrar de Cloudinary
            var tipoRecurso = item.Tipo == "imagen" ? ResourceType.Image : ResourceType.Video;
            await _cloudinaryService.DeleteFileAsync(item.PublicId, tipoRecurso);

            // 2. Borrar de DB
            _context.DjPortfolioItems.Remove(item);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Elemento eliminado" });
        }
    }
}