using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ChatAPI.Data;
using ChatAPI.DTOs;
using ChatAPI.Models;

namespace ChatAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private static readonly string[] Colores = {
            "#e63946","#457b9d","#2dc653","#f4a261","#a8dadc",
            "#e9c46a","#264653","#e76f51","#06d6a0","#118ab2"
        };

        public AuthController(AppDbContext db, IConfiguration config)
        { _db = db; _config = config; }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre) || string.IsNullOrWhiteSpace(dto.Correo))
                return BadRequest(new { message = "Nombre y correo son requeridos" });

            if (await _db.Usuarios.AnyAsync(u => u.Correo == dto.Correo))
                return BadRequest(new { message = "El correo ya está registrado" });

            // Contraseña completamente aleatoria
            var contrasena = GenerarPassword();
            var color = Colores[new Random().Next(Colores.Length)];

            var usuario = new Usuario
            {
                Nombre = dto.Nombre,
                Correo = dto.Correo,
                Contrasena = BCrypt.Net.BCrypt.HashPassword(contrasena),
                AvatarColor = color
            };
            _db.Usuarios.Add(usuario);
            await _db.SaveChangesAsync();

            // Devolvemos la contraseña generada UNA SOLA VEZ
            return Ok(new { message = "Registro exitoso", contrasena });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var usuario = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.Correo == dto.Correo && u.Activo);
            if (usuario == null || !BCrypt.Net.BCrypt.Verify(dto.Contrasena, usuario.Contrasena))
                return Unauthorized(new { message = "Correo o contraseña incorrectos" });

            return Ok(new
            {
                token = GenerarToken(usuario),
                id = usuario.Id,
                nombre = usuario.Nombre,
                correo = usuario.Correo,
                avatarColor = usuario.AvatarColor
            });
        }

        private static string GenerarPassword()
        {
            const string mayus = "ABCDEFGHJKMNPQRSTUVWXYZ";
            const string minus = "abcdefghjkmnpqrstuvwxyz";
            const string nums  = "23456789";
            const string spec  = "!@#$%&*";
            var rng = new Random();
            var chars = new List<char>
            {
                mayus[rng.Next(mayus.Length)],
                mayus[rng.Next(mayus.Length)],
                minus[rng.Next(minus.Length)],
                minus[rng.Next(minus.Length)],
                nums[rng.Next(nums.Length)],
                nums[rng.Next(nums.Length)],
                spec[rng.Next(spec.Length)]
            };
            const string all = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$%&*";
            while (chars.Count < 12) chars.Add(all[rng.Next(all.Length)]);
            return new string(chars.OrderBy(_ => rng.Next()).ToArray());
        }

        private string GenerarToken(Usuario u)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()),
                new Claim(ClaimTypes.Name, u.Nombre),
                new Claim(ClaimTypes.Email, u.Correo)
            };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                _config["Jwt:Issuer"], _config["Jwt:Audience"],
                claims, expires: DateTime.UtcNow.AddHours(12),
                signingCredentials: creds);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
