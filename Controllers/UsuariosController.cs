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
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsuariosController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly CryptoService _crypto;
        public UsuariosController(AppDbContext db, CryptoService crypto)
        { _db = db; _crypto = crypto; }

        private int MyId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        // GET mis contactos
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

        // POST buscar usuario por correo para agregar
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

        // POST agregar contacto
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

        // DELETE eliminar contacto
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

        // GET historial de mensajes con un contacto
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
                    Contenido = _crypto.Decrypt(m.ContenidoCifrado),
                    FechaEnvio = m.FechaEnvio,
                    Leido = m.Leido
                }).ToListAsync();

            // Marcar como leídos
            var noLeidos = await _db.Mensajes
                .Where(m => m.EmisorId == otroId && m.ReceptorId == MyId && !m.Leido)
                .ToListAsync();
            noLeidos.ForEach(m => m.Leido = true);
            await _db.SaveChangesAsync();

            return Ok(msgs);
        }

        // GET conteo de no leídos por contacto
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
