using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Lado.Models;
using Lado.Models.Moderacion;

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
        public DbSet<Apelacion> Apelaciones { get; set; }
        public DbSet<AgeVerificationLog> AgeVerificationLogs { get; set; }
        public DbSet<CreatorVerificationRequest> CreatorVerificationRequests { get; set; }
        public DbSet<Desafio> Desafios { get; set; }
        public DbSet<PropuestaDesafio> PropuestasDesafios { get; set; }
        public DbSet<MensajeDesafio> MensajesDesafio { get; set; }
        public DbSet<DesafioGuardado> DesafiosGuardados { get; set; }
        public DbSet<BadgeUsuario> BadgesUsuario { get; set; }
        public DbSet<EstadisticasDesafiosUsuario> EstadisticasDesafiosUsuario { get; set; }
        public DbSet<NotificacionDesafio> NotificacionesDesafio { get; set; }
        public DbSet<CompraContenido> ComprasContenido { get; set; }
        public DbSet<Tip> Tips { get; set; }
        public DbSet<Favorito> Favoritos { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - FEED PREMIUM
        // ========================================
        public DbSet<Story> Stories { get; set; }
        public DbSet<StoryVista> StoryVistas { get; set; }
        public DbSet<StoryLike> StoryLikes { get; set; }
        public DbSet<StoryDraft> StoryDrafts { get; set; }
        public DbSet<HistoriaDestacada> HistoriasDestacadas { get; set; }
        public DbSet<GrupoDestacado> GruposDestacados { get; set; }
        public DbSet<StoryEnviada> StoriesEnviadas { get; set; }
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
        public DbSet<VistaAnuncioUsuario> VistasAnunciosUsuarios { get; set; }
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
        public DbSet<HistoriaSilenciada> HistoriasSilenciadas { get; set; }
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
        // ⭐ DbSets NUEVOS - PUSH NOTIFICATIONS
        // ========================================
        public DbSet<PushSubscription> PushSubscriptions { get; set; }
        public DbSet<PreferenciasNotificacion> PreferenciasNotificaciones { get; set; }

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

        // ========================================
        // ⭐ DbSets NUEVOS - OBJETOS DETECTADOS EN CONTENIDO
        // ========================================
        public DbSet<ObjetoContenido> ObjetosContenido { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - EMAIL MASIVO
        // ========================================
        public DbSet<PlantillaEmail> PlantillasEmail { get; set; }
        public DbSet<CampanaEmail> CampanasEmail { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - SISTEMA DE POPUPS
        // ========================================
        public DbSet<Popup> Popups { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - SISTEMA LADO COINS
        // ========================================
        public DbSet<LadoCoin> LadoCoins { get; set; }
        public DbSet<TransaccionLadoCoin> TransaccionesLadoCoins { get; set; }
        public DbSet<Referido> Referidos { get; set; }
        public DbSet<RachaUsuario> RachasUsuarios { get; set; }
        public DbSet<ConfiguracionLadoCoin> ConfiguracionesLadoCoins { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - PHOTOWALL (MURO)
        // ========================================
        public DbSet<FotoDestacada> FotosDestacadas { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - PAYPAL
        // ========================================
        public DbSet<OrdenPayPalPendiente> OrdenesPayPalPendientes { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - SEO
        // ========================================
        public DbSet<ConfiguracionSeo> ConfiguracionesSeo { get; set; }
        public DbSet<Redireccion301> Redirecciones301 { get; set; }
        public DbSet<RutaRobotsTxt> RutasRobotsTxt { get; set; }
        public DbSet<BotRobotsTxt> BotsRobotsTxt { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - MODO MANTENIMIENTO
        // ========================================
        public DbSet<ModoMantenimiento> ModoMantenimiento { get; set; }
        public DbSet<HistorialMantenimiento> HistorialMantenimiento { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - NOTAS INTERNAS
        // ========================================
        public DbSet<NotaInterna> NotasInternas { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - AUDITORÍA DE CONFIGURACIONES
        // ========================================
        public DbSet<AuditoriaConfiguracion> AuditoriasConfiguracion { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - TEMPLATES DE RESPUESTAS
        // ========================================
        public DbSet<TemplateRespuesta> TemplatesRespuesta { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - SISTEMA DE SUPERVISIÓN Y MODERACIÓN
        // ========================================
        public DbSet<PermisoSupervisor> PermisosSupervisor { get; set; }
        public DbSet<RolSupervisor> RolesSupervisor { get; set; }
        public DbSet<RolSupervisorPermiso> RolesSupervisorPermisos { get; set; }
        public DbSet<UsuarioSupervisor> UsuariosSupervisor { get; set; }
        public DbSet<ColaModeracion> ColaModeracion { get; set; }
        public DbSet<DecisionModeracion> DecisionesModeracion { get; set; }
        public DbSet<MetricaSupervisor> MetricasSupervisor { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - SISTEMA DE TICKETS INTERNOS
        // ========================================
        public DbSet<TicketInterno> TicketsInternos { get; set; }
        public DbSet<RespuestaTicket> RespuestasTickets { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - CALENDARIO ADMIN
        // ========================================
        public DbSet<EventoAdmin> EventosAdmin { get; set; }
        public DbSet<ParticipanteEvento> ParticipantesEventos { get; set; }

        // ========================================
        // ⭐ DbSets NUEVOS - USUARIOS ADMINISTRADOS
        // ========================================
        public DbSet<MediaBiblioteca> MediaBiblioteca { get; set; }
        public DbSet<ConfiguracionPublicacionAutomatica> ConfiguracionesPublicacionAutomatica { get; set; }

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
            // ⭐ CONFIGURACIÓN DE STORY LIKES
            // ========================================
            modelBuilder.Entity<StoryLike>(entity =>
            {
                entity.HasKey(sl => sl.Id);

                entity.HasOne(sl => sl.Story)
                    .WithMany(s => s.Likes)
                    .HasForeignKey(sl => sl.StoryId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(sl => sl.Usuario)
                    .WithMany()
                    .HasForeignKey(sl => sl.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Un usuario solo puede dar like una vez por story
                entity.HasIndex(sl => new { sl.StoryId, sl.UsuarioId })
                    .IsUnique()
                    .HasDatabaseName("IX_StoryLikes_Story_Usuario_Unique");

                entity.HasIndex(sl => sl.StoryId)
                    .HasDatabaseName("IX_StoryLikes_StoryId");
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
            // ⭐ CONFIGURACIÓN DE HISTORIAS SILENCIADAS
            // ========================================
            modelBuilder.Entity<HistoriaSilenciada>(entity =>
            {
                entity.HasKey(h => h.Id);

                entity.HasOne(h => h.Usuario)
                    .WithMany()
                    .HasForeignKey(h => h.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(h => h.Silenciado)
                    .WithMany()
                    .HasForeignKey(h => h.SilenciadoId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Índices
                entity.HasIndex(h => h.UsuarioId)
                    .HasDatabaseName("IX_HistoriasSilenciadas_UsuarioId");

                entity.HasIndex(h => h.SilenciadoId)
                    .HasDatabaseName("IX_HistoriasSilenciadas_SilenciadoId");

                // Un usuario solo puede silenciar a otro una vez
                entity.HasIndex(h => new { h.UsuarioId, h.SilenciadoId })
                    .IsUnique()
                    .HasDatabaseName("IX_HistoriasSilenciadas_Usuario_Silenciado_Unique");
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
            // ⭐ CONFIGURACIÓN DE OBJETOS DETECTADOS EN CONTENIDO
            // ========================================
            modelBuilder.Entity<ObjetoContenido>(entity =>
            {
                entity.HasKey(o => o.Id);

                entity.HasOne(o => o.Contenido)
                    .WithMany(c => c.ObjetosDetectados)
                    .HasForeignKey(o => o.ContenidoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(o => o.NombreObjeto)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(o => o.Confianza)
                    .HasDefaultValue(0.8f);

                // Índices para búsqueda rápida por objeto
                entity.HasIndex(o => o.NombreObjeto)
                    .HasDatabaseName("IX_ObjetosContenido_NombreObjeto");

                entity.HasIndex(o => o.ContenidoId)
                    .HasDatabaseName("IX_ObjetosContenido_ContenidoId");

                // Índice compuesto para búsquedas con confianza
                entity.HasIndex(o => new { o.NombreObjeto, o.Confianza })
                    .HasDatabaseName("IX_ObjetosContenido_Objeto_Confianza");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE PLANTILLAS EMAIL
            // ========================================
            modelBuilder.Entity<PlantillaEmail>(entity =>
            {
                entity.HasKey(p => p.Id);

                entity.Property(p => p.Nombre)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(p => p.Descripcion)
                    .HasMaxLength(255)
                    .IsRequired(false);

                entity.Property(p => p.Asunto)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(p => p.ContenidoHtml)
                    .IsRequired();

                entity.Property(p => p.Categoria)
                    .HasMaxLength(50)
                    .HasDefaultValue("Marketing");

                entity.Property(p => p.EstaActiva)
                    .HasDefaultValue(true);

                // Índices
                entity.HasIndex(p => p.Categoria)
                    .HasDatabaseName("IX_PlantillasEmail_Categoria");

                entity.HasIndex(p => p.EstaActiva)
                    .HasDatabaseName("IX_PlantillasEmail_EstaActiva");

                entity.HasIndex(p => new { p.EstaActiva, p.Categoria })
                    .HasDatabaseName("IX_PlantillasEmail_Activa_Categoria");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE CAMPAÑAS EMAIL
            // ========================================
            modelBuilder.Entity<CampanaEmail>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.HasOne(c => c.Plantilla)
                    .WithMany()
                    .HasForeignKey(c => c.PlantillaId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(c => c.CreadoPor)
                    .WithMany()
                    .HasForeignKey(c => c.CreadoPorId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.Property(c => c.Nombre)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.Property(c => c.Asunto)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(c => c.ContenidoHtml)
                    .IsRequired();

                entity.Property(c => c.EmailsEspecificos)
                    .IsRequired(false);

                entity.Property(c => c.FiltroAdicional)
                    .IsRequired(false);

                entity.Property(c => c.DetalleErrores)
                    .IsRequired(false);

                entity.Property(c => c.TotalDestinatarios)
                    .HasDefaultValue(0);

                entity.Property(c => c.Enviados)
                    .HasDefaultValue(0);

                entity.Property(c => c.Fallidos)
                    .HasDefaultValue(0);

                // Índices
                entity.HasIndex(c => c.Estado)
                    .HasDatabaseName("IX_CampanasEmail_Estado");

                entity.HasIndex(c => c.FechaCreacion)
                    .HasDatabaseName("IX_CampanasEmail_FechaCreacion");

                entity.HasIndex(c => c.PlantillaId)
                    .HasDatabaseName("IX_CampanasEmail_PlantillaId");

                entity.HasIndex(c => new { c.Estado, c.FechaCreacion })
                    .HasDatabaseName("IX_CampanasEmail_Estado_Fecha");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE POPUPS
            // ========================================
            modelBuilder.Entity<Popup>(entity =>
            {
                entity.HasKey(p => p.Id);

                entity.Property(p => p.Nombre)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(p => p.Titulo)
                    .HasMaxLength(200);

                entity.Property(p => p.ImagenUrl)
                    .HasMaxLength(500);

                entity.Property(p => p.IconoClase)
                    .HasMaxLength(50);

                entity.Property(p => p.ColorFondo)
                    .HasMaxLength(50);

                entity.Property(p => p.ColorTexto)
                    .HasMaxLength(50);

                entity.Property(p => p.ColorBotonPrimario)
                    .HasMaxLength(50);

                entity.Property(p => p.SelectorClick)
                    .HasMaxLength(200);

                entity.Property(p => p.PaginasIncluidas)
                    .HasMaxLength(1000);

                entity.Property(p => p.PaginasExcluidas)
                    .HasMaxLength(1000);

                entity.Property(p => p.AnchoMaximo)
                    .HasDefaultValue(400);

                entity.Property(p => p.MostrarBotonCerrar)
                    .HasDefaultValue(true);

                entity.Property(p => p.CerrarAlClickFuera)
                    .HasDefaultValue(true);

                entity.Property(p => p.MostrarUsuariosLogueados)
                    .HasDefaultValue(true);

                entity.Property(p => p.MostrarUsuariosAnonimos)
                    .HasDefaultValue(true);

                entity.Property(p => p.MostrarEnMovil)
                    .HasDefaultValue(true);

                entity.Property(p => p.MostrarEnDesktop)
                    .HasDefaultValue(true);

                entity.Property(p => p.EstaActivo)
                    .HasDefaultValue(true);

                entity.Property(p => p.Prioridad)
                    .HasDefaultValue(5);

                entity.Property(p => p.Impresiones)
                    .HasDefaultValue(0);

                entity.Property(p => p.Clics)
                    .HasDefaultValue(0);

                entity.Property(p => p.Cierres)
                    .HasDefaultValue(0);

                // Índices para optimización
                entity.HasIndex(p => p.EstaActivo)
                    .HasDatabaseName("IX_Popups_EstaActivo");

                entity.HasIndex(p => p.Tipo)
                    .HasDatabaseName("IX_Popups_Tipo");

                entity.HasIndex(p => new { p.EstaActivo, p.Prioridad })
                    .HasDatabaseName("IX_Popups_Activo_Prioridad");

                entity.HasIndex(p => new { p.EstaActivo, p.FechaInicio, p.FechaFin })
                    .HasDatabaseName("IX_Popups_Activo_Fechas");
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

            // ========================================
            // ⭐ CONFIGURACIÓN DE LADO COINS (SALDO)
            // ========================================
            modelBuilder.Entity<LadoCoin>(entity =>
            {
                entity.HasKey(l => l.Id);

                entity.HasOne(l => l.Usuario)
                    .WithOne(u => u.LadoCoin)
                    .HasForeignKey<LadoCoin>(l => l.UsuarioId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(l => l.SaldoDisponible)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0);

                entity.Property(l => l.SaldoPorVencer)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0);

                entity.Property(l => l.TotalGanado)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0);

                entity.Property(l => l.TotalGastado)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0);

                entity.Property(l => l.TotalQuemado)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0);

                entity.Property(l => l.TotalRecibido)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0);

                // Índices
                entity.HasIndex(l => l.UsuarioId)
                    .IsUnique()
                    .HasDatabaseName("IX_LadoCoins_UsuarioId");

                entity.HasIndex(l => l.SaldoDisponible)
                    .HasDatabaseName("IX_LadoCoins_SaldoDisponible");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE TRANSACCIONES LADO COINS
            // ========================================
            modelBuilder.Entity<TransaccionLadoCoin>(entity =>
            {
                entity.HasKey(t => t.Id);

                entity.HasOne(t => t.Usuario)
                    .WithMany()
                    .HasForeignKey(t => t.UsuarioId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(t => t.Monto)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                entity.Property(t => t.MontoQuemado)
                    .HasColumnType("decimal(18,2)");

                entity.Property(t => t.SaldoAnterior)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                entity.Property(t => t.SaldoPosterior)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                entity.Property(t => t.MontoRestante)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0);

                entity.Property(t => t.Descripcion)
                    .HasMaxLength(500)
                    .IsRequired(false);

                entity.Property(t => t.ReferenciaId)
                    .HasMaxLength(100)
                    .IsRequired(false);

                entity.Property(t => t.TipoReferencia)
                    .HasMaxLength(50)
                    .IsRequired(false);

                entity.Property(t => t.Vencido)
                    .HasDefaultValue(false);

                // Índices
                entity.HasIndex(t => t.UsuarioId)
                    .HasDatabaseName("IX_TransaccionesLadoCoins_UsuarioId");

                entity.HasIndex(t => t.Tipo)
                    .HasDatabaseName("IX_TransaccionesLadoCoins_Tipo");

                entity.HasIndex(t => t.FechaTransaccion)
                    .HasDatabaseName("IX_TransaccionesLadoCoins_FechaTransaccion");

                entity.HasIndex(t => t.FechaVencimiento)
                    .HasDatabaseName("IX_TransaccionesLadoCoins_FechaVencimiento");

                entity.HasIndex(t => t.Vencido)
                    .HasDatabaseName("IX_TransaccionesLadoCoins_Vencido");

                // Índice compuesto para búsqueda de transacciones por vencer (FIFO)
                entity.HasIndex(t => new { t.UsuarioId, t.Vencido, t.FechaVencimiento, t.MontoRestante })
                    .HasDatabaseName("IX_TransaccionesLadoCoins_Vencimiento_FIFO");

                // Índice para historial del usuario
                entity.HasIndex(t => new { t.UsuarioId, t.FechaTransaccion })
                    .HasDatabaseName("IX_TransaccionesLadoCoins_Usuario_Fecha");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE REFERIDOS
            // ========================================
            modelBuilder.Entity<Referido>(entity =>
            {
                entity.HasKey(r => r.Id);

                entity.HasOne(r => r.Referidor)
                    .WithMany(u => u.MisReferidos)
                    .HasForeignKey(r => r.ReferidorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.ReferidoUsuario)
                    .WithMany()
                    .HasForeignKey(r => r.ReferidoUsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(r => r.CodigoUsado)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(r => r.TotalComisionGanada)
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0);

                entity.Property(r => r.BonoReferidorEntregado)
                    .HasDefaultValue(false);

                entity.Property(r => r.BonoReferidoEntregado)
                    .HasDefaultValue(false);

                entity.Property(r => r.BonoCreadorLadoBEntregado)
                    .HasDefaultValue(false);

                entity.Property(r => r.ComisionActiva)
                    .HasDefaultValue(true);

                // Índices
                entity.HasIndex(r => r.ReferidorId)
                    .HasDatabaseName("IX_Referidos_ReferidorId");

                entity.HasIndex(r => r.ReferidoUsuarioId)
                    .IsUnique()
                    .HasDatabaseName("IX_Referidos_ReferidoUsuarioId");

                entity.HasIndex(r => r.CodigoUsado)
                    .HasDatabaseName("IX_Referidos_CodigoUsado");

                entity.HasIndex(r => r.ComisionActiva)
                    .HasDatabaseName("IX_Referidos_ComisionActiva");

                entity.HasIndex(r => r.FechaExpiracionComision)
                    .HasDatabaseName("IX_Referidos_FechaExpiracionComision");

                // Índice compuesto para comisiones activas
                entity.HasIndex(r => new { r.ReferidorId, r.ComisionActiva, r.FechaExpiracionComision })
                    .HasDatabaseName("IX_Referidos_Comisiones_Activas");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE RACHAS USUARIO
            // ========================================
            modelBuilder.Entity<RachaUsuario>(entity =>
            {
                entity.HasKey(r => r.Id);

                entity.HasOne(r => r.Usuario)
                    .WithOne(u => u.Racha)
                    .HasForeignKey<RachaUsuario>(r => r.UsuarioId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(r => r.RachaActual)
                    .HasDefaultValue(0);

                entity.Property(r => r.RachaMaxima)
                    .HasDefaultValue(0);

                entity.Property(r => r.LikesHoy)
                    .HasDefaultValue(0);

                entity.Property(r => r.ComentariosHoy)
                    .HasDefaultValue(0);

                entity.Property(r => r.ContenidosHoy)
                    .HasDefaultValue(0);

                entity.Property(r => r.Premio5LikesHoy)
                    .HasDefaultValue(false);

                entity.Property(r => r.Premio3ComentariosHoy)
                    .HasDefaultValue(false);

                entity.Property(r => r.PremioContenidoHoy)
                    .HasDefaultValue(false);

                entity.Property(r => r.PremioLoginHoy)
                    .HasDefaultValue(false);

                // Índices
                entity.HasIndex(r => r.UsuarioId)
                    .IsUnique()
                    .HasDatabaseName("IX_RachasUsuarios_UsuarioId");

                entity.HasIndex(r => r.FechaReset)
                    .HasDatabaseName("IX_RachasUsuarios_FechaReset");

                entity.HasIndex(r => r.RachaActual)
                    .HasDatabaseName("IX_RachasUsuarios_RachaActual");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE CONFIGURACION LADO COINS
            // ========================================
            modelBuilder.Entity<ConfiguracionLadoCoin>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.Property(c => c.Clave)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(c => c.Valor)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                entity.Property(c => c.Descripcion)
                    .HasMaxLength(200)
                    .IsRequired(false);

                entity.Property(c => c.Categoria)
                    .HasMaxLength(50)
                    .IsRequired(false);

                entity.Property(c => c.ModificadoPor)
                    .HasMaxLength(100)
                    .IsRequired(false);

                entity.Property(c => c.Activo)
                    .HasDefaultValue(true);

                // Índices
                entity.HasIndex(c => c.Clave)
                    .IsUnique()
                    .HasDatabaseName("IX_ConfiguracionesLadoCoins_Clave");

                entity.HasIndex(c => c.Categoria)
                    .HasDatabaseName("IX_ConfiguracionesLadoCoins_Categoria");

                entity.HasIndex(c => c.Activo)
                    .HasDatabaseName("IX_ConfiguracionesLadoCoins_Activo");

                // Seed de configuraciones por defecto
                entity.HasData(
                    // Bonos de registro
                    new ConfiguracionLadoCoin { Id = 1, Clave = ConfiguracionLadoCoin.BONO_BIENVENIDA, Valor = 20, Descripcion = "Bono de bienvenida al registrarse", Categoria = "Registro", FechaModificacion = new DateTime(2026, 1, 1) },
                    new ConfiguracionLadoCoin { Id = 2, Clave = ConfiguracionLadoCoin.BONO_PRIMER_CONTENIDO, Valor = 5, Descripcion = "Bono por primera publicación", Categoria = "Registro", FechaModificacion = new DateTime(2026, 1, 1) },
                    new ConfiguracionLadoCoin { Id = 3, Clave = ConfiguracionLadoCoin.BONO_VERIFICAR_EMAIL, Valor = 2, Descripcion = "Bono por verificar email", Categoria = "Registro", FechaModificacion = new DateTime(2026, 1, 1) },
                    new ConfiguracionLadoCoin { Id = 4, Clave = ConfiguracionLadoCoin.BONO_COMPLETAR_PERFIL, Valor = 3, Descripcion = "Bono por completar perfil", Categoria = "Registro", FechaModificacion = new DateTime(2026, 1, 1) },

                    // Bonos diarios
                    new ConfiguracionLadoCoin { Id = 5, Clave = ConfiguracionLadoCoin.BONO_LOGIN_DIARIO, Valor = 0.50m, Descripcion = "Bono por login diario", Categoria = "Diario", FechaModificacion = new DateTime(2026, 1, 1) },
                    new ConfiguracionLadoCoin { Id = 6, Clave = ConfiguracionLadoCoin.BONO_CONTENIDO_DIARIO, Valor = 1, Descripcion = "Bono por subir contenido (1/día)", Categoria = "Diario", FechaModificacion = new DateTime(2026, 1, 1) },
                    new ConfiguracionLadoCoin { Id = 7, Clave = ConfiguracionLadoCoin.BONO_5_LIKES, Valor = 0.25m, Descripcion = "Bono por dar 5 likes", Categoria = "Diario", FechaModificacion = new DateTime(2026, 1, 1) },
                    new ConfiguracionLadoCoin { Id = 8, Clave = ConfiguracionLadoCoin.BONO_3_COMENTARIOS, Valor = 0.50m, Descripcion = "Bono por 3 comentarios", Categoria = "Diario", FechaModificacion = new DateTime(2026, 1, 1) },
                    new ConfiguracionLadoCoin { Id = 9, Clave = ConfiguracionLadoCoin.BONO_RACHA_7_DIAS, Valor = 5, Descripcion = "Bono por racha de 7 días", Categoria = "Diario", FechaModificacion = new DateTime(2026, 1, 1) },

                    // Referidos
                    new ConfiguracionLadoCoin { Id = 10, Clave = ConfiguracionLadoCoin.BONO_REFERIDOR, Valor = 10, Descripcion = "Bono para quien invita", Categoria = "Referidos", FechaModificacion = new DateTime(2026, 1, 1) },
                    new ConfiguracionLadoCoin { Id = 11, Clave = ConfiguracionLadoCoin.BONO_REFERIDO, Valor = 15, Descripcion = "Bono para quien es invitado", Categoria = "Referidos", FechaModificacion = new DateTime(2026, 1, 1) },
                    new ConfiguracionLadoCoin { Id = 12, Clave = ConfiguracionLadoCoin.BONO_REFERIDO_CREADOR, Valor = 50, Descripcion = "Bono cuando referido crea en LadoB", Categoria = "Referidos", FechaModificacion = new DateTime(2026, 1, 1) },
                    new ConfiguracionLadoCoin { Id = 13, Clave = ConfiguracionLadoCoin.COMISION_REFERIDO_PORCENTAJE, Valor = 10, Descripcion = "% de comisión de premios del referido", Categoria = "Referidos", FechaModificacion = new DateTime(2026, 1, 1) },
                    new ConfiguracionLadoCoin { Id = 14, Clave = ConfiguracionLadoCoin.COMISION_REFERIDO_MESES, Valor = 3, Descripcion = "Meses de duración de comisión", Categoria = "Referidos", FechaModificacion = new DateTime(2026, 1, 1) },

                    // Sistema
                    new ConfiguracionLadoCoin { Id = 15, Clave = ConfiguracionLadoCoin.PORCENTAJE_QUEMA, Valor = 5, Descripcion = "% de quema por transacción", Categoria = "Sistema", FechaModificacion = new DateTime(2026, 1, 1) },
                    new ConfiguracionLadoCoin { Id = 16, Clave = ConfiguracionLadoCoin.DIAS_VENCIMIENTO, Valor = 30, Descripcion = "Días hasta vencimiento", Categoria = "Sistema", FechaModificacion = new DateTime(2026, 1, 1) },
                    new ConfiguracionLadoCoin { Id = 17, Clave = ConfiguracionLadoCoin.MAX_PORCENTAJE_SUSCRIPCION, Valor = 30, Descripcion = "% máximo de LC en suscripciones", Categoria = "Sistema", FechaModificacion = new DateTime(2026, 1, 1) },
                    new ConfiguracionLadoCoin { Id = 18, Clave = ConfiguracionLadoCoin.MAX_PORCENTAJE_PROPINA, Valor = 100, Descripcion = "% máximo de LC en propinas", Categoria = "Sistema", FechaModificacion = new DateTime(2026, 1, 1) },

                    // Multiplicadores
                    new ConfiguracionLadoCoin { Id = 19, Clave = ConfiguracionLadoCoin.MULTIPLICADOR_PUBLICIDAD, Valor = 1.5m, Descripcion = "$1 LC = $1.50 en ads", Categoria = "Canje", FechaModificacion = new DateTime(2026, 1, 1) },
                    new ConfiguracionLadoCoin { Id = 20, Clave = ConfiguracionLadoCoin.MULTIPLICADOR_BOOST, Valor = 2, Descripcion = "$1 LC = $2 en boost", Categoria = "Canje", FechaModificacion = new DateTime(2026, 1, 1) },

                    // Límites
                    new ConfiguracionLadoCoin { Id = 21, Clave = ConfiguracionLadoCoin.MAX_PREMIO_DIARIO, Valor = 50, Descripcion = "Máximo LC ganables por día", Categoria = "Limites", FechaModificacion = new DateTime(2026, 1, 1) },
                    new ConfiguracionLadoCoin { Id = 22, Clave = ConfiguracionLadoCoin.MAX_PREMIO_MENSUAL, Valor = 500, Descripcion = "Máximo LC ganables por mes", Categoria = "Limites", FechaModificacion = new DateTime(2026, 1, 1) }
                );
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE APPLICATION USER - CÓDIGO REFERIDO
            // ========================================
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(e => e.CodigoReferido)
                    .HasMaxLength(20)
                    .IsRequired(false);

                entity.Property(e => e.PorcentajeMaxLadoCoinsSuscripcion)
                    .HasDefaultValue(30);

                entity.Property(e => e.AceptaLadoCoins)
                    .HasDefaultValue(true);

                entity.Property(e => e.BonoBienvenidaEntregado)
                    .HasDefaultValue(false);

                entity.Property(e => e.BonoPrimerContenidoEntregado)
                    .HasDefaultValue(false);

                entity.Property(e => e.BonoEmailVerificadoEntregado)
                    .HasDefaultValue(false);

                entity.Property(e => e.BonoPerfilCompletoEntregado)
                    .HasDefaultValue(false);

                // Índice para búsqueda por código de referido
                entity.HasIndex(e => e.CodigoReferido)
                    .IsUnique()
                    .HasFilter("[CodigoReferido] IS NOT NULL")
                    .HasDatabaseName("IX_AspNetUsers_CodigoReferido");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE FOTOS DESTACADAS (PHOTOWALL)
            // ========================================
            modelBuilder.Entity<FotoDestacada>(entity =>
            {
                entity.HasKey(f => f.Id);

                entity.HasOne(f => f.Contenido)
                    .WithMany()
                    .HasForeignKey(f => f.ContenidoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(f => f.Usuario)
                    .WithMany()
                    .HasForeignKey(f => f.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(f => f.CostoPagado)
                    .HasDefaultValue(0);

                // Índices
                entity.HasIndex(f => f.ContenidoId)
                    .HasDatabaseName("IX_FotosDestacadas_ContenidoId");

                entity.HasIndex(f => f.UsuarioId)
                    .HasDatabaseName("IX_FotosDestacadas_UsuarioId");

                entity.HasIndex(f => f.FechaExpiracion)
                    .HasDatabaseName("IX_FotosDestacadas_FechaExpiracion");

                entity.HasIndex(f => f.Nivel)
                    .HasDatabaseName("IX_FotosDestacadas_Nivel");

                // Índice compuesto para obtener destacados activos
                entity.HasIndex(f => new { f.FechaInicio, f.FechaExpiracion, f.Nivel })
                    .HasDatabaseName("IX_FotosDestacadas_Activos");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE ORDENES PAYPAL PENDIENTES
            // ========================================
            modelBuilder.Entity<OrdenPayPalPendiente>(entity =>
            {
                entity.HasKey(o => o.Id);

                entity.HasOne(o => o.Usuario)
                    .WithMany()
                    .HasForeignKey(o => o.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Índice único en OrderId (ID de PayPal)
                entity.HasIndex(o => o.OrderId)
                    .IsUnique()
                    .HasDatabaseName("IX_OrdenesPayPalPendientes_OrderId_Unique");

                // Índice único filtrado en CaptureId (solo para no-nulos)
                // Esto previene duplicados en CaptureId cuando existe
                entity.HasIndex(o => o.CaptureId)
                    .IsUnique()
                    .HasFilter("[CaptureId] IS NOT NULL")
                    .HasDatabaseName("IX_OrdenesPayPalPendientes_CaptureId_Unique");

                // Índice para búsquedas por usuario
                entity.HasIndex(o => o.UsuarioId)
                    .HasDatabaseName("IX_OrdenesPayPalPendientes_UsuarioId");

                // Índice para búsquedas por estado
                entity.HasIndex(o => o.Estado)
                    .HasDatabaseName("IX_OrdenesPayPalPendientes_Estado");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE SEO
            // ========================================
            modelBuilder.Entity<ConfiguracionSeo>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.Property(c => c.TituloSitio)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(c => c.DescripcionMeta)
                    .HasMaxLength(500);

                entity.Property(c => c.PalabrasClave)
                    .HasMaxLength(500);

                entity.Property(c => c.OgSiteName)
                    .HasMaxLength(100);

                entity.Property(c => c.OgImagenDefault)
                    .HasMaxLength(500);

                entity.Property(c => c.OgTypeDefault)
                    .HasMaxLength(50);

                entity.Property(c => c.OgLocale)
                    .HasMaxLength(20);

                entity.Property(c => c.TwitterSite)
                    .HasMaxLength(100);

                entity.Property(c => c.TwitterCardType)
                    .HasMaxLength(50);

                entity.Property(c => c.FacebookUrl)
                    .HasMaxLength(200);

                entity.Property(c => c.InstagramUrl)
                    .HasMaxLength(200);

                entity.Property(c => c.TwitterUrl)
                    .HasMaxLength(200);

                entity.Property(c => c.TikTokUrl)
                    .HasMaxLength(200);

                entity.Property(c => c.YouTubeUrl)
                    .HasMaxLength(200);

                entity.Property(c => c.OrganizacionNombre)
                    .HasMaxLength(200);

                entity.Property(c => c.OrganizacionDescripcion)
                    .HasMaxLength(500);

                entity.Property(c => c.OrganizacionLogo)
                    .HasMaxLength(500);

                entity.Property(c => c.OrganizacionFundacion)
                    .HasMaxLength(4);

                entity.Property(c => c.OrganizacionEmail)
                    .HasMaxLength(200);

                entity.Property(c => c.UrlBase)
                    .HasMaxLength(200);

                entity.Property(c => c.GoogleSiteVerification)
                    .HasMaxLength(100);

                entity.Property(c => c.BingSiteVerification)
                    .HasMaxLength(100);

                entity.Property(c => c.PinterestSiteVerification)
                    .HasMaxLength(100);

                entity.Property(c => c.ModificadoPor)
                    .HasMaxLength(100);

                entity.Property(c => c.SitemapPrioridadHome)
                    .HasColumnType("decimal(2,1)");

                entity.Property(c => c.SitemapPrioridadFeedPublico)
                    .HasColumnType("decimal(2,1)");

                entity.Property(c => c.SitemapPrioridadPerfiles)
                    .HasColumnType("decimal(2,1)");

                entity.Property(c => c.SitemapPrioridadContenidoVideo)
                    .HasColumnType("decimal(2,1)");

                entity.Property(c => c.SitemapPrioridadContenidoNormal)
                    .HasColumnType("decimal(2,1)");

                // Datos por defecto
                entity.HasData(
                    new ConfiguracionSeo
                    {
                        Id = 1,
                        TituloSitio = "Lado - Crea, Comparte y Monetiza",
                        DescripcionMeta = "Lado es la plataforma donde creadores y fans se conectan. Crea contenido exclusivo, monetiza tu creatividad y conecta con tu audiencia.",
                        PalabrasClave = "creadores, contenido exclusivo, monetización, fans, suscripciones, creadores de contenido",
                        IndexarSitio = true,
                        OgSiteName = "Lado",
                        OgImagenDefault = "/images/og-default.jpg",
                        OgImagenAncho = 1200,
                        OgImagenAlto = 630,
                        OgTypeDefault = "website",
                        OgLocale = "es_ES",
                        TwitterSite = "@ladoapp",
                        TwitterCardType = "summary_large_image",
                        InstagramUrl = "https://instagram.com/ladoapp",
                        TwitterUrl = "https://twitter.com/ladoapp",
                        OrganizacionNombre = "Lado",
                        OrganizacionDescripcion = "Plataforma de contenido exclusivo para creadores",
                        OrganizacionLogo = "/images/logo-512.png",
                        OrganizacionFundacion = "2024",
                        OrganizacionEmail = "soporte@ladoapp.com",
                        SitemapLimitePerfiles = 500,
                        SitemapLimiteContenido = 1000,
                        SitemapCacheIndexHoras = 1,
                        SitemapCachePaginasHoras = 24,
                        SitemapCachePerfilesHoras = 1,
                        SitemapCacheContenidoHoras = 1,
                        SitemapPrioridadHome = 1.0m,
                        SitemapPrioridadFeedPublico = 0.9m,
                        SitemapPrioridadPerfiles = 0.7m,
                        SitemapPrioridadContenidoVideo = 0.6m,
                        SitemapPrioridadContenidoNormal = 0.5m,
                        RobotsCrawlDelayGoogle = 0,
                        RobotsCrawlDelayBing = 1,
                        RobotsCrawlDelayOtros = 2,
                        UrlBase = "https://ladoapp.com",
                        FechaModificacion = new DateTime(2026, 1, 1)
                    }
                );
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE REDIRECCIONES 301
            // ========================================
            modelBuilder.Entity<Redireccion301>(entity =>
            {
                entity.HasKey(r => r.Id);

                entity.Property(r => r.UrlOrigen)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(r => r.UrlDestino)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(r => r.Nota)
                    .HasMaxLength(500);

                entity.Property(r => r.CreadoPor)
                    .HasMaxLength(100);

                entity.Property(r => r.Activa)
                    .HasDefaultValue(true);

                entity.Property(r => r.PreservarQueryString)
                    .HasDefaultValue(true);

                entity.Property(r => r.ContadorUso)
                    .HasDefaultValue(0);

                // Índice único en URL origen para evitar duplicados
                entity.HasIndex(r => r.UrlOrigen)
                    .IsUnique()
                    .HasDatabaseName("IX_Redirecciones301_UrlOrigen");

                entity.HasIndex(r => r.Activa)
                    .HasDatabaseName("IX_Redirecciones301_Activa");

                // Índice compuesto para búsqueda eficiente
                entity.HasIndex(r => new { r.Activa, r.UrlOrigen })
                    .HasDatabaseName("IX_Redirecciones301_Activa_UrlOrigen");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE RUTAS ROBOTS.TXT
            // ========================================
            modelBuilder.Entity<RutaRobotsTxt>(entity =>
            {
                entity.HasKey(r => r.Id);

                entity.Property(r => r.Ruta)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(r => r.UserAgent)
                    .HasMaxLength(100)
                    .HasDefaultValue("*");

                entity.Property(r => r.Descripcion)
                    .HasMaxLength(200);

                entity.Property(r => r.Activa)
                    .HasDefaultValue(true);

                entity.Property(r => r.Orden)
                    .HasDefaultValue(100);

                // Índices
                entity.HasIndex(r => r.Activa)
                    .HasDatabaseName("IX_RutasRobotsTxt_Activa");

                entity.HasIndex(r => r.UserAgent)
                    .HasDatabaseName("IX_RutasRobotsTxt_UserAgent");

                entity.HasIndex(r => new { r.Activa, r.Orden })
                    .HasDatabaseName("IX_RutasRobotsTxt_Activa_Orden");

                // Datos por defecto
                entity.HasData(
                    // Rutas permitidas
                    new RutaRobotsTxt { Id = 1, Ruta = "/", Tipo = TipoReglaRobots.Allow, UserAgent = "*", Orden = 1, Descripcion = "Permitir raíz", FechaCreacion = new DateTime(2026, 1, 1) },
                    new RutaRobotsTxt { Id = 2, Ruta = "/FeedPublico", Tipo = TipoReglaRobots.Allow, UserAgent = "*", Orden = 2, Descripcion = "Feed público indexable", FechaCreacion = new DateTime(2026, 1, 1) },

                    // Rutas bloqueadas - Áreas administrativas y privadas
                    new RutaRobotsTxt { Id = 3, Ruta = "/Admin/", Tipo = TipoReglaRobots.Disallow, UserAgent = "*", Orden = 10, Descripcion = "Panel de administración", FechaCreacion = new DateTime(2026, 1, 1) },
                    new RutaRobotsTxt { Id = 4, Ruta = "/Account/", Tipo = TipoReglaRobots.Disallow, UserAgent = "*", Orden = 11, Descripcion = "Cuentas de usuario", FechaCreacion = new DateTime(2026, 1, 1) },
                    new RutaRobotsTxt { Id = 5, Ruta = "/api/", Tipo = TipoReglaRobots.Disallow, UserAgent = "*", Orden = 12, Descripcion = "API endpoints", FechaCreacion = new DateTime(2026, 1, 1) },
                    new RutaRobotsTxt { Id = 6, Ruta = "/Identity/", Tipo = TipoReglaRobots.Disallow, UserAgent = "*", Orden = 13, Descripcion = "Identity pages", FechaCreacion = new DateTime(2026, 1, 1) },

                    // Áreas privadas de usuario
                    new RutaRobotsTxt { Id = 7, Ruta = "/Feed/", Tipo = TipoReglaRobots.Disallow, UserAgent = "*", Orden = 20, Descripcion = "Feed privado", FechaCreacion = new DateTime(2026, 1, 1) },
                    new RutaRobotsTxt { Id = 8, Ruta = "/Mensajes/", Tipo = TipoReglaRobots.Disallow, UserAgent = "*", Orden = 21, Descripcion = "Mensajes privados", FechaCreacion = new DateTime(2026, 1, 1) },
                    new RutaRobotsTxt { Id = 9, Ruta = "/Billetera/", Tipo = TipoReglaRobots.Disallow, UserAgent = "*", Orden = 22, Descripcion = "Billetera", FechaCreacion = new DateTime(2026, 1, 1) },
                    new RutaRobotsTxt { Id = 10, Ruta = "/Suscripciones/", Tipo = TipoReglaRobots.Disallow, UserAgent = "*", Orden = 23, Descripcion = "Suscripciones", FechaCreacion = new DateTime(2026, 1, 1) },
                    new RutaRobotsTxt { Id = 11, Ruta = "/Stories/", Tipo = TipoReglaRobots.Disallow, UserAgent = "*", Orden = 24, Descripcion = "Stories", FechaCreacion = new DateTime(2026, 1, 1) },
                    new RutaRobotsTxt { Id = 12, Ruta = "/Dashboard/", Tipo = TipoReglaRobots.Disallow, UserAgent = "*", Orden = 25, Descripcion = "Dashboard", FechaCreacion = new DateTime(2026, 1, 1) },
                    new RutaRobotsTxt { Id = 13, Ruta = "/Configuracion/", Tipo = TipoReglaRobots.Disallow, UserAgent = "*", Orden = 26, Descripcion = "Configuración usuario", FechaCreacion = new DateTime(2026, 1, 1) },

                    // Archivos temporales y framework
                    new RutaRobotsTxt { Id = 14, Ruta = "/Content/temp/", Tipo = TipoReglaRobots.Disallow, UserAgent = "*", Orden = 30, Descripcion = "Archivos temporales", FechaCreacion = new DateTime(2026, 1, 1) },
                    new RutaRobotsTxt { Id = 15, Ruta = "/_framework/", Tipo = TipoReglaRobots.Disallow, UserAgent = "*", Orden = 31, Descripcion = "Framework files", FechaCreacion = new DateTime(2026, 1, 1) },
                    new RutaRobotsTxt { Id = 16, Ruta = "/_blazor/", Tipo = TipoReglaRobots.Disallow, UserAgent = "*", Orden = 32, Descripcion = "Blazor files", FechaCreacion = new DateTime(2026, 1, 1) },

                    // Query strings problemáticos (duplicados)
                    new RutaRobotsTxt { Id = 17, Ruta = "/*?sort=", Tipo = TipoReglaRobots.Disallow, UserAgent = "*", Orden = 40, Descripcion = "Evita duplicados por sort", FechaCreacion = new DateTime(2026, 1, 1) },
                    new RutaRobotsTxt { Id = 18, Ruta = "/*?filter=", Tipo = TipoReglaRobots.Disallow, UserAgent = "*", Orden = 41, Descripcion = "Evita duplicados por filter", FechaCreacion = new DateTime(2026, 1, 1) },
                    new RutaRobotsTxt { Id = 19, Ruta = "/*?page=", Tipo = TipoReglaRobots.Disallow, UserAgent = "*", Orden = 42, Descripcion = "Evita duplicados por paginación", FechaCreacion = new DateTime(2026, 1, 1) }
                );
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE BOTS ROBOTS.TXT
            // ========================================
            modelBuilder.Entity<BotRobotsTxt>(entity =>
            {
                entity.HasKey(b => b.Id);

                entity.Property(b => b.UserAgent)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(b => b.Descripcion)
                    .HasMaxLength(200);

                entity.Property(b => b.Activo)
                    .HasDefaultValue(true);

                entity.Property(b => b.Bloqueado)
                    .HasDefaultValue(false);

                entity.Property(b => b.EsBotImportante)
                    .HasDefaultValue(false);

                entity.Property(b => b.CrawlDelay)
                    .HasDefaultValue(0);

                entity.Property(b => b.Orden)
                    .HasDefaultValue(100);

                // Índice único en UserAgent
                entity.HasIndex(b => b.UserAgent)
                    .IsUnique()
                    .HasDatabaseName("IX_BotsRobotsTxt_UserAgent");

                entity.HasIndex(b => b.Activo)
                    .HasDatabaseName("IX_BotsRobotsTxt_Activo");

                entity.HasIndex(b => new { b.Activo, b.Orden })
                    .HasDatabaseName("IX_BotsRobotsTxt_Activo_Orden");

                // Datos por defecto - Bots importantes
                entity.HasData(
                    new BotRobotsTxt { Id = 1, UserAgent = "Googlebot", Bloqueado = false, CrawlDelay = 0, EsBotImportante = true, Orden = 1, Descripcion = "Bot de Google", FechaCreacion = new DateTime(2026, 1, 1) },
                    new BotRobotsTxt { Id = 2, UserAgent = "Googlebot-Image", Bloqueado = false, CrawlDelay = 0, EsBotImportante = true, Orden = 2, Descripcion = "Bot de Google Images", FechaCreacion = new DateTime(2026, 1, 1) },
                    new BotRobotsTxt { Id = 3, UserAgent = "Bingbot", Bloqueado = false, CrawlDelay = 1, EsBotImportante = true, Orden = 3, Descripcion = "Bot de Bing", FechaCreacion = new DateTime(2026, 1, 1) },
                    new BotRobotsTxt { Id = 4, UserAgent = "Slurp", Bloqueado = false, CrawlDelay = 2, EsBotImportante = true, Orden = 4, Descripcion = "Bot de Yahoo", FechaCreacion = new DateTime(2026, 1, 1) },
                    new BotRobotsTxt { Id = 5, UserAgent = "DuckDuckBot", Bloqueado = false, CrawlDelay = 1, EsBotImportante = true, Orden = 5, Descripcion = "Bot de DuckDuckGo", FechaCreacion = new DateTime(2026, 1, 1) },
                    new BotRobotsTxt { Id = 6, UserAgent = "Yandex", Bloqueado = false, CrawlDelay = 2, EsBotImportante = true, Orden = 6, Descripcion = "Bot de Yandex", FechaCreacion = new DateTime(2026, 1, 1) },
                    new BotRobotsTxt { Id = 7, UserAgent = "facebookexternalhit", Bloqueado = false, CrawlDelay = 0, EsBotImportante = true, Orden = 7, Descripcion = "Bot de Facebook para previews", FechaCreacion = new DateTime(2026, 1, 1) },
                    new BotRobotsTxt { Id = 8, UserAgent = "Twitterbot", Bloqueado = false, CrawlDelay = 0, EsBotImportante = true, Orden = 8, Descripcion = "Bot de Twitter para cards", FechaCreacion = new DateTime(2026, 1, 1) },
                    new BotRobotsTxt { Id = 9, UserAgent = "LinkedInBot", Bloqueado = false, CrawlDelay = 0, EsBotImportante = true, Orden = 9, Descripcion = "Bot de LinkedIn para previews", FechaCreacion = new DateTime(2026, 1, 1) },

                    // Bots SEO/análisis - bloquear
                    new BotRobotsTxt { Id = 10, UserAgent = "SemrushBot", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 50, Descripcion = "Bot de SEMrush (scraping)", FechaCreacion = new DateTime(2026, 1, 1) },
                    new BotRobotsTxt { Id = 11, UserAgent = "AhrefsBot", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 51, Descripcion = "Bot de Ahrefs (scraping)", FechaCreacion = new DateTime(2026, 1, 1) },
                    new BotRobotsTxt { Id = 12, UserAgent = "MJ12bot", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 52, Descripcion = "Bot de Majestic (scraping)", FechaCreacion = new DateTime(2026, 1, 1) },
                    new BotRobotsTxt { Id = 13, UserAgent = "DotBot", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 53, Descripcion = "Bot de Moz (scraping)", FechaCreacion = new DateTime(2026, 1, 1) },
                    new BotRobotsTxt { Id = 14, UserAgent = "BLEXBot", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 54, Descripcion = "Bot de BLEXBot (scraping)", FechaCreacion = new DateTime(2026, 1, 1) },
                    new BotRobotsTxt { Id = 15, UserAgent = "DataForSeoBot", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 55, Descripcion = "Bot de DataForSEO (scraping)", FechaCreacion = new DateTime(2026, 1, 1) },
                    new BotRobotsTxt { Id = 16, UserAgent = "PetalBot", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 56, Descripcion = "Bot de Huawei/Petal", FechaCreacion = new DateTime(2026, 1, 1) },
                    new BotRobotsTxt { Id = 17, UserAgent = "Bytespider", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 57, Descripcion = "Bot de ByteDance/TikTok (agresivo)", FechaCreacion = new DateTime(2026, 1, 1) },

                    // AI Crawlers - bloquear
                    new BotRobotsTxt { Id = 18, UserAgent = "GPTBot", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 60, Descripcion = "Bot de OpenAI/ChatGPT", FechaCreacion = new DateTime(2026, 1, 1) },
                    new BotRobotsTxt { Id = 19, UserAgent = "ChatGPT-User", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 61, Descripcion = "Usuario ChatGPT", FechaCreacion = new DateTime(2026, 1, 1) },
                    new BotRobotsTxt { Id = 20, UserAgent = "CCBot", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 62, Descripcion = "Bot de Common Crawl", FechaCreacion = new DateTime(2026, 1, 1) },
                    new BotRobotsTxt { Id = 21, UserAgent = "anthropic-ai", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 63, Descripcion = "Bot de Anthropic", FechaCreacion = new DateTime(2026, 1, 1) },
                    new BotRobotsTxt { Id = 22, UserAgent = "Claude-Web", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 64, Descripcion = "Bot de Claude", FechaCreacion = new DateTime(2026, 1, 1) },
                    new BotRobotsTxt { Id = 23, UserAgent = "Google-Extended", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 65, Descripcion = "Bot de Google para AI", FechaCreacion = new DateTime(2026, 1, 1) },
                    new BotRobotsTxt { Id = 24, UserAgent = "Amazonbot", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 66, Descripcion = "Bot de Amazon", FechaCreacion = new DateTime(2026, 1, 1) }
                );
            });

            // ========================================
            // ⭐ CONFIGURACIÓN DE SISTEMA DE SUPERVISIÓN Y MODERACIÓN
            // ========================================

            // Permisos de Supervisor
            modelBuilder.Entity<PermisoSupervisor>(entity =>
            {
                entity.HasKey(p => p.Id);

                entity.Property(p => p.Codigo)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(p => p.Nombre)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(p => p.Descripcion)
                    .HasMaxLength(500);

                entity.Property(p => p.Activo)
                    .HasDefaultValue(true);

                entity.HasIndex(p => p.Codigo)
                    .IsUnique()
                    .HasDatabaseName("IX_PermisosSupervisor_Codigo");

                entity.HasIndex(p => p.Modulo)
                    .HasDatabaseName("IX_PermisosSupervisor_Modulo");
            });

            // Roles de Supervisor
            modelBuilder.Entity<RolSupervisor>(entity =>
            {
                entity.HasKey(r => r.Id);

                entity.Property(r => r.Nombre)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(r => r.Descripcion)
                    .HasMaxLength(500);

                entity.Property(r => r.ColorBadge)
                    .HasMaxLength(20)
                    .HasDefaultValue("#4682B4");

                entity.Property(r => r.Icono)
                    .HasMaxLength(50)
                    .HasDefaultValue("fa-user-shield");

                entity.Property(r => r.Activo)
                    .HasDefaultValue(true);

                entity.Property(r => r.MaxItemsSimultaneos)
                    .HasDefaultValue(5);

                entity.HasIndex(r => r.Nombre)
                    .IsUnique()
                    .HasDatabaseName("IX_RolesSupervisor_Nombre");
            });

            // Tabla de unión Rol-Permiso
            modelBuilder.Entity<RolSupervisorPermiso>(entity =>
            {
                entity.HasKey(rp => rp.Id);

                entity.HasOne(rp => rp.RolSupervisor)
                    .WithMany(r => r.RolesPermisos)
                    .HasForeignKey(rp => rp.RolSupervisorId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(rp => rp.PermisoSupervisor)
                    .WithMany(p => p.RolesPermisos)
                    .HasForeignKey(rp => rp.PermisoSupervisorId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(rp => new { rp.RolSupervisorId, rp.PermisoSupervisorId })
                    .IsUnique()
                    .HasDatabaseName("IX_RolesSupervisorPermisos_Rol_Permiso");
            });

            // Usuario Supervisor
            modelBuilder.Entity<UsuarioSupervisor>(entity =>
            {
                entity.HasKey(us => us.Id);

                entity.HasOne(us => us.Usuario)
                    .WithMany()
                    .HasForeignKey(us => us.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(us => us.RolSupervisor)
                    .WithMany(r => r.Usuarios)
                    .HasForeignKey(us => us.RolSupervisorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(us => us.AsignadoPor)
                    .WithMany()
                    .HasForeignKey(us => us.AsignadoPorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(us => us.EstaActivo)
                    .HasDefaultValue(true);

                entity.Property(us => us.EstaDisponible)
                    .HasDefaultValue(true);

                entity.Property(us => us.ItemsAsignados)
                    .HasDefaultValue(0);

                entity.HasIndex(us => us.UsuarioId)
                    .HasDatabaseName("IX_UsuariosSupervisor_UsuarioId");

                entity.HasIndex(us => new { us.EstaActivo, us.EstaDisponible })
                    .HasDatabaseName("IX_UsuariosSupervisor_Activo_Disponible");
            });

            // Cola de Moderación
            modelBuilder.Entity<ColaModeracion>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.HasOne(c => c.Contenido)
                    .WithMany()
                    .HasForeignKey(c => c.ContenidoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.SupervisorAsignado)
                    .WithMany()
                    .HasForeignKey(c => c.SupervisorAsignadoId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.Reporte)
                    .WithMany()
                    .HasForeignKey(c => c.ReporteId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(c => c.ConfianzaIA)
                    .HasColumnType("decimal(5,4)");

                entity.Property(c => c.Estado)
                    .HasDefaultValue(EstadoModeracion.Pendiente);

                entity.Property(c => c.Prioridad)
                    .HasDefaultValue(PrioridadModeracion.Normal);

                entity.Property(c => c.VecesReasignado)
                    .HasDefaultValue(0);

                // Índices para consultas eficientes
                entity.HasIndex(c => c.Estado)
                    .HasDatabaseName("IX_ColaModeracion_Estado");

                entity.HasIndex(c => c.Prioridad)
                    .HasDatabaseName("IX_ColaModeracion_Prioridad");

                entity.HasIndex(c => new { c.Estado, c.Prioridad, c.FechaCreacion })
                    .HasDatabaseName("IX_ColaModeracion_Estado_Prioridad_Fecha");

                entity.HasIndex(c => c.SupervisorAsignadoId)
                    .HasDatabaseName("IX_ColaModeracion_SupervisorAsignado");

                entity.HasIndex(c => c.ContenidoId)
                    .HasDatabaseName("IX_ColaModeracion_ContenidoId");
            });

            // Decisiones de Moderación
            modelBuilder.Entity<DecisionModeracion>(entity =>
            {
                entity.HasKey(d => d.Id);

                entity.HasOne(d => d.ColaModeracion)
                    .WithMany(c => c.Decisiones)
                    .HasForeignKey(d => d.ColaModeracionId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Supervisor)
                    .WithMany()
                    .HasForeignKey(d => d.SupervisorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.RevertidoPor)
                    .WithMany()
                    .HasForeignKey(d => d.RevertidoPorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(d => d.FueRevertida)
                    .HasDefaultValue(false);

                entity.HasIndex(d => d.SupervisorId)
                    .HasDatabaseName("IX_DecisionesModeracion_SupervisorId");

                entity.HasIndex(d => d.FechaDecision)
                    .HasDatabaseName("IX_DecisionesModeracion_FechaDecision");

                entity.HasIndex(d => new { d.SupervisorId, d.FechaDecision })
                    .HasDatabaseName("IX_DecisionesModeracion_Supervisor_Fecha");
            });

            // Métricas de Supervisor
            modelBuilder.Entity<MetricaSupervisor>(entity =>
            {
                entity.HasKey(m => m.Id);

                entity.HasOne(m => m.Supervisor)
                    .WithMany()
                    .HasForeignKey(m => m.SupervisorId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(m => m.Fecha)
                    .HasColumnType("date");

                entity.Property(m => m.TiempoPromedioSegundos)
                    .HasColumnType("decimal(10,2)")
                    .HasDefaultValue(0);

                entity.Property(m => m.TasaAprobacion)
                    .HasColumnType("decimal(5,2)")
                    .HasDefaultValue(0);

                entity.Property(m => m.TasaEscalamiento)
                    .HasColumnType("decimal(5,2)")
                    .HasDefaultValue(0);

                entity.Property(m => m.ConcordanciaIA)
                    .HasColumnType("decimal(5,2)");

                // Índice único por supervisor y fecha
                entity.HasIndex(m => new { m.SupervisorId, m.Fecha })
                    .IsUnique()
                    .HasDatabaseName("IX_MetricasSupervisor_Supervisor_Fecha");

                entity.HasIndex(m => m.Fecha)
                    .HasDatabaseName("IX_MetricasSupervisor_Fecha");
            });

            // ==============================================
            // ÍNDICES CRÍTICOS PARA SEGURIDAD Y RENDIMIENTO
            // ==============================================

            // IpBloqueada - Crítico para middleware de bloqueo
            modelBuilder.Entity<IpBloqueada>(entity =>
            {
                entity.HasIndex(e => e.DireccionIp)
                    .HasDatabaseName("IX_IpsBloqueadas_DireccionIp");

                entity.HasIndex(e => e.EstaActivo)
                    .HasDatabaseName("IX_IpsBloqueadas_EstaActivo");

                entity.HasIndex(e => new { e.DireccionIp, e.EstaActivo })
                    .HasDatabaseName("IX_IpsBloqueadas_DireccionIp_EstaActivo");

                entity.HasIndex(e => new { e.EstaActivo, e.FechaExpiracion })
                    .HasDatabaseName("IX_IpsBloqueadas_EstaActivo_FechaExpiracion");

                entity.HasIndex(e => e.TipoBloqueo)
                    .HasDatabaseName("IX_IpsBloqueadas_TipoBloqueo");
            });

            // IntentoAtaque - Para estadísticas de seguridad
            modelBuilder.Entity<IntentoAtaque>(entity =>
            {
                entity.HasIndex(e => e.DireccionIp)
                    .HasDatabaseName("IX_IntentosAtaque_DireccionIp");

                entity.HasIndex(e => e.Fecha)
                    .HasDatabaseName("IX_IntentosAtaque_Fecha");

                entity.HasIndex(e => e.TipoAtaque)
                    .HasDatabaseName("IX_IntentosAtaque_TipoAtaque");

                entity.HasIndex(e => new { e.DireccionIp, e.Fecha })
                    .HasDatabaseName("IX_IntentosAtaque_DireccionIp_Fecha");

                entity.HasIndex(e => new { e.TipoAtaque, e.Fecha })
                    .HasDatabaseName("IX_IntentosAtaque_TipoAtaque_Fecha");

                entity.HasIndex(e => e.UsuarioId)
                    .HasDatabaseName("IX_IntentosAtaque_UsuarioId");
            });

            // LogEvento - Para panel de administración de logs
            modelBuilder.Entity<LogEvento>(entity =>
            {
                entity.HasIndex(e => e.Fecha)
                    .HasDatabaseName("IX_LogEventos_Fecha");

                entity.HasIndex(e => e.Categoria)
                    .HasDatabaseName("IX_LogEventos_Categoria");

                entity.HasIndex(e => e.Tipo)
                    .HasDatabaseName("IX_LogEventos_Tipo");

                entity.HasIndex(e => e.UsuarioId)
                    .HasDatabaseName("IX_LogEventos_UsuarioId");

                entity.HasIndex(e => new { e.Categoria, e.Tipo })
                    .HasDatabaseName("IX_LogEventos_Categoria_Tipo");

                entity.HasIndex(e => new { e.Fecha, e.Categoria })
                    .HasDatabaseName("IX_LogEventos_Fecha_Categoria");

                entity.HasIndex(e => new { e.UsuarioId, e.Fecha })
                    .HasDatabaseName("IX_LogEventos_UsuarioId_Fecha");
            });

            // ========================================
            // ⭐ CONFIGURACIÓN CALENDARIO ADMIN
            // ========================================
            modelBuilder.Entity<EventoAdmin>(entity =>
            {
                entity.HasOne(e => e.CreadoPor)
                    .WithMany()
                    .HasForeignKey(e => e.CreadoPorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ParticipanteEvento>(entity =>
            {
                entity.HasOne(p => p.Evento)
                    .WithMany(e => e.Participantes)
                    .HasForeignKey(p => p.EventoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(p => p.Usuario)
                    .WithMany()
                    .HasForeignKey(p => p.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========================================
            // ⭐ CONFIGURACIÓN TICKETS INTERNOS
            // ========================================
            modelBuilder.Entity<TicketInterno>(entity =>
            {
                entity.HasOne(t => t.CreadoPor)
                    .WithMany()
                    .HasForeignKey(t => t.CreadoPorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.AsignadoA)
                    .WithMany()
                    .HasForeignKey(t => t.AsignadoAId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<RespuestaTicket>(entity =>
            {
                entity.HasOne(r => r.Ticket)
                    .WithMany(t => t.Respuestas)
                    .HasForeignKey(r => r.TicketId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.Autor)
                    .WithMany()
                    .HasForeignKey(r => r.AutorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========================================
            // ⭐ CONFIGURACIÓN NOTAS INTERNAS
            // ========================================
            modelBuilder.Entity<NotaInterna>(entity =>
            {
                entity.HasOne(n => n.CreadoPor)
                    .WithMany()
                    .HasForeignKey(n => n.CreadoPorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(n => n.EditadoPor)
                    .WithMany()
                    .HasForeignKey(n => n.EditadoPorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========================================
            // ⭐ CONFIGURACIÓN TEMPLATES RESPUESTA
            // ========================================
            modelBuilder.Entity<TemplateRespuesta>(entity =>
            {
                entity.HasOne(t => t.CreadoPor)
                    .WithMany()
                    .HasForeignKey(t => t.CreadoPorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========================================
            // ⭐ CONFIGURACIÓN MODO MANTENIMIENTO
            // ========================================
            modelBuilder.Entity<ModoMantenimiento>(entity =>
            {
                entity.HasOne(m => m.ActivadoPor)
                    .WithMany()
                    .HasForeignKey(m => m.ActivadoPorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<HistorialMantenimiento>(entity =>
            {
                entity.HasOne(h => h.ActivadoPor)
                    .WithMany()
                    .HasForeignKey(h => h.ActivadoPorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(h => h.DesactivadoPor)
                    .WithMany()
                    .HasForeignKey(h => h.DesactivadoPorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========================================
            // ⭐ CONFIGURACIÓN AUDITORIA
            // ========================================
            modelBuilder.Entity<AuditoriaConfiguracion>(entity =>
            {
                entity.HasOne(a => a.ModificadoPor)
                    .WithMany()
                    .HasForeignKey(a => a.ModificadoPorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}