using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Lado.Models;

namespace Lado.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSets principales
        public DbSet<Contenido> Contenidos { get; set; }
        public DbSet<Suscripcion> Suscripciones { get; set; }
        public DbSet<Transaccion> Transacciones { get; set; }
        public DbSet<Like> Likes { get; set; }
        public DbSet<Comentario> Comentarios { get; set; }
        public DbSet<MensajePrivado> MensajesPrivados { get; set; }
        public DbSet<ChatMensaje> ChatMensajes { get; set; }
        public DbSet<Reporte> Reportes { get; set; }
        public DbSet<AgeVerificationLog> AgeVerificationLogs { get; set; }
        public DbSet<CreatorVerificationRequest> CreatorVerificationRequests { get; set; }
        public DbSet<Desafio> Desafios { get; set; }
        public DbSet<PropuestaDesafio> PropuestasDesafios { get; set; }
        public DbSet<CompraContenido> ComprasContenido { get; set; }
        public DbSet<Tip> Tips { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ========================================
            // CONFIGURACIÓN DE APPLICATION USER
            // ========================================
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(e => e.TotalGanancias)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0)
                    .IsRequired();

                entity.Property(e => e.PrecioSuscripcion)
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.Saldo)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0)
                    .IsRequired();

                entity.HasIndex(e => e.TipoUsuario);
                entity.HasIndex(e => e.EsCreador);
            });

            // ========================================
            // CONFIGURACIÓN DE COMPRA CONTENIDO
            // ========================================
            modelBuilder.Entity<CompraContenido>(entity =>
            {
                entity.HasOne(cc => cc.Contenido)
                    .WithMany()
                    .HasForeignKey(cc => cc.ContenidoId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(cc => cc.Usuario)
                    .WithMany()
                    .HasForeignKey(cc => cc.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(cc => cc.Monto)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                entity.HasIndex(cc => new { cc.UsuarioId, cc.ContenidoId })
                    .IsUnique();
            });

            // ========================================
            // CONFIGURACIÓN DE TIP
            // ========================================
            modelBuilder.Entity<Tip>(entity =>
            {
                entity.HasOne(t => t.Fan)
                    .WithMany()
                    .HasForeignKey(t => t.FanId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.Creador)
                    .WithMany()
                    .HasForeignKey(t => t.CreadorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(t => t.Monto)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();
            });

            // ========================================
            // CONFIGURACIÓN DE DESAFÍOS
            // ========================================
            modelBuilder.Entity<Desafio>(entity =>
            {
                entity.HasKey(d => d.Id);

                entity.HasOne(d => d.Fan)
                    .WithMany()
                    .HasForeignKey(d => d.FanId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.CreadorObjetivo)
                    .WithMany()
                    .HasForeignKey(d => d.CreadorObjetivoId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);

                entity.HasOne(d => d.CreadorAsignado)
                    .WithMany()
                    .HasForeignKey(d => d.CreadorAsignadoId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);

                entity.Property(d => d.Presupuesto)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                entity.Property(d => d.PrecioFinal)
                    .HasColumnType("decimal(18,2)");

                entity.HasIndex(d => d.Estado)
                    .HasDatabaseName("IX_Desafios_Estado");

                entity.HasIndex(d => d.Categoria)
                    .HasDatabaseName("IX_Desafios_Categoria");

                entity.HasIndex(d => d.FechaCreacion)
                    .HasDatabaseName("IX_Desafios_FechaCreacion");

                entity.HasIndex(d => d.FanId)
                    .HasDatabaseName("IX_Desafios_FanId");

                entity.HasIndex(d => d.CreadorObjetivoId)
                    .HasDatabaseName("IX_Desafios_CreadorObjetivoId");

                entity.HasIndex(d => d.CreadorAsignadoId)
                    .HasDatabaseName("IX_Desafios_CreadorAsignadoId");
            });

            // ========================================
            // CONFIGURACIÓN DE PROPUESTAS
            // ========================================
            modelBuilder.Entity<PropuestaDesafio>(entity =>
            {
                entity.HasKey(p => p.Id);

                entity.HasOne(p => p.Desafio)
                    .WithMany(d => d.Propuestas)
                    .HasForeignKey(p => p.DesafioId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(p => p.Creador)
                    .WithMany()
                    .HasForeignKey(p => p.CreadorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(p => p.PrecioPropuesto)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                entity.HasIndex(p => p.Estado)
                    .HasDatabaseName("IX_PropuestasDesafios_Estado");

                entity.HasIndex(p => p.FechaPropuesta)
                    .HasDatabaseName("IX_PropuestasDesafios_FechaPropuesta");

                entity.HasIndex(p => p.CreadorId)
                    .HasDatabaseName("IX_PropuestasDesafios_CreadorId");
            });

            // ========================================
            // CONFIGURACIÓN DE SUSCRIPCIONES
            // ========================================
            modelBuilder.Entity<Suscripcion>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Fan)
                    .WithMany(u => u.Suscripciones)
                    .HasForeignKey(e => e.FanId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Creador)
                    .WithMany(u => u.Suscriptores)
                    .HasForeignKey(e => e.CreadorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(e => e.PrecioMensual)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                entity.Property(e => e.Precio)
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.FechaInicio)
                    .HasColumnType("datetime2")
                    .IsRequired();

                entity.Property(e => e.FechaCancelacion)
                    .HasColumnType("datetime2")
                    .IsRequired(false);

                entity.Property(e => e.ProximaRenovacion)
                    .HasColumnType("datetime2")
                    .IsRequired();

                entity.Property(e => e.EstaActiva)
                    .HasDefaultValue(true)
                    .IsRequired();

                entity.Property(e => e.RenovacionAutomatica)
                    .HasDefaultValue(true)
                    .IsRequired();

                entity.HasIndex(e => e.FanId)
                    .HasDatabaseName("IX_Suscripciones_FanId");

                entity.HasIndex(e => e.CreadorId)
                    .HasDatabaseName("IX_Suscripciones_CreadorId");

                entity.HasIndex(e => e.EstaActiva)
                    .HasDatabaseName("IX_Suscripciones_EstaActiva");

                entity.HasIndex(e => new { e.FanId, e.CreadorId, e.EstaActiva })
                    .HasDatabaseName("IX_Suscripciones_Fan_Creador_Activa");
            });

            // ========================================
            // CONFIGURACIÓN DE CONTENIDO
            // ========================================
            modelBuilder.Entity<Contenido>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Usuario)
                    .WithMany()
                    .HasForeignKey(e => e.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(e => e.PrecioDesbloqueo)
                    .HasColumnType("decimal(18,2)");

                entity.HasIndex(e => e.UsuarioId);
                entity.HasIndex(e => e.EstaActivo);
                entity.HasIndex(e => e.FechaPublicacion);
            });

            // ========================================
            // CONFIGURACIÓN DE TRANSACCIONES
            // ========================================
            modelBuilder.Entity<Transaccion>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Monto)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                entity.Property(e => e.Comision)
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.MontoNeto)
                    .HasColumnType("decimal(18,2)");

                entity.HasIndex(e => e.UsuarioId);
                entity.HasIndex(e => e.FechaTransaccion);
                entity.HasIndex(e => e.EstadoTransaccion);
            });

            // ========================================
            // CONFIGURACIÓN DE MENSAJES PRIVADOS
            // ========================================
            modelBuilder.Entity<MensajePrivado>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Remitente)
                    .WithMany()
                    .HasForeignKey(e => e.RemitenteId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Destinatario)
                    .WithMany()
                    .HasForeignKey(e => e.DestinatarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.RemitenteId);
                entity.HasIndex(e => e.DestinatarioId);
                entity.HasIndex(e => e.FechaEnvio);
                entity.HasIndex(e => e.Leido);
            });

            // ========================================
            // CONFIGURACIÓN DE CHAT MENSAJES
            // ========================================
            modelBuilder.Entity<ChatMensaje>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Remitente)
                    .WithMany()
                    .HasForeignKey(e => e.RemitenteId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Destinatario)
                    .WithMany()
                    .HasForeignKey(e => e.DestinatarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.RemitenteId);
                entity.HasIndex(e => e.DestinatarioId);
                entity.HasIndex(e => e.FechaEnvio);
            });

            // ========================================
            // CONFIGURACIÓN DE REPORTES
            // ========================================
            modelBuilder.Entity<Reporte>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.UsuarioReportador)
                    .WithMany()
                    .HasForeignKey(e => e.UsuarioReportadorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.UsuarioReportado)
                    .WithMany()
                    .HasForeignKey(e => e.UsuarioReportadoId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Contenido)
                    .WithMany()
                    .HasForeignKey(e => e.ContenidoId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.UsuarioReportadorId);
                entity.HasIndex(e => e.UsuarioReportadoId);
                entity.HasIndex(e => e.ContenidoId);
                entity.HasIndex(e => e.FechaReporte);
            });

            // ========================================
            // CONFIGURACIÓN DE LIKES
            // ========================================
            modelBuilder.Entity<Like>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => new { e.UsuarioId, e.ContenidoId })
                    .IsUnique()
                    .HasDatabaseName("IX_Likes_Usuario_Contenido_Unique");

                entity.HasIndex(e => e.ContenidoId);
            });

            // ========================================
            // CONFIGURACIÓN DE COMENTARIOS
            // ========================================
            modelBuilder.Entity<Comentario>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => e.ContenidoId);
                entity.HasIndex(e => e.UsuarioId);
                entity.HasIndex(e => e.FechaCreacion);
            });
        }
    }
}