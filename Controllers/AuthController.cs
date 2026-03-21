using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
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
        private static readonly Dictionary<string, string> _challenges = new();

        public AuthController(AppDbContext db, IConfiguration config)
        { _db = db; _config = config; }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre) || string.IsNullOrWhiteSpace(dto.Correo))
                return BadRequest(new { message = "Nombre y correo son requeridos" });
            if (await _db.Usuarios.AnyAsync(u => u.Correo == dto.Correo))
                return BadRequest(new { message = "El correo ya está registrado" });

            var contrasena = GenerarPassword();
            var color = Colores[new Random().Next(Colores.Length)];
            var usuario = new Usuario
            {
                Nombre = dto.Nombre, Correo = dto.Correo,
                Contrasena = BCrypt.Net.BCrypt.HashPassword(contrasena),
                AvatarColor = color
            };
            _db.Usuarios.Add(usuario);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Registro exitoso", contrasena,
                token = GenerarToken(usuario), id = usuario.Id,
                nombre = usuario.Nombre, correo = usuario.Correo,
                avatarColor = usuario.AvatarColor, tieneBiometrico = false
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Correo == dto.Correo && u.Activo);
            if (usuario == null || !BCrypt.Net.BCrypt.Verify(dto.Contrasena, usuario.Contrasena))
                return Unauthorized(new { message = "Correo o contraseña incorrectos" });
            return Ok(BuildAuthResponse(usuario));
        }

        [HttpPost("webauthn/register-challenge")]
        public async Task<IActionResult> WebAuthnRegisterChallenge([FromBody] WebAuthnChallengeRequestDto dto)
        {
            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Correo == dto.Correo && u.Activo);
            if (usuario == null) return NotFound(new { message = "Usuario no encontrado" });
            var challenge = GenerarChallenge();
            _challenges[$"reg_{dto.Correo}"] = challenge;
            return Ok(new
            {
                challenge,
                rp = new { name = "ChatApp", id = dto.RpId ?? "localhost" },
                user = new
                {
                    id = Convert.ToBase64String(Encoding.UTF8.GetBytes(usuario.Id.ToString())),
                    name = usuario.Correo, displayName = usuario.Nombre
                },
                pubKeyCredParams = new[] { new { alg = -7, type = "public-key" } },
                timeout = 60000, attestation = "none"
            });
        }

        [HttpPost("webauthn/register-verify")]
        public async Task<IActionResult> WebAuthnRegisterVerify([FromBody] WebAuthnRegisterDto dto)
        {
            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Correo == dto.Correo && u.Activo);
            if (usuario == null) return NotFound(new { message = "Usuario no encontrado" });
            if (!_challenges.ContainsKey($"reg_{dto.Correo}"))
                return BadRequest(new { message = "Challenge expirado" });
            _challenges.Remove($"reg_{dto.Correo}");
            usuario.BiometricoCredentialId = dto.CredentialId;
            usuario.BiometricoPublicKey = dto.PublicKey;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Biométrico registrado correctamente" });
        }

        [HttpPost("webauthn/login-challenge")]
        public async Task<IActionResult> WebAuthnLoginChallenge([FromBody] WebAuthnChallengeRequestDto dto)
        {
            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Correo == dto.Correo && u.Activo);
            if (usuario == null || string.IsNullOrEmpty(usuario.BiometricoCredentialId))
                return NotFound(new { message = "No hay biométrico registrado" });
            var challenge = GenerarChallenge();
            _challenges[$"auth_{dto.Correo}"] = challenge;
            return Ok(new
            {
                challenge,
                allowCredentials = new[] { new { type = "public-key", id = usuario.BiometricoCredentialId } },
                timeout = 60000, userVerification = "required"
            });
        }

        [HttpPost("webauthn/login-verify")]
        public async Task<IActionResult> WebAuthnLoginVerify([FromBody] WebAuthnLoginDto dto)
        {
            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Correo == dto.Correo && u.Activo);
            if (usuario == null || string.IsNullOrEmpty(usuario.BiometricoCredentialId))
                return NotFound(new { message = "Usuario o biométrico no encontrado" });
            if (!_challenges.ContainsKey($"auth_{dto.Correo}"))
                return BadRequest(new { message = "Challenge expirado" });
            _challenges.Remove($"auth_{dto.Correo}");
            if (usuario.BiometricoCredentialId != dto.CredentialId)
                return Unauthorized(new { message = "Credencial biométrica no válida" });
            return Ok(BuildAuthResponse(usuario));
        }

        [HttpGet("webauthn/status/{correo}")]
        public async Task<IActionResult> WebAuthnStatus(string correo)
        {
            var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.Correo == correo && x.Activo);
            if (u == null) return NotFound();
            return Ok(new { tieneBiometrico = !string.IsNullOrEmpty(u.BiometricoCredentialId) });
        }

        private object BuildAuthResponse(Usuario u) => new
        {
            token = GenerarToken(u), id = u.Id, nombre = u.Nombre,
            correo = u.Correo, avatarColor = u.AvatarColor,
            tieneBiometrico = !string.IsNullOrEmpty(u.BiometricoCredentialId)
        };

        private static string GenerarChallenge()
        {
            var bytes = new byte[32]; RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes);
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
                mayus[rng.Next(mayus.Length)], mayus[rng.Next(mayus.Length)],
                minus[rng.Next(minus.Length)], minus[rng.Next(minus.Length)],
                nums[rng.Next(nums.Length)], nums[rng.Next(nums.Length)],
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
