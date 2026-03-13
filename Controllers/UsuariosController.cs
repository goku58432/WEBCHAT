using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ChatAPI.Data;
using ChatAPI.DTOs;
using ChatAPI.Models;
using ChatAPI.Services;

namespace ChatAPI.Controllers
{
    public class ArchivoUploadDto
    {
        public int ReceptorId { get; set; }
        public IFormFile Archivo { get; set; } = null!;
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsuariosController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly CryptoService _crypto;
        private readonly CloudinaryService _cloudinary;

        public UsuariosController(AppDbContext db, CryptoService crypto, CloudinaryService cloudinary)
        { _db = db; _crypto = crypto; _cloudinary = cloudinary; }

        private int MyId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        [HttpGet("contactos")]
        public async Task<IActionResult> GetContactos()
        {
            var lista = await _db.Contactos
                .Where(c => c.UsuarioId == MyId)
                .Include(c => c.ContactoUsuario)
                .Select(c => new UsuarioDto
                {
                    Id = c.ContactoUsuario!.Id,
                    Nombre = c.ContactoUsuario.Nombre,
                    Correo = c.ContactoUsuario.Correo,
                    AvatarColor = c.ContactoUsuario.AvatarColor,
                    UltimaConexion = c.ContactoUsuario.UltimaConexion
                }).ToListAsync();
            return Ok(lista);
        }

        [HttpPost("buscar")]
        public async Task<IActionResult> Buscar([FromBody] BuscarUsuarioDto dto)
        {
            var usuario = await _db.Usuarios
                .Where(u => u.Correo == dto.Correo && u.Activo && u.Id != MyId)
                .Select(u => new UsuarioDto
                {
                    Id = u.Id,
                    Nombre = u.Nombre,
                    Correo = u.Correo,
                    AvatarColor = u.AvatarColor,
                    UltimaConexion = u.UltimaConexion
                }).FirstOrDefaultAsync();

            if (usuario == null) return NotFound(new { message = "Usuario no encontrado" });

            var yaExiste = await _db.Contactos
                .AnyAsync(c => c.UsuarioId == MyId && c.ContactoUsuarioId == usuario.Id);
            if (yaExiste) return BadRequest(new { message = "Ya está en tus contactos" });

            return Ok(usuario);
        }

        [HttpPost("contactos")]
        public async Task<IActionResult> AgregarContacto([FromBody] int contactoId)
        {
            if (contactoId == MyId) return BadRequest(new { message = "No puedes agregarte a ti mismo" });

            var existe = await _db.Usuarios.AnyAsync(u => u.Id == contactoId && u.Activo);
            if (!existe) return NotFound(new { message = "Usuario no existe" });

            var yaExiste = await _db.Contactos
                .AnyAsync(c => c.UsuarioId == MyId && c.ContactoUsuarioId == contactoId);
            if (yaExiste) return BadRequest(new { message = "Ya está en tus contactos" });

            _db.Contactos.Add(new Contacto { UsuarioId = MyId, ContactoUsuarioId = contactoId });
            await _db.SaveChangesAsync();
            return Ok(new { message = "Contacto agregado" });
        }

        [HttpDelete("contactos/{contactoId}")]
        public async Task<IActionResult> EliminarContacto(int contactoId)
        {
            var c = await _db.Contactos
                .FirstOrDefaultAsync(x => x.UsuarioId == MyId && x.ContactoUsuarioId == contactoId);
            if (c == null) return NotFound();
            _db.Contactos.Remove(c);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Contacto eliminado" });
        }

        [HttpGet("mensajes/{otroId}")]
        public async Task<IActionResult> GetMensajes(int otroId)
        {
            var msgs = await _db.Mensajes
                .Include(m => m.Emisor)
                .Where(m => (m.EmisorId == MyId && m.ReceptorId == otroId) ||
                            (m.EmisorId == otroId && m.ReceptorId == MyId))
                .OrderBy(m => m.FechaEnvio)
                .Select(m => new MensajeResponseDto
                {
                    Id = m.Id,
                    EmisorId = m.EmisorId,
                    EmisorNombre = m.Emisor!.Nombre,
                    ReceptorId = m.ReceptorId,
                    Contenido = m.TipoMensaje == "texto" ? _crypto.Decrypt(m.ContenidoCifrado) : m.ContenidoCifrado,
                    TipoMensaje = m.TipoMensaje,
                    ArchivoUrl = m.ArchivoUrl,
                    ArchivoNombre = m.ArchivoNombre,
                    FechaEnvio = m.FechaEnvio,
                    Leido = m.Leido
                }).ToListAsync();

            var noLeidos = await _db.Mensajes
                .Where(m => m.EmisorId == otroId && m.ReceptorId == MyId && !m.Leido)
                .ToListAsync();
            noLeidos.ForEach(m => m.Leido = true);
            await _db.SaveChangesAsync();

            return Ok(msgs);
        }

        [HttpPost("mensaje-archivo")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(52_428_800)]
        public async Task<IActionResult> EnviarArchivo([FromForm] ArchivoUploadDto dto)
        {
            var archivo = dto.Archivo;
            var receptorId = dto.ReceptorId;

            if (archivo == null || archivo.Length == 0)
                return BadRequest(new { message = "No se recibió archivo" });

            var ext = Path.GetExtension(archivo.FileName).ToLower();
            string tipo;
            if (new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext)) tipo = "imagen";
            else if (new[] { ".mp4", ".mov", ".avi", ".webm" }.Contains(ext)) tipo = "video";
            else tipo = "archivo";

            var url = await _cloudinary.SubirArchivoAsync(archivo, tipo);

            var msg = new Mensaje
            {
                EmisorId = MyId,
                ReceptorId = receptorId,
                ContenidoCifrado = archivo.FileName,
                TipoMensaje = tipo,
                ArchivoUrl = url,
                ArchivoNombre = archivo.FileName,
                FechaEnvio = DateTime.UtcNow
            };
            _db.Mensajes.Add(msg);
            await _db.SaveChangesAsync();

            return Ok(new MensajeResponseDto
            {
                Id = msg.Id,
                EmisorId = MyId,
                ReceptorId = receptorId,
                Contenido = archivo.FileName,
                TipoMensaje = tipo,
                ArchivoUrl = url,
                ArchivoNombre = archivo.FileName,
                FechaEnvio = msg.FechaEnvio,
                Leido = false
            });
        }

        [HttpGet("no-leidos")]
        public async Task<IActionResult> GetNoLeidos()
        {
            var counts = await _db.Mensajes
                .Where(m => m.ReceptorId == MyId && !m.Leido)
                .GroupBy(m => m.EmisorId)
                .Select(g => new { emisorId = g.Key, count = g.Count() })
                .ToListAsync();
            return Ok(counts);
        }
    }
}
