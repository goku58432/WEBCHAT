using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ChatAPI.Data;
using ChatAPI.DTOs;
using ChatAPI.Models;

namespace ChatAPI.Controllers
{
    [ApiController]
    [Route("api/facial")]
    public class FacialController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        // Umbral de similitud coseno — entre 0 y 1
        // 0.75 = bastante estricto, baja a 0.70 si hay falsos negativos
        private const double UMBRAL = 0.75;

        public FacialController(AppDbContext db, IConfiguration config)
        { _db = db; _config = config; }

        // ── Registrar embedding del rostro ───────────────────────────────────
        [HttpPost("registrar")]
        public async Task<IActionResult> Registrar([FromBody] FacialRegistrarDto dto)
        {
            if (string.IsNullOrEmpty(dto.Correo) || string.IsNullOrEmpty(dto.Embedding))
                return BadRequest(new { message = "Datos incompletos" });

            var usuario = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.Correo == dto.Correo && u.Activo);
            if (usuario == null)
                return NotFound(new { message = "Usuario no encontrado" });

            // Validar que el embedding es un array de números válido
            try
            {
                var arr = JsonSerializer.Deserialize<float[]>(dto.Embedding);
                if (arr == null || arr.Length < 10)
                    return BadRequest(new { message = "Embedding inválido" });
            }
            catch
            {
                return BadRequest(new { message = "Formato de embedding inválido" });
            }

            usuario.FacialEmbedding = dto.Embedding;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Rostro registrado correctamente" });
        }

        // ── Verificar rostro y devolver token ────────────────────────────────
        [HttpPost("verificar")]
        public async Task<IActionResult> Verificar([FromBody] FacialVerificarDto dto)
        {
            if (string.IsNullOrEmpty(dto.Correo) || string.IsNullOrEmpty(dto.Embedding))
                return BadRequest(new { message = "Datos incompletos" });

            var usuario = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.Correo == dto.Correo && u.Activo);

            if (usuario == null)
                return NotFound(new { message = "Usuario no encontrado" });

            if (string.IsNullOrEmpty(usuario.FacialEmbedding))
                return BadRequest(new { message = "No tienes rostro registrado" });

            try
            {
                var embeddingGuardado  = JsonSerializer.Deserialize<float[]>(usuario.FacialEmbedding)!;
                var embeddingRecibido  = JsonSerializer.Deserialize<float[]>(dto.Embedding)!;

                double similitud = SimilitudCoseno(embeddingGuardado, embeddingRecibido);

                if (similitud < UMBRAL)
                    return Unauthorized(new
                    {
                        message = "Rostro no reconocido",
                        similitud = Math.Round(similitud, 3)
                    });

                return Ok(new
                {
                    token        = GenerarToken(usuario),
                    id           = usuario.Id,
                    nombre       = usuario.Nombre,
                    correo       = usuario.Correo,
                    avatarColor  = usuario.AvatarColor,
                    tieneFacial  = true,
                    similitud    = Math.Round(similitud, 3)
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Error procesando rostro: " + ex.Message });
            }
        }

        // ── Consultar si el usuario tiene facial registrado ───────────────────
        [HttpGet("status/{correo}")]
        public async Task<IActionResult> Status(string correo)
        {
            var u = await _db.Usuarios
                .FirstOrDefaultAsync(x => x.Correo == correo && x.Activo);
            if (u == null) return NotFound();
            return Ok(new { tieneFacial = !string.IsNullOrEmpty(u.FacialEmbedding) });
        }

        // ── Similitud coseno entre dos vectores ──────────────────────────────
        private static double SimilitudCoseno(float[] a, float[] b)
        {
            if (a.Length != b.Length) return 0;
            double dot = 0, normA = 0, normB = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot  += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }
            if (normA == 0 || normB == 0) return 0;
            return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        private string GenerarToken(Usuario u)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()),
                new Claim(ClaimTypes.Name, u.Nombre),
                new Claim(ClaimTypes.Email, u.Correo)
            };
            var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                _config["Jwt:Issuer"], _config["Jwt:Audience"],
                claims, expires: DateTime.UtcNow.AddHours(12),
                signingCredentials: creds);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
