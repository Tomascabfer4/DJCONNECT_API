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
        // 1. Añadimos el servicio de Cloudinary
        private readonly CloudinaryService _cloudinaryService;

        // 2. Actualizamos el constructor para inyectar el servicio
        public UsuariosController(DjConnectContext context, IConfiguration configuration, CloudinaryService cloudinaryService)
        {
            _context = context;
            _configuration = configuration;
            _cloudinaryService = cloudinaryService;
        }

        [HttpPost("registro/cliente")]
        public async Task<ActionResult<UsuarioPerfilDto>> RegistrarCliente(RegistroDto registroDto)
        {
            if (await _context.Usuarios.AnyAsync(u => u.Email == registroDto.Email))
            {
                return BadRequest("El email ya existe.");
            }

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

        [HttpPost("registro/dj")]
        public async Task<ActionResult<UsuarioPerfilDto>> RegistrarDJ(RegistroDto registroDto)
        {
            if (await _context.Usuarios.AnyAsync(u => u.Email == registroDto.Email))
            {
                return BadRequest("El email ya existe.");
            }

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

        [HttpPost("login")]
        public async Task<ActionResult<object>> Login([FromBody] LoginDto loginData)
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == loginData.Email && u.Activo == true);

            if (usuario == null || !BCrypt.Net.BCrypt.Verify(loginData.Password, usuario.PasswordHash))
            {
                return Unauthorized("Email o contraseña incorrectos.");
            }

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
                expires: DateTime.Now.AddHours(2),
                signingCredentials: creds
            );

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                usuario = usuario.Nombre,
                rol = usuario.TipoUsuario
            });
        }

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
                Rol = usuario.TipoUsuario
            };
        }

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
            // 3. Eliminada la línea de FotoPerfil para forzar el uso del endpoint de subida de archivos

            await _context.SaveChangesAsync();
            return Ok(new { mensaje = "Datos actualizados correctamente" });
        }

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

        // ==========================================
        // GESTIÓN DE FOTO DE PERFIL (SUBIR / CAMBIAR)
        // ==========================================
        [Authorize]
        [HttpPut("perfil/foto")]
        public async Task<ActionResult> SubirFotoPerfil([FromForm] PerfilFotoDto dto)
        {
            var userId = int.Parse(User.FindFirst("id")?.Value);
            var usuario = await _context.Usuarios.FindAsync(userId);

            if (usuario == null) return NotFound("Usuario no encontrado.");

            // Si ya tenía foto antes, la borramos de Cloudinary
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
        // ELIMINAR FOTO DE PERFIL
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
    }
}