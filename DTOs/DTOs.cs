namespace ChatAPI.DTOs
{
    public class RegisterDto
    {
        public string Nombre { get; set; } = "";
        public string Correo { get; set; } = "";
    }

    public class LoginDto
    {
        public string Correo { get; set; } = "";
        public string Contrasena { get; set; } = "";
    }

    public class BuscarUsuarioDto
    {
        public string Correo { get; set; } = "";
    }

    public class UsuarioDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string Correo { get; set; } = "";
        public string AvatarColor { get; set; } = "";
        public DateTime? UltimaConexion { get; set; }
    }

    public class MensajeResponseDto
    {
        public int Id { get; set; }
        public int EmisorId { get; set; }
        public string EmisorNombre { get; set; } = "";
        public int ReceptorId { get; set; }
        public string Contenido { get; set; } = "";
        public string TipoMensaje { get; set; } = "texto";
        public string? ArchivoUrl { get; set; }
        public string? ArchivoNombre { get; set; }
        public DateTime FechaEnvio { get; set; }
        public bool Leido { get; set; }
    }
}
