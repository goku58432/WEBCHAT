using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using ChatAPI.Data;
using ChatAPI.Models;
using ChatAPI.Services;

namespace ChatAPI.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly AppDbContext _db;
        private readonly CryptoService _crypto;
        private static readonly Dictionary<int, string> _conexiones = new();

        public ChatHub(AppDbContext db, CryptoService crypto)
        {
            _db = db; _crypto = crypto;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = int.Parse(Context.User!.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            _conexiones[userId] = Context.ConnectionId;
            var u = await _db.Usuarios.FindAsync(userId);
            if (u != null) { u.UltimaConexion = DateTime.UtcNow; await _db.SaveChangesAsync(); }
            await Clients.All.SendAsync("UsuarioConectado", userId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            var userId = int.Parse(Context.User!.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            _conexiones.Remove(userId);
            await Clients.All.SendAsync("UsuarioDesconectado", userId);
            await base.OnDisconnectedAsync(ex);
        }

        public async Task EnviarMensaje(int receptorId, string contenido)
        {
            var emisorId = int.Parse(Context.User!.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var emisor = await _db.Usuarios.FindAsync(emisorId);
            if (emisor == null) return;

            var msg = new Mensaje
            {
                EmisorId = emisorId,
                ReceptorId = receptorId,
                ContenidoCifrado = _crypto.Encrypt(contenido),
                TipoMensaje = "texto",
                FechaEnvio = DateTime.UtcNow
            };
            _db.Mensajes.Add(msg);
            await _db.SaveChangesAsync();

            var response = new
            {
                id = msg.Id,
                emisorId,
                emisorNombre = emisor.Nombre,
                receptorId,
                contenido,
                tipoMensaje = "texto",
                archivoUrl = (string?)null,
                archivoNombre = (string?)null,
                fechaEnvio = msg.FechaEnvio,
                leido = false
            };

            await Clients.Caller.SendAsync("MensajeRecibido", response);
            if (_conexiones.TryGetValue(receptorId, out var connId))
                await Clients.Client(connId).SendAsync("MensajeRecibido", response);
        }

        public async Task NotificarArchivo(int receptorId, object mensajeDto)
        {
            var emisorId = int.Parse(Context.User!.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            if (_conexiones.TryGetValue(receptorId, out var connId))
                await Clients.Client(connId).SendAsync("MensajeRecibido", mensajeDto);
        }
    }
}
