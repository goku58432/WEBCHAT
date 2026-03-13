using System.ComponentModel.DataAnnotations;
namespace ChatAPI.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        [Required] public string Nombre { get; set; } = "";
        [Required] public string Correo { get; set; } = "";
        [Required] public string Contrasena { get; set; } = "";
        public string AvatarColor { get; set; } = "#e63946";
        public bool Activo { get; set; } = true;
        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
        public DateTime? UltimaConexion { get; set; }
        public ICollection<Contacto> Contactos { get; set; } = new List<Contacto>();
    }

    public class Contacto
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public int ContactoUsuarioId { get; set; }
        public DateTime FechaAgregado { get; set; } = DateTime.UtcNow;
        public Usuario? Usuario { get; set; }
        public Usuario? ContactoUsuario { get; set; }
    }

    public class Mensaje
    {
        public int Id { get; set; }
        public int EmisorId { get; set; }
        public int ReceptorId { get; set; }
        public string ContenidoCifrado { get; set; } = "";
        // "texto" | "imagen" | "video" | "archivo"
        public string TipoMensaje { get; set; } = "texto";
        // URL de Cloudinary si es media, null si es texto
        public string? ArchivoUrl { get; set; }
        // Nombre original del archivo para descargas
        public string? ArchivoNombre { get; set; }
        public DateTime FechaEnvio { get; set; } = DateTime.UtcNow;
        public bool Leido { get; set; } = false;
        public Usuario? Emisor { get; set; }
        public Usuario? Receptor { get; set; }
    }
}
