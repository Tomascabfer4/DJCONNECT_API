using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API_DJCONNECT.Data;
using API_DJCONNECT.Modelos;
using API_DJCONNECT.DTOs;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using API_DJCONNECT.Services;
using CloudinaryDotNet;

namespace API_DJCONNECT.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsuariosController : ControllerBase
    {
        private readonly DjConnectContext _context;
        private readonly IConfiguration _configuration;
        private readonly CloudinaryService _cloudinaryService;

        public UsuariosController(DjConnectContext context, IConfiguration configuration, CloudinaryService cloudinaryService)
        {
            _context = context;
            _configuration = configuration;
            _cloudinaryService = cloudinaryService;
        }

        // ==========================================
        // 'Register.jsx' (Pestaña Cliente).
        // FLUJO: Recibe los datos planos, encripta la contraseña con BCrypt (por seguridad)
        // y guarda al usuario en la tabla Usuarios con el rol "client".
        // ==========================================
        [HttpPost("registro/cliente")]
        public async Task<ActionResult<UsuarioPerfilDto>> RegistrarCliente(RegistroDto registroDto)
        {
            if (await _context.Usuarios.AnyAsync(u => u.Email == registroDto.Email))
                return BadRequest("El email ya existe.");

            var usuario = new Usuario
            {
                Nombre = registroDto.Nombre,
                Email = registroDto.Email,
                Telefono = registroDto.Telefono,
                Ubicacion = registroDto.Ubicacion,
                TipoUsuario = "client",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(registroDto.Password)
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetUsuario", new { id = usuario.Id }, new UsuarioPerfilDto
            {
                Id = usuario.Id,
                Nombre = usuario.Nombre,
                Email = usuario.Email,
                Rol = "client"
            });
        }

        // ==========================================
        // 'Register.jsx' (Pestaña DJ).
        // FLUJO: Igual que el cliente, pero con una diferencia vital: también crea automáticamente 
        // una fila vinculada en la tabla 'DjPerfiles' para que el DJ tenga un catálogo desde el minuto 1.
        // ==========================================
        [HttpPost("registro/dj")]
        public async Task<ActionResult<UsuarioPerfilDto>> RegistrarDJ(RegistroDto registroDto)
        {
            if (await _context.Usuarios.AnyAsync(u => u.Email == registroDto.Email))
                return BadRequest("El email ya existe.");

            var usuario = new Usuario
            {
                Nombre = registroDto.Nombre,
                Email = registroDto.Email,
                Ubicacion = registroDto.Ubicacion,
                Telefono = registroDto.Telefono,
                TipoUsuario = "dj",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(registroDto.Password)
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            // Creamos el perfil público vacío por defecto
            var perfil = new DjPerfil
            {
                UsuarioId = usuario.Id,
                NombreArtistico = "DJ " + usuario.Nombre,
                Generos = "Varios",
                PrecioPorHora = 50.00m
            };

            _context.DjPerfiles.Add(perfil);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "DJ registrado correctamente", id = usuario.Id });
        }

        // ==========================================
        // 'Login.jsx' y 'AuthContext.jsx'.
        // FLUJO: Comprueba el email y la contraseña desencriptada. Si es correcto, empaqueta
        // la ID y el Rol del usuario en un Token JWT (el "pase VIP") y se lo devuelve a React 
        // para que lo guarde en el LocalStorage.
        // ==========================================
        [HttpPost("login")]
        public async Task<ActionResult<object>> Login([FromBody] LoginDto loginData)
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == loginData.Email && u.Activo == true);

            if (usuario == null || !BCrypt.Net.BCrypt.Verify(loginData.Password, usuario.PasswordHash))
                return Unauthorized("Email o contraseña incorrectos.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Email),
                new Claim("id", usuario.Id.ToString()),
                new Claim("role", usuario.TipoUsuario ?? "client"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(2), // Caduca a las 2 horas por seguridad
                signingCredentials: creds
            );

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                usuario = usuario.Nombre,
                rol = usuario.TipoUsuario
            });
        }

        // ==========================================
        // 'AuthContext.jsx' (Función checkAuth).
        // FLUJO: Cada vez que el usuario recarga la página en React (F5), React manda su Token aquí. 
        // El Backend lee el ID oculto en el token y le devuelve todos sus datos para mantener la sesión viva.
        // ==========================================
        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<UsuarioPerfilDto>> GetMyProfile()
        {
            var userId = int.Parse(User.FindFirst("id")?.Value);
            var usuario = await _context.Usuarios.FindAsync(userId);
            if (usuario == null) return NotFound();

            return new UsuarioPerfilDto
            {
                Id = usuario.Id,
                Nombre = usuario.Nombre,
                Email = usuario.Email,
                Foto = usuario.FotoPerfil,
                Rol = usuario.TipoUsuario,
                Telefono = usuario.Telefono,
                Ubicacion = usuario.Ubicacion
            };
        }

        // ==========================================
        // ESTO AUN NO SE USA, SE DEJA EL ENDPOINT PARA UN FUTURO
        // FLUJO: Permite obtener los datos básicos de CUALQUIER usuario si tienes su ID.
        // Podría servir en el futuro para una vista de "Perfil de Cliente".
        // ==========================================
        [HttpGet("{id}")]
        public async Task<ActionResult<UsuarioPerfilDto>> GetUsuario(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null) return NotFound();

            return new UsuarioPerfilDto
            {
                Id = usuario.Id,
                Nombre = usuario.Nombre,
                Email = usuario.Email,
                Foto = usuario.FotoPerfil,
                Rol = usuario.TipoUsuario
            };
        }

        // ==========================================
        // Pestaña 'Configuración' (Para cambiar el Nombre/Teléfono).
        // FLUJO: Actualiza los datos de la tabla base 'Usuarios'.
        // ==========================================
        [Authorize]
        [HttpPut("perfil")]
        public async Task<IActionResult> UpdateMiPerfil(UpdateClienteDto datos)
        {
            var userId = int.Parse(User.FindFirst("id")?.Value);
            var usuario = await _context.Usuarios.FindAsync(userId);
            if (usuario == null) return NotFound();

            usuario.Nombre = datos.Nombre;
            usuario.Telefono = datos.Telefono;
            usuario.Ubicacion = datos.Ubicacion;

            await _context.SaveChangesAsync();
            return Ok(new { mensaje = "Datos actualizados correctamente" });
        }

        // ==========================================
        // Pestaña 'Configuración' (Componente de Avatar).
        // FLUJO: Recibe una imagen física, la sube a Cloudinary (servidor de imágenes),
        // borra la foto antigua si existía, y guarda la nueva URL en la base de datos.
        // ==========================================
        [Authorize]
        [HttpPut("perfil/foto")]
        public async Task<ActionResult> SubirFotoPerfil([FromForm] PerfilFotoDto dto)
        {
            var userId = int.Parse(User.FindFirst("id")?.Value);
            var usuario = await _context.Usuarios.FindAsync(userId);

            if (usuario == null) return NotFound("Usuario no encontrado.");

            if (!string.IsNullOrEmpty(usuario.FotoPerfilPublicId))
            {
                await _cloudinaryService.DeleteFileAsync(usuario.FotoPerfilPublicId, CloudinaryDotNet.Actions.ResourceType.Image);
            }

            var resultado = await _cloudinaryService.UploadImageAsync(dto.File);
            if (resultado.Error != null) return BadRequest(resultado.Error.Message);

            usuario.FotoPerfil = resultado.SecureUrl.AbsoluteUri;
            usuario.FotoPerfilPublicId = resultado.PublicId;

            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Foto actualizada", url = usuario.FotoPerfil });
        }

        // ==========================================
        // ESTO AUN NO SE USA, SE DEJA EL ENDPOINT PARA UN FUTURO
        // FLUJO: Borra la imagen de Cloudinary para ahorrar espacio y limpia la DB.
        // ==========================================
        [Authorize]
        [HttpDelete("perfil/foto")]
        public async Task<ActionResult> EliminarFotoPerfil()
        {
            var userId = int.Parse(User.FindFirst("id")?.Value);
            var usuario = await _context.Usuarios.FindAsync(userId);

            if (usuario == null) return NotFound("Usuario no encontrado.");

            if (!string.IsNullOrEmpty(usuario.FotoPerfilPublicId))
            {
                await _cloudinaryService.DeleteFileAsync(usuario.FotoPerfilPublicId, CloudinaryDotNet.Actions.ResourceType.Image);
            }

            usuario.FotoPerfil = null;
            usuario.FotoPerfilPublicId = null;

            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Foto de perfil eliminada." });
        }

        // ==========================================
        // ESTO AUN NO SE USA, SE DEJA EL ENDPOINT PARA UN FUTURO.
        // FLUJO: Un "Soft Delete". En lugar de borrar al usuario (que rompería las reservas pasadas),
        // lo marcamos como Activo = false para que no pueda loguearse ni salir en las búsquedas.
        // ==========================================
        [Authorize]
        [HttpDelete("desactivar-cuenta")]
        public async Task<IActionResult> DesactivarCuenta()
        {
            var userId = int.Parse(User.FindFirst("id")?.Value);
            var usuario = await _context.Usuarios.FindAsync(userId);
            if (usuario == null) return NotFound();

            usuario.Activo = false;
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Tu cuenta ha sido desactivada." });
        }
    }
}