using Microsoft.EntityFrameworkCore;
using ChatAPI.Models;
namespace ChatAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<Usuario> Usuarios => Set<Usuario>();
        public DbSet<Contacto> Contactos => Set<Contacto>();
        public DbSet<Mensaje> Mensajes => Set<Mensaje>();

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<Contacto>()
                .HasOne(c => c.Usuario).WithMany(u => u.Contactos)
                .HasForeignKey(c => c.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            m.Entity<Contacto>()
                .HasOne(c => c.ContactoUsuario).WithMany()
                .HasForeignKey(c => c.ContactoUsuarioId).OnDelete(DeleteBehavior.Restrict);
            m.Entity<Contacto>()
                .HasIndex(c => new { c.UsuarioId, c.ContactoUsuarioId }).IsUnique();
            m.Entity<Mensaje>()
                .HasOne(x => x.Emisor).WithMany()
                .HasForeignKey(x => x.EmisorId).OnDelete(DeleteBehavior.Restrict);
            m.Entity<Mensaje>()
                .HasOne(x => x.Receptor).WithMany()
                .HasForeignKey(x => x.ReceptorId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
