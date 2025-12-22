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

        // ========================================
        // DbSets EXISTENTES
        // ========================================
        public DbSet<Contenido> Contenidos { get; set; }
        public DbSet<ArchivoContenido> ArchivosContenido { get; set; }
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
        public DbSet<Favorito> Favoritos { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - FEED PREMIUM
        // ========================================
        public DbSet<Story> Stories { get; set; }
        public DbSet<StoryVista> StoryVistas { get; set; }
        public DbSet<Reaccion> Reacciones { get; set; }
        public DbSet<Coleccion> Colecciones { get; set; }
        public DbSet<ContenidoColeccion> ContenidoColecciones { get; set; }
        public DbSet<CompraColeccion> ComprasColeccion { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - SISTEMA DE AGENCIAS Y PUBLICIDAD
        // ========================================
        public DbSet<Agencia> Agencias { get; set; }
        public DbSet<Anuncio> Anuncios { get; set; }
        public DbSet<SegmentacionAnuncio> SegmentacionesAnuncios { get; set; }
        public DbSet<ImpresionAnuncio> ImpresionesAnuncios { get; set; }
        public DbSet<ClicAnuncio> ClicsAnuncios { get; set; }
        public DbSet<TransaccionAgencia> TransaccionesAgencias { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - SISTEMA DE INTERESES
        // ========================================
        public DbSet<CategoriaInteres> CategoriasIntereses { get; set; }
        public DbSet<InteresUsuario> InteresesUsuarios { get; set; }
        public DbSet<InteraccionContenido> InteraccionesContenidos { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - SISTEMA DE BLOQUEOS Y SEGURIDAD
        // ========================================
        public DbSet<BloqueoUsuario> BloqueosUsuarios { get; set; }
        public DbSet<IpBloqueada> IpsBloqueadas { get; set; }
        public DbSet<IntentoAtaque> IntentosAtaque { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - BIBLIOTECA DE MÚSICA
        // ========================================
        public DbSet<PistaMusical> PistasMusica { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - SISTEMA DE FEEDBACK
        // ========================================
        public DbSet<Feedback> Feedbacks { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - NOTIFICACIONES
        // ========================================
        public DbSet<Notificacion> Notificaciones { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - LOGS Y EVENTOS
        // ========================================
        public DbSet<LogEvento> LogEventos { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - JWT TOKENS
        // ========================================
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<ActiveToken> ActiveTokens { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - CONTADOR DE VISITAS
        // ========================================
        public DbSet<VisitaApp> VisitasApp { get; set; }
        public DbSet<VisitaDetalle> VisitasDetalle { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - TASAS DE CAMBIO
        // ========================================
        public DbSet<TasaCambio> TasasCambio { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - RETENCIONES POR PAÍS
        // ========================================
        public DbSet<RetencionPais> RetencionesPaises { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - ALGORITMOS DE FEED
        // ========================================
        public DbSet<AlgoritmoFeed> AlgoritmosFeed { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - CONFIGURACIÓN PLATAFORMA
        // ========================================
        public DbSet<ConfiguracionPlataforma> ConfiguracionesPlataforma { get; set; }
        public DbSet<PreferenciaAlgoritmoUsuario> PreferenciasAlgoritmoUsuario { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - SISTEMA DE CONFIANZA
        // ========================================
        public DbSet<ConfiguracionConfianza> ConfiguracionesConfianza { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - LIKES EN COMENTARIOS
        // ========================================
        public DbSet<LikeComentario> LikesComentarios { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ========================================
            // CONFIGURACIÓN DE APPLICATION USER (EXISTENTE)
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

                entity.Property(e => e.Seudonimo)
                    .HasMaxLength(50)
                    .IsRequired(false);

                entity.Property(e => e.SeudonimoVerificado)
                    .HasDefaultValue(false)
                    .IsRequired();

                entity.HasIndex(e => e.TipoUsuario)
                    .HasDatabaseName("IX_AspNetUsers_TipoUsuario");

                entity.HasIndex(e => e.EsCreador)
                    .HasDatabaseName("IX_AspNetUsers_EsCreador");

                entity.HasIndex(e => e.Seudonimo)
                    .HasDatabaseName("IX_AspNetUsers_Seudonimo");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE STORIES
            // ========================================
            modelBuilder.Entity<Story>(entity =>
            {
                entity.HasKey(s => s.Id);

                entity.HasOne(s => s.Creador)
                    .WithMany()
                    .HasForeignKey(s => s.CreadorId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(s => s.RutaArchivo)
                    .HasMaxLength(500)
                    .IsRequired();

                entity.Property(s => s.Texto)
                    .HasMaxLength(500)
                    .IsRequired(false);

                entity.Property(s => s.EstaActivo)
                    .HasDefaultValue(true);

                entity.Property(s => s.NumeroVistas)
                    .HasDefaultValue(0);

                // Índices para Stories
                entity.HasIndex(s => s.CreadorId)
                    .HasDatabaseName("IX_Stories_CreadorId");

                entity.HasIndex(s => s.FechaExpiracion)
                    .HasDatabaseName("IX_Stories_FechaExpiracion");

                entity.HasIndex(s => new { s.CreadorId, s.FechaExpiracion, s.EstaActivo })
                    .HasDatabaseName("IX_Stories_Creador_Expiracion_Activo");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE STORY VISTAS
            // ========================================
            modelBuilder.Entity<StoryVista>(entity =>
            {
                entity.HasKey(sv => sv.Id);

                entity.HasOne(sv => sv.Story)
                    .WithMany(s => s.Vistas)
                    .HasForeignKey(sv => sv.StoryId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(sv => sv.Usuario)
                    .WithMany()
                    .HasForeignKey(sv => sv.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Índices para StoryVistas
                entity.HasIndex(sv => sv.StoryId)
                    .HasDatabaseName("IX_StoryVistas_StoryId");

                entity.HasIndex(sv => sv.UsuarioId)
                    .HasDatabaseName("IX_StoryVistas_UsuarioId");

                // Un usuario solo puede ver una story una vez
                entity.HasIndex(sv => new { sv.StoryId, sv.UsuarioId })
                    .IsUnique()
                    .HasDatabaseName("IX_StoryVistas_Story_Usuario_Unique");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE REACCIONES
            // ========================================
            modelBuilder.Entity<Reaccion>(entity =>
            {
                entity.HasKey(r => r.Id);

                entity.HasOne(r => r.Contenido)
                    .WithMany(c => c.Reacciones)
                    .HasForeignKey(r => r.ContenidoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.Usuario)
                    .WithMany()
                    .HasForeignKey(r => r.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Índices para Reacciones
                entity.HasIndex(r => r.ContenidoId)
                    .HasDatabaseName("IX_Reacciones_ContenidoId");

                entity.HasIndex(r => r.UsuarioId)
                    .HasDatabaseName("IX_Reacciones_UsuarioId");

                // Un usuario solo puede tener UNA reacción por contenido
                entity.HasIndex(r => new { r.UsuarioId, r.ContenidoId })
                    .IsUnique()
                    .HasDatabaseName("IX_Reacciones_Usuario_Contenido_Unique");

                entity.HasIndex(r => new { r.ContenidoId, r.TipoReaccion })
                    .HasDatabaseName("IX_Reacciones_Contenido_Tipo");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE COLECCIONES
            // ========================================
            modelBuilder.Entity<Coleccion>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.HasOne(c => c.Creador)
                    .WithMany()
                    .HasForeignKey(c => c.CreadorId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(c => c.Nombre)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(c => c.Descripcion)
                    .HasMaxLength(1000)
                    .IsRequired(false);

                entity.Property(c => c.ImagenPortada)
                    .HasMaxLength(500)
                    .IsRequired(false);

                entity.Property(c => c.Precio)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                entity.Property(c => c.PrecioOriginal)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired(false);

                entity.Property(c => c.EstaActiva)
                    .HasDefaultValue(true);

                // Índices para Colecciones
                entity.HasIndex(c => c.CreadorId)
                    .HasDatabaseName("IX_Colecciones_CreadorId");

                entity.HasIndex(c => c.EstaActiva)
                    .HasDatabaseName("IX_Colecciones_EstaActiva");

                entity.HasIndex(c => new { c.CreadorId, c.EstaActiva })
                    .HasDatabaseName("IX_Colecciones_Creador_Activa");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE CONTENIDO COLECCIONES (Tabla Intermedia)
            // ========================================
            modelBuilder.Entity<ContenidoColeccion>(entity =>
            {
                // Clave compuesta
                entity.HasKey(cc => new { cc.ContenidoId, cc.ColeccionId });

                entity.HasOne(cc => cc.Contenido)
                    .WithMany(c => c.Colecciones)
                    .HasForeignKey(cc => cc.ContenidoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(cc => cc.Coleccion)
                    .WithMany(col => col.Contenidos)
                    .HasForeignKey(cc => cc.ColeccionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(cc => cc.Orden)
                    .HasDefaultValue(0);

                // Índices para ContenidoColecciones
                entity.HasIndex(cc => cc.ColeccionId)
                    .HasDatabaseName("IX_ContenidoColecciones_ColeccionId");

                entity.HasIndex(cc => cc.ContenidoId)
                    .HasDatabaseName("IX_ContenidoColecciones_ContenidoId");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE COMPRAS COLECCIÓN
            // ========================================
            modelBuilder.Entity<CompraColeccion>(entity =>
            {
                entity.HasKey(cc => cc.Id);

                entity.HasOne(cc => cc.Coleccion)
                    .WithMany(c => c.Compras)
                    .HasForeignKey(cc => cc.ColeccionId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(cc => cc.Comprador)
                    .WithMany()
                    .HasForeignKey(cc => cc.CompradorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(cc => cc.Precio)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                // Índices para ComprasColeccion
                entity.HasIndex(cc => cc.ColeccionId)
                    .HasDatabaseName("IX_ComprasColeccion_ColeccionId");

                entity.HasIndex(cc => cc.CompradorId)
                    .HasDatabaseName("IX_ComprasColeccion_CompradorId");

                entity.HasIndex(cc => new { cc.CompradorId, cc.ColeccionId })
                    .IsUnique()
                    .HasDatabaseName("IX_ComprasColeccion_Comprador_Coleccion_Unique");
            });

            // ========================================
            // CONFIGURACIÓN DE COMPRA CONTENIDO (EXISTENTE)
            // ========================================
            modelBuilder.Entity<CompraContenido>(entity =>
            {
                entity.HasOne(cc => cc.Contenido)
                    .WithMany(c => c.Compras)
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
            // CONFIGURACIÓN DE TIP (EXISTENTE)
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
            // CONFIGURACIÓN DE DESAFÍOS (EXISTENTE)
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
            // CONFIGURACIÓN DE PROPUESTAS (EXISTENTE)
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
            // CONFIGURACIÓN DE SUSCRIPCIONES (EXISTENTE)
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
            // CONFIGURACIÓN DE CONTENIDO (EXISTENTE + ACTUALIZADO)
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

                // ⭐ Nuevas propiedades de Preview
                entity.Property(e => e.TienePreview)
                    .HasDefaultValue(false);

                entity.Property(e => e.RutaPreview)
                    .HasMaxLength(500)
                    .IsRequired(false);

                entity.HasIndex(e => e.UsuarioId);
                entity.HasIndex(e => e.EstaActivo);
                entity.HasIndex(e => e.FechaPublicacion);
                entity.HasIndex(e => e.TipoLado);

                // ⚡ Índices compuestos para optimizar Explorar
                entity.HasIndex(e => new { e.EstaActivo, e.EsBorrador, e.Censurado, e.EsPrivado, e.TipoLado, e.FechaPublicacion })
                    .HasDatabaseName("IX_Contenidos_Explorar_Optimizado");

                entity.HasIndex(e => new { e.EstaActivo, e.EsBorrador, e.Censurado, e.EsPrivado, e.UsuarioId })
                    .HasDatabaseName("IX_Contenidos_Usuario_Activo");

                entity.HasIndex(e => new { e.TipoLado, e.EstaActivo, e.Latitud, e.Longitud })
                    .HasDatabaseName("IX_Contenidos_Mapa_Optimizado");

                entity.HasIndex(e => new { e.NumeroLikes, e.NumeroComentarios, e.FechaPublicacion })
                    .HasDatabaseName("IX_Contenidos_Popularidad");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE ARCHIVOS CONTENIDO (CARRUSEL)
            // ========================================
            modelBuilder.Entity<ArchivoContenido>(entity =>
            {
                entity.HasKey(a => a.Id);

                entity.HasOne(a => a.Contenido)
                    .WithMany(c => c.Archivos)
                    .HasForeignKey(a => a.ContenidoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(a => a.RutaArchivo)
                    .HasMaxLength(500)
                    .IsRequired();

                entity.Property(a => a.Thumbnail)
                    .HasMaxLength(500)
                    .IsRequired(false);

                entity.Property(a => a.AltText)
                    .HasMaxLength(500)
                    .IsRequired(false);

                entity.Property(a => a.Orden)
                    .HasDefaultValue(0);

                // Índices
                entity.HasIndex(a => a.ContenidoId)
                    .HasDatabaseName("IX_ArchivosContenido_ContenidoId");

                entity.HasIndex(a => new { a.ContenidoId, a.Orden })
                    .HasDatabaseName("IX_ArchivosContenido_Contenido_Orden");
            });

            // ========================================
            // CONFIGURACIÓN DE TRANSACCIONES (EXISTENTE)
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
            // CONFIGURACIÓN DE MENSAJES PRIVADOS (EXISTENTE)
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
            // CONFIGURACIÓN DE CHAT MENSAJES (EXISTENTE)
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
            // CONFIGURACIÓN DE REPORTES (EXISTENTE)
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
            // CONFIGURACIÓN DE LIKES (EXISTENTE)
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
            // CONFIGURACIÓN DE COMENTARIOS (EXISTENTE + HILOS)
            // ========================================
            modelBuilder.Entity<Comentario>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Relación self-referencing para respuestas en hilo
                entity.HasOne(e => e.ComentarioPadre)
                    .WithMany(e => e.Respuestas)
                    .HasForeignKey(e => e.ComentarioPadreId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.ContenidoId);
                entity.HasIndex(e => e.UsuarioId);
                entity.HasIndex(e => e.FechaCreacion);
                entity.HasIndex(e => e.ComentarioPadreId)
                    .HasDatabaseName("IX_Comentarios_ComentarioPadreId");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE LIKES EN COMENTARIOS
            // ========================================
            modelBuilder.Entity<LikeComentario>(entity =>
            {
                entity.HasKey(l => l.Id);

                entity.HasOne(l => l.Comentario)
                    .WithMany(c => c.Likes)
                    .HasForeignKey(l => l.ComentarioId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(l => l.Usuario)
                    .WithMany()
                    .HasForeignKey(l => l.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Índices
                entity.HasIndex(l => l.ComentarioId)
                    .HasDatabaseName("IX_LikesComentarios_ComentarioId");

                entity.HasIndex(l => l.UsuarioId)
                    .HasDatabaseName("IX_LikesComentarios_UsuarioId");

                // Un usuario solo puede dar like una vez a un comentario
                entity.HasIndex(l => new { l.UsuarioId, l.ComentarioId })
                    .IsUnique()
                    .HasDatabaseName("IX_LikesComentarios_Usuario_Comentario_Unique");
            });

            // ========================================
            // CONFIGURACIÓN DE FAVORITOS
            // ========================================
            modelBuilder.Entity<Favorito>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Contenido)
                    .WithMany()
                    .HasForeignKey(e => e.ContenidoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Usuario)
                    .WithMany()
                    .HasForeignKey(e => e.UsuarioId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.UsuarioId, e.ContenidoId })
                    .IsUnique()
                    .HasDatabaseName("IX_Favoritos_Usuario_Contenido_Unique");

                entity.HasIndex(e => e.ContenidoId)
                    .HasDatabaseName("IX_Favoritos_ContenidoId");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE AGENCIAS
            // ========================================
            modelBuilder.Entity<Agencia>(entity =>
            {
                entity.HasKey(a => a.Id);

                entity.HasOne(a => a.Usuario)
                    .WithOne(u => u.Agencia)
                    .HasForeignKey<Agencia>(a => a.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(a => a.SaldoPublicitario)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0);

                entity.Property(a => a.TotalGastado)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0);

                entity.Property(a => a.TotalRecargado)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0);

                entity.HasIndex(a => a.UsuarioId)
                    .IsUnique()
                    .HasDatabaseName("IX_Agencias_UsuarioId");

                entity.HasIndex(a => a.Estado)
                    .HasDatabaseName("IX_Agencias_Estado");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE ANUNCIOS
            // ========================================
            modelBuilder.Entity<Anuncio>(entity =>
            {
                entity.HasKey(a => a.Id);

                entity.HasOne(a => a.Agencia)
                    .WithMany(ag => ag.Anuncios)
                    .HasForeignKey(a => a.AgenciaId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(a => a.PresupuestoDiario)
                    .HasColumnType("decimal(18,2)");

                entity.Property(a => a.PresupuestoTotal)
                    .HasColumnType("decimal(18,2)");

                entity.Property(a => a.CostoPorMilImpresiones)
                    .HasColumnType("decimal(18,4)");

                entity.Property(a => a.CostoPorClic)
                    .HasColumnType("decimal(18,4)");

                entity.Property(a => a.GastoTotal)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0);

                entity.Property(a => a.GastoHoy)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0);

                entity.HasIndex(a => a.AgenciaId)
                    .HasDatabaseName("IX_Anuncios_AgenciaId");

                entity.HasIndex(a => a.Estado)
                    .HasDatabaseName("IX_Anuncios_Estado");

                entity.HasIndex(a => new { a.Estado, a.FechaInicio, a.FechaFin })
                    .HasDatabaseName("IX_Anuncios_Estado_Fechas");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE SEGMENTACION ANUNCIO
            // ========================================
            modelBuilder.Entity<SegmentacionAnuncio>(entity =>
            {
                entity.HasKey(s => s.Id);

                entity.HasOne(s => s.Anuncio)
                    .WithOne(a => a.Segmentacion)
                    .HasForeignKey<SegmentacionAnuncio>(s => s.AnuncioId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE TRANSACCIONES AGENCIA
            // ========================================
            modelBuilder.Entity<TransaccionAgencia>(entity =>
            {
                entity.HasKey(t => t.Id);

                entity.HasOne(t => t.Agencia)
                    .WithMany(a => a.Transacciones)
                    .HasForeignKey(t => t.AgenciaId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(t => t.Anuncio)
                    .WithMany()
                    .HasForeignKey(t => t.AnuncioId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.Property(t => t.Monto)
                    .HasColumnType("decimal(18,2)");

                entity.Property(t => t.SaldoAnterior)
                    .HasColumnType("decimal(18,2)");

                entity.Property(t => t.SaldoPosterior)
                    .HasColumnType("decimal(18,2)");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE CATEGORIAS DE INTERES
            // ========================================
            modelBuilder.Entity<CategoriaInteres>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.HasOne(c => c.CategoriaPadre)
                    .WithMany(c => c.Subcategorias)
                    .HasForeignKey(c => c.CategoriaPadreId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(c => c.Nombre)
                    .HasDatabaseName("IX_CategoriasIntereses_Nombre");

                entity.HasIndex(c => c.EstaActiva)
                    .HasDatabaseName("IX_CategoriasIntereses_EstaActiva");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE INTERESES USUARIO
            // ========================================
            modelBuilder.Entity<InteresUsuario>(entity =>
            {
                entity.HasKey(i => i.Id);

                entity.HasOne(i => i.Usuario)
                    .WithMany(u => u.Intereses)
                    .HasForeignKey(i => i.UsuarioId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(i => i.CategoriaInteres)
                    .WithMany(c => c.UsuariosInteresados)
                    .HasForeignKey(i => i.CategoriaInteresId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(i => i.PesoInteres)
                    .HasColumnType("decimal(5,2)")
                    .HasDefaultValue(1.0m);
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE INTERACCIONES CONTENIDO
            // ========================================
            modelBuilder.Entity<InteraccionContenido>(entity =>
            {
                entity.HasKey(i => i.Id);

                entity.HasOne(i => i.Usuario)
                    .WithMany()
                    .HasForeignKey(i => i.UsuarioId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(i => i.Contenido)
                    .WithMany()
                    .HasForeignKey(i => i.ContenidoId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE CONTENIDO - CATEGORIA INTERES
            // ========================================
            modelBuilder.Entity<Contenido>(entity =>
            {
                entity.HasOne(c => c.CategoriaInteres)
                    .WithMany()
                    .HasForeignKey(c => c.CategoriaInteresId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE BLOQUEOS USUARIO
            // ========================================
            modelBuilder.Entity<BloqueoUsuario>(entity =>
            {
                entity.HasKey(b => b.Id);

                entity.HasOne(b => b.Bloqueador)
                    .WithMany()
                    .HasForeignKey(b => b.BloqueadorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(b => b.Bloqueado)
                    .WithMany()
                    .HasForeignKey(b => b.BloqueadoId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(b => b.Razon)
                    .HasMaxLength(500)
                    .IsRequired(false);

                // Índices
                entity.HasIndex(b => b.BloqueadorId)
                    .HasDatabaseName("IX_BloqueosUsuarios_BloqueadorId");

                entity.HasIndex(b => b.BloqueadoId)
                    .HasDatabaseName("IX_BloqueosUsuarios_BloqueadoId");

                // Un usuario solo puede bloquear a otro una vez
                entity.HasIndex(b => new { b.BloqueadorId, b.BloqueadoId })
                    .IsUnique()
                    .HasDatabaseName("IX_BloqueosUsuarios_Bloqueador_Bloqueado_Unique");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE NOTIFICACIONES
            // ========================================
            modelBuilder.Entity<Notificacion>(entity =>
            {
                entity.HasKey(n => n.Id);

                entity.HasOne(n => n.Usuario)
                    .WithMany()
                    .HasForeignKey(n => n.UsuarioId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(n => n.UsuarioOrigen)
                    .WithMany()
                    .HasForeignKey(n => n.UsuarioOrigenId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.Property(n => n.Mensaje)
                    .HasMaxLength(500)
                    .IsRequired();

                entity.Property(n => n.Titulo)
                    .HasMaxLength(200)
                    .IsRequired(false);

                entity.Property(n => n.UrlDestino)
                    .HasMaxLength(500)
                    .IsRequired(false);

                entity.Property(n => n.ImagenUrl)
                    .HasMaxLength(500)
                    .IsRequired(false);

                entity.Property(n => n.Leida)
                    .HasDefaultValue(false);

                entity.Property(n => n.EstaActiva)
                    .HasDefaultValue(true);

                // Índices para búsqueda eficiente
                entity.HasIndex(n => n.UsuarioId)
                    .HasDatabaseName("IX_Notificaciones_UsuarioId");

                entity.HasIndex(n => n.Leida)
                    .HasDatabaseName("IX_Notificaciones_Leida");

                entity.HasIndex(n => n.FechaCreacion)
                    .HasDatabaseName("IX_Notificaciones_FechaCreacion");

                entity.HasIndex(n => new { n.UsuarioId, n.Leida, n.EstaActiva })
                    .HasDatabaseName("IX_Notificaciones_Usuario_Leida_Activa");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE PISTAS MUSICALES
            // ========================================
            modelBuilder.Entity<PistaMusical>(entity =>
            {
                entity.HasKey(p => p.Id);

                entity.Property(p => p.Titulo)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(p => p.Artista)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(p => p.Album)
                    .HasMaxLength(100)
                    .IsRequired(false);

                entity.Property(p => p.Genero)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(p => p.RutaArchivo)
                    .HasMaxLength(500)
                    .IsRequired();

                entity.Property(p => p.RutaPortada)
                    .HasMaxLength(500)
                    .IsRequired(false);

                entity.Property(p => p.Energia)
                    .HasMaxLength(20)
                    .IsRequired(false);

                entity.Property(p => p.EstadoAnimo)
                    .HasMaxLength(50)
                    .IsRequired(false);

                entity.Property(p => p.EsLibreDeRegalias)
                    .HasDefaultValue(true);

                entity.Property(p => p.ContadorUsos)
                    .HasDefaultValue(0);

                entity.Property(p => p.Activo)
                    .HasDefaultValue(true);

                // Índices para búsqueda eficiente
                entity.HasIndex(p => p.Genero)
                    .HasDatabaseName("IX_PistasMusica_Genero");

                entity.HasIndex(p => p.Activo)
                    .HasDatabaseName("IX_PistasMusica_Activo");

                entity.HasIndex(p => p.ContadorUsos)
                    .HasDatabaseName("IX_PistasMusica_ContadorUsos");

                entity.HasIndex(p => new { p.Activo, p.Genero })
                    .HasDatabaseName("IX_PistasMusica_Activo_Genero");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE ALGORITMOS DE FEED
            // ========================================
            modelBuilder.Entity<AlgoritmoFeed>(entity =>
            {
                entity.HasKey(a => a.Id);

                entity.Property(a => a.Codigo)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(a => a.Nombre)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(a => a.Descripcion)
                    .HasMaxLength(500)
                    .IsRequired(false);

                entity.Property(a => a.Icono)
                    .HasMaxLength(100)
                    .IsRequired(false);

                entity.Property(a => a.Activo)
                    .HasDefaultValue(true);

                entity.Property(a => a.EsPorDefecto)
                    .HasDefaultValue(false);

                entity.Property(a => a.TotalUsos)
                    .HasDefaultValue(0);

                // Índices
                entity.HasIndex(a => a.Codigo)
                    .IsUnique()
                    .HasDatabaseName("IX_AlgoritmosFeed_Codigo");

                entity.HasIndex(a => a.Activo)
                    .HasDatabaseName("IX_AlgoritmosFeed_Activo");

                entity.HasIndex(a => a.EsPorDefecto)
                    .HasDatabaseName("IX_AlgoritmosFeed_EsPorDefecto");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE PREFERENCIAS ALGORITMO USUARIO
            // ========================================
            modelBuilder.Entity<PreferenciaAlgoritmoUsuario>(entity =>
            {
                entity.HasKey(p => p.Id);

                entity.HasOne(p => p.Usuario)
                    .WithMany()
                    .HasForeignKey(p => p.UsuarioId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(p => p.AlgoritmoFeed)
                    .WithMany()
                    .HasForeignKey(p => p.AlgoritmoFeedId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Un usuario solo puede tener una preferencia
                entity.HasIndex(p => p.UsuarioId)
                    .IsUnique()
                    .HasDatabaseName("IX_PreferenciasAlgoritmoUsuario_UsuarioId");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE REFRESH TOKENS (JWT)
            // ========================================
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(r => r.Id);

                entity.HasOne(r => r.User)
                    .WithMany()
                    .HasForeignKey(r => r.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(r => r.Token)
                    .HasMaxLength(500)
                    .IsRequired();

                entity.Property(r => r.DeviceInfo)
                    .HasMaxLength(500)
                    .IsRequired(false);

                entity.Property(r => r.IpAddress)
                    .HasMaxLength(50)
                    .IsRequired(false);

                // Índices
                entity.HasIndex(r => r.Token)
                    .IsUnique()
                    .HasDatabaseName("IX_RefreshTokens_Token");

                entity.HasIndex(r => r.UserId)
                    .HasDatabaseName("IX_RefreshTokens_UserId");

                entity.HasIndex(r => new { r.UserId, r.IsRevoked, r.ExpiryDate })
                    .HasDatabaseName("IX_RefreshTokens_User_Active");
            });

            // ========================================
            // ⭐ ACTIVE TOKENS (VALIDACIÓN JWT EN TIEMPO REAL)
            // ========================================
            modelBuilder.Entity<ActiveToken>(entity =>
            {
                entity.HasKey(a => a.Id);

                entity.HasOne(a => a.User)
                    .WithMany()
                    .HasForeignKey(a => a.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(a => a.Jti)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(a => a.DeviceInfo)
                    .HasMaxLength(500)
                    .IsRequired(false);

                entity.Property(a => a.IpAddress)
                    .HasMaxLength(50)
                    .IsRequired(false);

                // Índices para búsqueda rápida
                entity.HasIndex(a => a.Jti)
                    .IsUnique()
                    .HasDatabaseName("IX_ActiveTokens_Jti");

                entity.HasIndex(a => a.UserId)
                    .HasDatabaseName("IX_ActiveTokens_UserId");

                // Índice para limpieza de tokens expirados
                entity.HasIndex(a => new { a.ExpiresAt, a.IsRevoked })
                    .HasDatabaseName("IX_ActiveTokens_Cleanup");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE PLATAFORMA
            // ========================================
            modelBuilder.Entity<ConfiguracionPlataforma>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.Property(c => c.Clave)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(c => c.Valor)
                    .HasMaxLength(500);

                entity.Property(c => c.Descripcion)
                    .HasMaxLength(255);

                entity.Property(c => c.Categoria)
                    .HasMaxLength(50);

                entity.HasIndex(c => c.Clave)
                    .IsUnique()
                    .HasDatabaseName("IX_ConfiguracionPlataforma_Clave");

                entity.HasIndex(c => c.Categoria)
                    .HasDatabaseName("IX_ConfiguracionPlataforma_Categoria");

                // Datos por defecto
                entity.HasData(
                    new ConfiguracionPlataforma
                    {
                        Id = 1,
                        Clave = ConfiguracionPlataforma.COMISION_BILLETERA_ELECTRONICA,
                        Valor = "2.5",
                        Descripcion = "Comision por usar billetera electronica (%)",
                        Categoria = "Billetera"
                    },
                    new ConfiguracionPlataforma
                    {
                        Id = 2,
                        Clave = ConfiguracionPlataforma.TIEMPO_PROCESO_RETIRO,
                        Valor = "3-5 dias habiles",
                        Descripcion = "Tiempo estimado para procesar retiros",
                        Categoria = "Billetera"
                    },
                    new ConfiguracionPlataforma
                    {
                        Id = 3,
                        Clave = ConfiguracionPlataforma.MONTO_MINIMO_RECARGA,
                        Valor = "5",
                        Descripcion = "Monto minimo para recargar saldo",
                        Categoria = "Billetera"
                    },
                    new ConfiguracionPlataforma
                    {
                        Id = 4,
                        Clave = ConfiguracionPlataforma.MONTO_MAXIMO_RECARGA,
                        Valor = "1000",
                        Descripcion = "Monto maximo para recargar saldo",
                        Categoria = "Billetera"
                    },
                    new ConfiguracionPlataforma
                    {
                        Id = 5,
                        Clave = ConfiguracionPlataforma.COMISION_PLATAFORMA,
                        Valor = "20",
                        Descripcion = "Comision general de la plataforma (%)",
                        Categoria = "General"
                    }
                );
            });
        }
    }
}