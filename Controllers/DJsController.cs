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
    public class DJsController : ControllerBase
    {
        private readonly DjConnectContext _context;

        public DJsController(DjConnectContext context)
        {
            _context = context;
        }

        // ==========================================
        // FLUJO: Es un endpoint de respaldo útil para obtener el catálogo completo de DJs 
        // sin aplicar ningún filtro. Ideal para futuras implementaciones como un "Top DJs".
        // ==========================================
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DjPublicoDto>>> GetDJs()
        {
            var djs = await _context.Usuarios
                .Where(u => u.TipoUsuario == "dj" && u.Activo == true)
                .Include(u => u.DjPerfil)
                .Select(usuario => new DjPublicoDto
                {
                    Id = usuario.Id,
                    NombreArtistico = usuario.DjPerfil.NombreArtistico ?? usuario.Nombre,
                    Ciudad = usuario.Ubicacion ?? "Mundo",
                    Foto = usuario.FotoPerfil,
                    GenerosMusicales = usuario.DjPerfil.Generos ?? "Varios",
                    Precio = usuario.DjPerfil.PrecioPorHora
                })
                .ToListAsync();

            return Ok(djs);
        }

        // ==========================================
        // Vista 'DJDetail.jsx' o el Modal del Perfil del DJ.
        // FLUJO: Cuando el cliente hace clic en la tarjeta de un DJ, React manda el 'id' a este endpoint.
        // C# recoge la bio, la nota media, la experiencia y las fotos, y se lo devuelve a React 
        // empaquetado en un 'DjDetalleDto' para pintar la pantalla completa.
        // ==========================================
        [HttpGet("{id}")]
        [AllowAnonymous] // Importante: Cualquiera (incluso sin login) puede ver un perfil público
        public async Task<ActionResult<DjDetalleDto>> GetDJ(int id)
        {
            var usuario = await _context.Usuarios
                .Include(u => u.DjPerfil)
                .FirstOrDefaultAsync(u => u.Id == id && u.TipoUsuario == "dj" && u.Activo == true);

            if (usuario == null) return NotFound("DJ no encontrado");

            // Calculamos cuántas valoraciones tiene en total para mostrar el número junto a las estrellas
            int numValoraciones = await _context.Valoraciones.CountAsync(v => v.DjId == usuario.Id);

            var djDto = new DjDetalleDto
            {
                Id = usuario.Id,
                NombreArtistico = usuario.DjPerfil.NombreArtistico ?? usuario.Nombre,
                Bio = usuario.DjPerfil.Bio,
                GenerosMusicales = usuario.DjPerfil.Generos ?? "Varios",
                Precio = usuario.DjPerfil.PrecioPorHora,
                AniosExperiencia = usuario.DjPerfil.AniosExperiencia,
                ValoracionPromedio = (double)usuario.DjPerfil.ValoracionPromedio,
                NumeroValoraciones = numValoraciones,
                Ciudad = usuario.Ubicacion ?? "No especificada",
                Foto = usuario.FotoPerfil
            };

            return Ok(djDto);
        }

        // ==========================================
        // Pestaña 'Configuración' (Solo vista DJ).
        // FLUJO: El DJ rellena el formulario editando su tarifa, géneros, etc. React manda un JSON 
        // con la estructura 'UpdateDjPerfilDto'. C# busca al DJ usando el Token de seguridad y guarda los cambios.
        // ==========================================
        [Authorize(Roles = "dj")]
        [HttpPut("perfil")]
        public async Task<IActionResult> UpdatePerfil(UpdateDjPerfilDto perfilDto)
        {
            var userId = int.Parse(User.FindFirst("id")?.Value);
            var perfil = await _context.DjPerfiles.FirstOrDefaultAsync(p => p.UsuarioId == userId);

            if (perfil == null) return NotFound("No se encontró el perfil del DJ.");

            perfil.NombreArtistico = perfilDto.NombreArtistico;
            perfil.Bio = perfilDto.Bio;
            perfil.Generos = perfilDto.Generos;
            perfil.PrecioPorHora = perfilDto.PrecioPorHora;
            perfil.AniosExperiencia = perfilDto.AniosExperiencia;

            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Perfil actualizado correctamente" });
        }

        // ==========================================
        // Vista 'ClientDashboard.jsx' (El Buscador principal).
        // El hook 'useDebounce' de React envía peticiones aquí cada vez que el cliente 
        // teclea en los inputs de ciudad, precio o género. C# filtra la base de datos de forma dinámica
        // y devuelve el array para que React pinte las tarjetas de los DJs (Componente 'DJCard').
        // ==========================================
        [HttpGet("buscar")]
        public async Task<ActionResult<IEnumerable<object>>> BuscarDJs(
        [FromQuery] string? nombre,
        [FromQuery] string? genero,
        [FromQuery] decimal? precioMax,
        [FromQuery] string? ubicacion)
        {
            var query = _context.Usuarios
                .Include(u => u.DjPerfil)
                .Where(u => u.TipoUsuario == "dj" && u.Activo == true)
                .AsQueryable();

            if (!string.IsNullOrEmpty(nombre))
                query = query.Where(u => u.DjPerfil.NombreArtistico.ToLower().Contains(nombre.ToLower())
                                      || u.Nombre.ToLower().Contains(nombre.ToLower()));

            if (!string.IsNullOrEmpty(genero))
                query = query.Where(u => u.DjPerfil.Generos.ToLower().Contains(genero.ToLower()));

            if (precioMax.HasValue)
                query = query.Where(u => u.DjPerfil.PrecioPorHora <= precioMax.Value);

            if (!string.IsNullOrEmpty(ubicacion))
                query = query.Where(u => u.Ubicacion.ToLower().Contains(ubicacion.ToLower()));

            var resultados = await query.Select(u => new
            {
                u.Id,
                u.Nombre,
                NombreArtistico = u.DjPerfil.NombreArtistico,
                Foto = u.FotoPerfil,
                Ciudad = u.Ubicacion,
                Ubicacion = u.Ubicacion,
                Generos = u.DjPerfil.Generos,
                GenerosMusicales = u.DjPerfil.Generos,
                Precio = u.DjPerfil.PrecioPorHora,
                PrecioPorHora = u.DjPerfil.PrecioPorHora,
                ValoracionPromedio = u.DjPerfil.ValoracionPromedio
            }).ToListAsync();

            return Ok(resultados);
        }
    }
}