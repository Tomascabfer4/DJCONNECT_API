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
        // CATÁLOGO PÚBLICO DE DJS (SOLO LECTURA)
        // ==========================================

        // 1. OBTENER TODOS LOS DJS (Catálogo principal)
        // GET: api/DJs
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DjPublicoDto>>> GetDJs()
        {
            var djs = await _context.Usuarios
                .Where(u => u.TipoUsuario == "dj" && u.Activo == true) // Solo queremos DJs
                .Include(u => u.DjPerfil)          // Traemos su perfil extendido
                .Select(usuario => new DjPublicoDto
                {
                    // Mapeamos a DTO para no enseñar datos privados
                    NombreArtistico = usuario.DjPerfil.NombreArtistico ?? usuario.Nombre,
                    Ciudad = usuario.Ubicacion ?? "Mundo",
                    Foto = usuario.FotoPerfil,
                    GenerosMusicales = usuario.DjPerfil.Generos ?? "Varios",
                    Precio = usuario.DjPerfil.PrecioPorHora
                })
                .ToListAsync();

            return Ok(djs);
        }

        // 2. OBTENER UN DJ POR ID (Ficha de detalle COMPLETA)
        // GET: api/DJs/5
        [HttpGet("{id}")]
        [AllowAnonymous] // Importante: Cualquiera debe poder ver el perfil
        public async Task<ActionResult<DjDetalleDto>> GetDJ(int id)
        {
            // Buscamos el usuario DJ por su ID
            var usuario = await _context.Usuarios
                .Include(u => u.DjPerfil)
                .FirstOrDefaultAsync(u => u.Id == id && u.TipoUsuario == "dj" && u.Activo == true);

            if (usuario == null)
            {
                return NotFound("DJ no encontrado");
            }

            // Calculamos el número de valoraciones reales si existe la tabla
            int numValoraciones = await _context.Valoraciones.CountAsync(v => v.DjId == usuario.Id);

            // Mapeamos al DTO completo (DjDetalleDto)
            var djDto = new DjDetalleDto
            {
                Id = usuario.Id,
                NombreArtistico = usuario.DjPerfil.NombreArtistico ?? usuario.Nombre,
                Bio = usuario.DjPerfil.Bio, // <--- Dato clave para el detalle
                GenerosMusicales = usuario.DjPerfil.Generos ?? "Varios",
                Precio = usuario.DjPerfil.PrecioPorHora,
                AniosExperiencia = usuario.DjPerfil.AniosExperiencia, // <--- Dato clave
                ValoracionPromedio = (double)usuario.DjPerfil.ValoracionPromedio,
                NumeroValoraciones = numValoraciones,
                Ciudad = usuario.Ubicacion ?? "No especificada",
                Foto = usuario.FotoPerfil
            };

            return Ok(djDto);
        }

        // 3. BUSCAR POR GÉNERO
        // GET: api/DJs/genero/Techno
        //[HttpGet("genero/{genero}")]
        //public async Task<ActionResult<IEnumerable<DjPublicoDto>>> GetDJsByGenre(string genero)
        //{
        //    // Nota: ToLower() ayuda a que la búsqueda no sea sensible a mayúsculas
        //    var djs = await _context.Usuarios
        //        .Where(u => u.TipoUsuario == "dj" &&
        //                    u.DjPerfil.Generos.ToLower().Contains(genero.ToLower()))
        //        .Include(u => u.DjPerfil)
        //        .Select(usuario => new DjPublicoDto
        //        {
        //            NombreArtistico = usuario.DjPerfil.NombreArtistico ?? usuario.Nombre,
        //            Ciudad = usuario.Ubicacion ?? "Mundo",
        //            Foto = usuario.FotoPerfil,
        //            GenerosMusicales = usuario.DjPerfil.Generos,
        //            Precio = usuario.DjPerfil.PrecioPorHora
        //        })
        //        .ToListAsync();

        //    return Ok(djs);
        //}

        // 4. BUSCAR POR PRECIO MÁXIMO
        // GET: api/DJs/precio/100
        //[HttpGet("precio/{maxPrecio}")]
        //public async Task<ActionResult<IEnumerable<DjPublicoDto>>> GetDJsByMaxPrice(decimal maxPrecio)
        //{
        //    var djs = await _context.Usuarios
        //        .Where(u => u.TipoUsuario == "dj" && u.DjPerfil.PrecioPorHora <= maxPrecio)
        //        .Include(u => u.DjPerfil)
        //        .Select(usuario => new DjPublicoDto
        //        {
        //            NombreArtistico = usuario.DjPerfil.NombreArtistico ?? usuario.Nombre,
        //            Ciudad = usuario.Ubicacion ?? "Mundo",
        //            Foto = usuario.FotoPerfil,
        //            GenerosMusicales = usuario.DjPerfil.Generos,
        //            Precio = usuario.DjPerfil.PrecioPorHora
        //        })
        //        .ToListAsync();

        //    return Ok(djs);
        //}

        [Authorize(Roles = "dj")] // Solo los que tengan rol 'dj' en el token pueden entrar
        [HttpPut("perfil")]
        public async Task<IActionResult> UpdatePerfil(UpdateDjPerfilDto perfilDto)
        {
            // 1. Extraer el ID del DJ desde el Token
            var userId = int.Parse(User.FindFirst("id")?.Value);

            // 2. Buscar su perfil en la base de datos
            var perfil = await _context.DjPerfiles.FirstOrDefaultAsync(p => p.UsuarioId == userId);

            if (perfil == null) return NotFound("No se encontró el perfil del DJ.");

            // 3. Actualizar los campos
            perfil.NombreArtistico = perfilDto.NombreArtistico;
            perfil.Bio = perfilDto.Bio;
            perfil.Generos = perfilDto.Generos;
            perfil.PrecioPorHora = perfilDto.PrecioPorHora;
            perfil.AniosExperiencia = perfilDto.AniosExperiencia;

            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Perfil actualizado correctamente" });
        }

        [HttpGet("buscar")]
        public async Task<ActionResult<IEnumerable<object>>> BuscarDJs(
        [FromQuery] string? genero,
        [FromQuery] decimal? precioMax,
        [FromQuery] string? ubicacion)
        {
            // 1. Empezamos con la consulta base incluyendo el perfil
            var query = _context.Usuarios
                .Include(u => u.DjPerfil)
                .Where(u => u.TipoUsuario == "dj" && u.Activo == true)
                .AsQueryable();

            // 2. Aplicamos filtros solo si el usuario los envía
            if (!string.IsNullOrEmpty(genero))
            {
                query = query.Where(u => u.DjPerfil.Generos.ToLower().Contains(genero.ToLower()));
            }

            if (precioMax.HasValue)
            {
                query = query.Where(u => u.DjPerfil.PrecioPorHora <= precioMax.Value);
            }

            if (!string.IsNullOrEmpty(ubicacion))
            {
                query = query.Where(u => u.Ubicacion.ToLower().Contains(ubicacion.ToLower()));
            }

            // 3. Proyectamos a un objeto limpio para el Frontend
            var resultados = await query.Select(u => new
            {
                u.Id,
                u.Nombre,
                NombreArtistico = u.DjPerfil.NombreArtistico,
                u.FotoPerfil,
                u.Ubicacion,
                u.DjPerfil.Generos,
                u.DjPerfil.PrecioPorHora,
                u.DjPerfil.ValoracionPromedio
            }).ToListAsync();

            return Ok(resultados);
        }
    }
}