using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Lado.Models
{
    public class ApplicationUser : IdentityUser
    {
        // === INFORMACIÓN BÁSICA ===
        [Display(Name = "Nombre Completo")]
        [Required(ErrorMessage = "El nombre completo es obligatorio")]
        [StringLength(100)]
        public string NombreCompleto { get; set; } = string.Empty;

        [Display(Name = "Biografía")]
        [StringLength(500)]
        public string? Biografia { get; set; }

        [Display(Name = "Foto de Perfil")]
        public string? FotoPerfil { get; set; }

        [Display(Name = "Foto de Portada")]
        public string? FotoPortada { get; set; }

        // ========================================
        // SISTEMA DE IDENTIDAD DUAL (LADO A / LADO B)
        // ========================================

        // LADO B - Identidad Premium/Anónima
        [Display(Name = "Seudónimo")]
        [StringLength(50)]
        public string? Seudonimo { get; set; }

        [Display(Name = "Seudónimo Verificado")]
        public bool SeudonimoVerificado { get; set; } = false;

        [Display(Name = "Foto de Perfil LadoB")]
        public string? FotoPerfilLadoB { get; set; }

        [Display(Name = "Biografía LadoB")]
        [StringLength(500)]
        public string? BiografiaLadoB { get; set; }

        /// <summary>
        /// Si es true, usuarios que no siguen el LadoA no pueden ver el perfil LadoA.
        /// Protege la identidad real de creadores LadoB.
        /// </summary>
        [Display(Name = "Ocultar Identidad LadoA")]
        public bool OcultarIdentidadLadoA { get; set; } = false;

        // === TIPO DE USUARIO ===
        [Display(Name = "Tipo de Usuario")]
        public int TipoUsuario { get; set; } = 0; // 0 = Fan, 1 = Creador, 2 = Agencia (Admin se maneja por Roles)

        [Display(Name = "Es Creador")]
        public bool EsCreador { get; set; } = false;

        /// <summary>
        /// Indica si este usuario es administrado/virtual (controlado desde el panel de admin)
        /// </summary>
        [Display(Name = "Usuario Administrado")]
        public bool EsUsuarioAdministrado { get; set; } = false;

        // === INFORMACIÓN DE CREADOR ===
        [Display(Name = "Precio de Suscripción")]
        [Range(0, 999999)]
        public decimal PrecioSuscripcion { get; set; } = 9.99m;

        [Display(Name = "Precio de Suscripción LadoB")]
        [Range(0, 999999)]
        public decimal? PrecioSuscripcionLadoB { get; set; }

        [Display(Name = "Categoría")]
        [StringLength(50)]
        public string? Categoria { get; set; }

        [Display(Name = "Número de Seguidores")]
        public int NumeroSeguidores { get; set; } = 0;

        [Display(Name = "Visitas al Perfil")]
        public int VisitasPerfil { get; set; } = 0;

        // === FINANZAS ===
        [Display(Name = "Saldo Disponible")]
        public decimal Saldo { get; set; } = 0;

        [Display(Name = "Total de Ganancias")]
        public decimal TotalGanancias { get; set; } = 0;

        [Display(Name = "Crédito Publicitario")]
        public decimal SaldoPublicitario { get; set; } = 0;

        // ========================================
        // BOOST DE ALGORITMO
        // ========================================

        [Display(Name = "Boost Activo")]
        public bool BoostActivo { get; set; } = false;

        [Display(Name = "Crédito de Boost Restante")]
        public decimal BoostCredito { get; set; } = 0;

        [Display(Name = "Fecha de Vencimiento del Boost")]
        public DateTime? BoostFechaFin { get; set; }

        [Display(Name = "Multiplicador de Visibilidad")]
        public decimal BoostMultiplicador { get; set; } = 1.0m;

        // ========================================
        // CONFIGURACIÓN DE RETIROS (CREADORES)
        // ========================================

        [Display(Name = "Comisión de Retiro (%)")]
        [Range(0, 100)]
        public decimal ComisionRetiro { get; set; } = 20; // 20% por defecto

        [Display(Name = "Monto Mínimo de Retiro")]
        [Range(0, 999999)]
        public decimal MontoMinimoRetiro { get; set; } = 50; // $50 USD mínimo por defecto

        [Display(Name = "Moneda Preferida")]
        [StringLength(3)]
        public string MonedaPreferida { get; set; } = "USD";

        [Display(Name = "Cuenta de Retiro (PayPal/Banco)")]
        [StringLength(200)]
        public string? CuentaRetiro { get; set; }

        [Display(Name = "Tipo de Cuenta de Retiro")]
        [StringLength(50)]
        public string? TipoCuentaRetiro { get; set; } // PayPal, Transferencia, etc.

        [Display(Name = "Retención de Impuestos (%)")]
        [Range(0, 100)]
        public decimal? RetencionImpuestos { get; set; } // null = usar tasa del país

        [Display(Name = "Usar Retención del País")]
        public bool UsarRetencionPais { get; set; } = true; // true = usa la tasa del país, false = usa RetencionImpuestos

        // === ESTADO DE CUENTA ===
        [Display(Name = "Cuenta Activa")]
        public bool EstaActivo { get; set; } = true;

        // === SUSPENSIÓN TEMPORAL ===
        [Display(Name = "Fecha Fin Suspensión")]
        public DateTime? FechaSuspensionFin { get; set; }

        [Display(Name = "Razón Suspensión")]
        [StringLength(500)]
        public string? RazonSuspension { get; set; }

        [Display(Name = "Suspendido Por")]
        public string? SuspendidoPorId { get; set; }

        [Display(Name = "Fecha Suspensión")]
        public DateTime? FechaSuspension { get; set; }

        /// <summary>
        /// Verifica si el usuario está actualmente suspendido (temporal o permanente)
        /// </summary>
        public bool EstaSuspendido()
        {
            // Si no está activo, está suspendido permanentemente
            if (!EstaActivo) return true;

            // Si tiene fecha de fin de suspensión y aún no ha pasado, está suspendido
            if (FechaSuspensionFin.HasValue && FechaSuspensionFin.Value > DateTime.Now)
                return true;

            return false;
        }

        /// <summary>
        /// Obtiene el tiempo restante de suspensión temporal
        /// </summary>
        public TimeSpan? TiempoRestanteSuspension()
        {
            if (!FechaSuspensionFin.HasValue || FechaSuspensionFin.Value <= DateTime.Now)
                return null;

            return FechaSuspensionFin.Value - DateTime.Now;
        }

        [Display(Name = "Usuario Verificado")]
        public bool EsVerificado { get; set; } = false;

        /// <summary>
        /// Versión de seguridad - se incrementa al cerrar sesión o cambiar contraseña
        /// Invalida todos los tokens JWT emitidos antes del cambio
        /// </summary>
        [Display(Name = "Versión de Seguridad")]
        public int SecurityVersion { get; set; } = 1;

        [Display(Name = "Fecha de Registro")]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        // === RELACIONES ===
        public ICollection<Suscripcion> Suscripciones { get; set; } = new List<Suscripcion>();
        public ICollection<Suscripcion> Suscriptores { get; set; } = new List<Suscripcion>();

        // ========================================
        // VERIFICACIÓN DE EDAD
        // ========================================
        [Display(Name = "Fecha de Nacimiento")]
        public DateTime? FechaNacimiento { get; set; }

        [Display(Name = "País de Residencia")]
        [StringLength(5)]
        public string? Pais { get; set; }

        [Display(Name = "Ciudad")]
        [StringLength(100)]
        public string? Ciudad { get; set; }

        [Display(Name = "Genero")]
        public GeneroUsuario Genero { get; set; } = GeneroUsuario.NoEspecificado;

        [Display(Name = "Edad Verificada")]
        public bool AgeVerified { get; set; } = false;

        [Display(Name = "Fecha de Verificación de Edad")]
        public DateTime? AgeVerifiedDate { get; set; }

        // ========================================
        // VERIFICACIÓN DE IDENTIDAD (CREADORES)
        // ========================================
        [Display(Name = "Creador Verificado")]
        public bool CreadorVerificado { get; set; } = false;

        [Display(Name = "Fecha de Verificación de Identidad")]
        public DateTime? FechaVerificacion { get; set; }

        // ========================================
        // SISTEMA DE CONFIANZA
        // ========================================
        [Display(Name = "Última Actividad")]
        public DateTime? UltimaActividad { get; set; }

        [Display(Name = "Contador de Ingresos")]
        public int ContadorIngresos { get; set; } = 0;

        [Display(Name = "Mensajes Recibidos (total)")]
        public int MensajesRecibidosTotal { get; set; } = 0;

        [Display(Name = "Mensajes Respondidos (total)")]
        public int MensajesRespondidosTotal { get; set; } = 0;

        [Display(Name = "Tiempo Promedio de Respuesta (minutos)")]
        public int? TiempoPromedioRespuesta { get; set; }

        [Display(Name = "Reportes Recibidos")]
        public int ReportesRecibidos { get; set; } = 0;

        [Display(Name = "Contenidos Publicados")]
        public int ContenidosPublicados { get; set; } = 0;

        // ========================================
        // PUBLICIDAD Y AGENCIA
        // ========================================
        [Display(Name = "Permite Publicidad Personalizada")]
        public bool PermitePublicidadPersonalizada { get; set; } = true;

        // ========================================
        // PRIVACIDAD DE UBICACIÓN
        // ========================================
        /// <summary>
        /// Si es true, detecta ubicación automáticamente desde metadatos EXIF de las fotos
        /// </summary>
        [Display(Name = "Detectar Ubicación Automáticamente")]
        public bool DetectarUbicacionAutomaticamente { get; set; } = false;

        // ========================================
        // PREFERENCIAS DE IDIOMA Y ZONA HORARIA
        // ========================================
        [Display(Name = "Idioma Preferido")]
        [StringLength(5)]
        public string Idioma { get; set; } = "es"; // es = Español, en = English, pt = Português

        /// <summary>
        /// Zona horaria del usuario (formato IANA, ej: "America/Santiago", "America/Bogota")
        /// Se detecta automáticamente al registrarse y puede cambiarse en configuración.
        /// </summary>
        [Display(Name = "Zona Horaria")]
        [StringLength(50)]
        public string? ZonaHoraria { get; set; }

        // ========================================
        // PREFERENCIA DE LADO (A o B)
        // ========================================
        [Display(Name = "Lado Preferido")]
        public TipoLado LadoPreferido { get; set; } = TipoLado.LadoA; // LadoA = Público, LadoB = Premium

        /// <summary>
        /// Si es true, bloquea todo el contenido LadoB del feed y explorar.
        /// No verá usuarios con contenido premium ni su contenido.
        /// </summary>
        [Display(Name = "Bloquear Contenido Adulto (LadoB)")]
        public bool BloquearLadoB { get; set; } = false;

        /// <summary>
        /// Si es true, el contenido del usuario no aparecerá en el Feed Público.
        /// Solo usuarios autenticados podrán ver su contenido en el feed normal.
        /// </summary>
        [Display(Name = "Ocultar del Feed Público")]
        public bool OcultarDeFeedPublico { get; set; } = false;

        // ========================================
        // PROMOCIÓN DE LADOB DESDE LADOA
        // ========================================
        /// <summary>
        /// Muestra un banner "Tengo contenido exclusivo" en el perfil LadoA
        /// </summary>
        [Display(Name = "Mostrar Teaser LadoB en Perfil")]
        public bool MostrarTeaserLadoB { get; set; } = false;

        /// <summary>
        /// Permite publicar versiones censuradas del contenido LadoB en el feed LadoA
        /// </summary>
        [Display(Name = "Permitir Preview Blur de LadoB")]
        public bool PermitirPreviewBlurLadoB { get; set; } = false;

        // ========================================
        // PREFERENCIAS DE EMAIL
        // ========================================

        /// <summary>
        /// Si es true, el usuario recibe emails de marketing y promociones
        /// </summary>
        [Display(Name = "Recibir Emails de Marketing")]
        public bool RecibirEmailsMarketing { get; set; } = true;

        /// <summary>
        /// Si es true, el usuario recibe comunicados oficiales de la plataforma
        /// </summary>
        [Display(Name = "Recibir Comunicados")]
        public bool RecibirEmailsComunicados { get; set; } = true;

        // ========================================
        // PREFERENCIAS DE NOTIFICACIONES EMAIL
        // ========================================

        [Display(Name = "Email: Nuevos Mensajes")]
        public bool EmailNuevosMensajes { get; set; } = true;

        [Display(Name = "Email: Comentarios")]
        public bool EmailComentarios { get; set; } = true;

        [Display(Name = "Email: Menciones")]
        public bool EmailMenciones { get; set; } = true;

        [Display(Name = "Email: Nuevos Seguidores")]
        public bool EmailNuevosSeguidores { get; set; } = true;

        [Display(Name = "Email: Nuevas Suscripciones")]
        public bool EmailNuevasSuscripciones { get; set; } = true;

        [Display(Name = "Email: Propinas")]
        public bool EmailPropinas { get; set; } = true;

        [Display(Name = "Email: Nuevo Contenido")]
        public bool EmailNuevoContenido { get; set; } = false;

        [Display(Name = "Email: Stories")]
        public bool EmailStories { get; set; } = false;

        [Display(Name = "Email: Resumen Semanal")]
        public bool EmailResumenSemanal { get; set; } = true;

        [Display(Name = "Email: Reporte Ganancias")]
        public bool EmailReporteGanancias { get; set; } = true;

        [Display(Name = "Email: Consejos")]
        public bool EmailConsejos { get; set; } = false;

        // ========================================
        // SISTEMA LADO COINS (Dólares Premio)
        // ========================================

        /// <summary>
        /// Código único de referido para invitar otros usuarios.
        /// Se genera automáticamente al registrarse.
        /// </summary>
        [Display(Name = "Código de Referido")]
        [StringLength(20)]
        public string? CodigoReferido { get; set; }

        /// <summary>
        /// Si ya recibió el bono de bienvenida ($20)
        /// </summary>
        [Display(Name = "Bono Bienvenida Entregado")]
        public bool BonoBienvenidaEntregado { get; set; } = false;

        /// <summary>
        /// Si ya recibió el bono por primer contenido ($5)
        /// </summary>
        [Display(Name = "Bono Primer Contenido Entregado")]
        public bool BonoPrimerContenidoEntregado { get; set; } = false;

        /// <summary>
        /// Si ya recibió el bono por verificar email ($2)
        /// </summary>
        [Display(Name = "Bono Email Verificado Entregado")]
        public bool BonoEmailVerificadoEntregado { get; set; } = false;

        /// <summary>
        /// Si ya recibió el bono por completar perfil ($3)
        /// </summary>
        [Display(Name = "Bono Perfil Completo Entregado")]
        public bool BonoPerfilCompletoEntregado { get; set; } = false;

        /// <summary>
        /// Si el creador acepta pagos con LadoCoins.
        /// Obligatorio para creadores LadoB (siempre true).
        /// </summary>
        [Display(Name = "Acepta Pago con LadoCoins")]
        public bool AceptaLadoCoins { get; set; } = true;

        /// <summary>
        /// Porcentaje máximo de LadoCoins aceptado en suscripciones (0-30%).
        /// Para propinas se permite hasta 100%.
        /// </summary>
        [Display(Name = "Porcentaje Máximo LadoCoins Suscripción")]
        [Range(0, 30)]
        public int PorcentajeMaxLadoCoinsSuscripcion { get; set; } = 30;

        // Relación con LadoCoin (saldo)
        public virtual LadoCoin? LadoCoin { get; set; }

        // Relación con RachaUsuario
        public virtual RachaUsuario? Racha { get; set; }

        // Relación con Referidos (usuarios que invité)
        public virtual ICollection<Referido> MisReferidos { get; set; } = new List<Referido>();

        // ========================================
        // MÉTODOS HELPER LADO COINS
        // ========================================

        /// <summary>
        /// Verifica si el perfil está completo para el bono.
        /// Requiere: Foto, Bio, Nombre, País, Fecha nacimiento, Género
        /// </summary>
        public bool PerfilCompletoParaBono()
        {
            return !string.IsNullOrEmpty(FotoPerfil) &&
                   !string.IsNullOrEmpty(Biografia) &&
                   !string.IsNullOrEmpty(NombreCompleto) &&
                   !string.IsNullOrEmpty(Pais) &&
                   FechaNacimiento.HasValue &&
                   Genero != GeneroUsuario.NoEspecificado;
        }

        // ========================================
        // REDES SOCIALES
        // ========================================

        [Display(Name = "Instagram")]
        [StringLength(100)]
        public string? Instagram { get; set; }

        [Display(Name = "Twitter/X")]
        [StringLength(100)]
        public string? Twitter { get; set; }

        [Display(Name = "Facebook")]
        [StringLength(200)]
        public string? Facebook { get; set; }

        [Display(Name = "YouTube")]
        [StringLength(200)]
        public string? YouTube { get; set; }

        [Display(Name = "TikTok")]
        [StringLength(100)]
        public string? TikTok { get; set; }

        [Display(Name = "OnlyFans")]
        [StringLength(100)]
        public string? OnlyFans { get; set; }

        // ========================================
        // ESTADO EN LÍNEA Y PRIVACIDAD
        // ========================================

        /// <summary>
        /// Si es true, otros usuarios pueden ver cuando está en línea
        /// </summary>
        [Display(Name = "Mostrar Estado En Línea")]
        public bool MostrarEstadoEnLinea { get; set; } = true;

        // ========================================
        // CONTROLES DE PRIVACIDAD AVANZADOS
        // ========================================

        /// <summary>
        /// Si es true, el perfil es privado y requiere aprobación para seguir
        /// </summary>
        [Display(Name = "Perfil Privado")]
        public bool PerfilPrivado { get; set; } = false;

        /// <summary>
        /// Si es true, los usuarios pueden encontrar este perfil en búsquedas
        /// </summary>
        [Display(Name = "Mostrar en Búsquedas")]
        public bool MostrarEnBusquedas { get; set; } = true;

        /// <summary>
        /// Define quién puede enviar mensajes directos a este usuario
        /// </summary>
        [Display(Name = "Quién puede enviar mensajes")]
        public PermisoPrivacidad QuienPuedeMensajear { get; set; } = PermisoPrivacidad.Todos;

        /// <summary>
        /// Define quién puede comentar en las publicaciones de este usuario
        /// </summary>
        [Display(Name = "Quién puede comentar")]
        public PermisoPrivacidad QuienPuedeComentar { get; set; } = PermisoPrivacidad.Todos;

        /// <summary>
        /// Si es true, otros usuarios pueden ver la lista de seguidores
        /// </summary>
        [Display(Name = "Mostrar Seguidores")]
        public bool MostrarSeguidores { get; set; } = true;

        /// <summary>
        /// Si es true, otros usuarios pueden ver a quién sigue este usuario
        /// </summary>
        [Display(Name = "Mostrar Siguiendo")]
        public bool MostrarSiguiendo { get; set; } = true;

        /// <summary>
        /// Si es true, se permite etiquetar a este usuario en publicaciones
        /// </summary>
        [Display(Name = "Permitir Etiquetas")]
        public bool PermitirEtiquetas { get; set; } = true;

        /// <summary>
        /// Verifica si el usuario está en línea (actividad en los últimos 5 minutos)
        /// </summary>
        public bool EstaEnLinea => UltimaActividad.HasValue &&
            (DateTime.UtcNow - UltimaActividad.Value).TotalMinutes <= 5;

        /// <summary>
        /// Obtiene texto descriptivo de última conexión
        /// </summary>
        public string ObtenerUltimaConexionTexto()
        {
            if (!UltimaActividad.HasValue)
                return "Sin conexión reciente";

            var diferencia = DateTime.UtcNow - UltimaActividad.Value;

            if (diferencia.TotalMinutes <= 5)
                return "En línea";
            if (diferencia.TotalMinutes < 60)
                return $"Hace {(int)diferencia.TotalMinutes} min";
            if (diferencia.TotalHours < 24)
                return $"Hace {(int)diferencia.TotalHours}h";
            if (diferencia.TotalDays < 7)
                return $"Hace {(int)diferencia.TotalDays} días";

            return "Hace más de una semana";
        }

        // Relacion con Agencia (si TipoUsuario == 2)
        public virtual Agencia? Agencia { get; set; }

        // Relacion con Intereses
        public virtual ICollection<InteresUsuario> Intereses { get; set; } = new List<InteresUsuario>();

        // Relacion con Estadísticas de Desafíos
        public virtual EstadisticasDesafiosUsuario? EstadisticasDesafios { get; set; }

        // Relacion con Badges de Desafíos
        public virtual ICollection<BadgeUsuario> BadgesDesafios { get; set; } = new List<BadgeUsuario>();

        // ========================================
        // MÉTODOS HELPER
        // ========================================

        /// <summary>
        /// Obtiene el nombre de visualización según el contexto y el tipo de lado.
        /// </summary>
        /// <param name="usarLadoB">Si true, usa la identidad de LadoB (seudónimo)</param>
        /// <returns>El nombre a mostrar</returns>
        public string ObtenerNombreDisplay(bool usarLadoB = false)
        {
            if (usarLadoB && !string.IsNullOrEmpty(Seudonimo))
            {
                return Seudonimo;
            }
            return NombreCompleto;
        }

        /// <summary>
        /// Obtiene la foto de perfil según el contexto.
        /// </summary>
        /// <param name="usarLadoB">Si true, usa la foto de LadoB</param>
        /// <returns>La ruta de la foto o null</returns>
        public string? ObtenerFotoPerfil(bool usarLadoB = false)
        {
            if (usarLadoB && !string.IsNullOrEmpty(FotoPerfilLadoB))
            {
                return FotoPerfilLadoB;
            }
            return FotoPerfil;
        }

        /// <summary>
        /// Obtiene la biografía según el contexto.
        /// </summary>
        /// <param name="usarLadoB">Si true, usa la biografía de LadoB</param>
        /// <returns>La biografía o null</returns>
        public string? ObtenerBiografia(bool usarLadoB = false)
        {
            if (usarLadoB && !string.IsNullOrEmpty(BiografiaLadoB))
            {
                return BiografiaLadoB;
            }
            return Biografia;
        }

        /// <summary>
        /// Verifica si tiene configurada la identidad de LadoB (creador premium).
        /// Un usuario es LadoB si es CREADOR, está VERIFICADO y tiene un seudónimo configurado.
        /// </summary>
        /// <returns>True si es creador premium verificado (EsCreador + CreadorVerificado + tiene seudónimo)</returns>
        public bool TieneLadoB()
        {
            return EsCreador && CreadorVerificado && !string.IsNullOrEmpty(Seudonimo);
        }

        /// <summary>
        /// Valida si el usuario tiene la edad mínima requerida
        /// </summary>
        /// <param name="edadMinima">Edad mínima a validar (por defecto 18)</param>
        /// <returns>True si cumple con la edad mínima</returns>
        public bool TieneEdadMinima(int edadMinima = 18)
        {
            if (!FechaNacimiento.HasValue) return false;

            var edad = DateTime.Now.Year - FechaNacimiento.Value.Year;

            if (FechaNacimiento.Value.Date > DateTime.Now.AddYears(-edad))
                edad--;

            return edad >= edadMinima;
        }

        /// <summary>
        /// Calcula la edad actual del usuario
        /// </summary>
        /// <returns>Edad en años o null si no hay fecha de nacimiento</returns>
        public int? ObtenerEdad()
        {
            if (!FechaNacimiento.HasValue) return null;

            var edad = DateTime.Now.Year - FechaNacimiento.Value.Year;

            if (FechaNacimiento.Value.Date > DateTime.Now.AddYears(-edad))
                edad--;

            return edad;
        }

        /// <summary>
        /// Verifica si el usuario es un creador activo y verificado
        /// </summary>
        public bool EsCreadorActivo()
        {
            return TipoUsuario == 1 && EsCreador && EstaActivo;
        }

        /// <summary>
        /// Verifica si el usuario puede publicar contenido premium
        /// </summary>
        public bool PuedePublicarPremium()
        {
            return EsCreadorActivo() && CreadorVerificado;
        }

        /// <summary>
        /// Obtiene el nombre público (con seudónimo si está disponible)
        /// </summary>
        public string NombrePublico => !string.IsNullOrEmpty(Seudonimo) ? Seudonimo : NombreCompleto;

        /// <summary>
        /// Verifica si tiene verificación completa (edad + identidad si es creador)
        /// </summary>
        public bool TieneVerificacionCompleta()
        {
            if (!AgeVerified) return false;

            if (TipoUsuario == 1)
                return CreadorVerificado;

            return true;
        }

        /// <summary>
        /// Obtiene la inicial del nombre para mostrar en avatares.
        /// </summary>
        /// <param name="usarLadoB">Si true, usa la inicial del seudónimo</param>
        /// <returns>La primera letra del nombre/seudónimo</returns>
        public string ObtenerInicial(bool usarLadoB = false)
        {
            if (usarLadoB && !string.IsNullOrEmpty(Seudonimo) && Seudonimo.Length > 0)
            {
                return Seudonimo.Substring(0, 1).ToUpper();
            }
            if (!string.IsNullOrEmpty(NombreCompleto) && NombreCompleto.Length > 0)
            {
                return NombreCompleto.Substring(0, 1).ToUpper();
            }
            return "U"; // Default si no hay nombre
        }

        // ========================================
        // MÉTODOS DE CONFIANZA
        // ========================================

        /// <summary>
        /// Calcula la tasa de respuesta a mensajes (0-100%)
        /// </summary>
        public int ObtenerTasaRespuesta()
        {
            if (MensajesRecibidosTotal == 0) return 0;
            return (int)Math.Round((double)MensajesRespondidosTotal / MensajesRecibidosTotal * 100);
        }

        /// <summary>
        /// Determina si el creador está activo (actividad en últimas 48h)
        /// </summary>
        public bool EstaActivoReciente()
        {
            if (!UltimaActividad.HasValue) return false;
            return (DateTime.Now - UltimaActividad.Value).TotalHours <= 48;
        }

        /// <summary>
        /// Obtiene el nivel de confianza (0-5 estrellas)
        /// </summary>
        public int ObtenerNivelConfianza()
        {
            int nivel = 0;

            // +1 por verificación de identidad
            if (CreadorVerificado) nivel++;

            // +1 por verificación de edad
            if (AgeVerified) nivel++;

            // +1 por tasa de respuesta > 70%
            if (ObtenerTasaRespuesta() >= 70) nivel++;

            // +1 por estar activo recientemente
            if (EstaActivoReciente()) nivel++;

            // +1 por tener contenido (más de 5 publicaciones)
            if (ContenidosPublicados >= 5) nivel++;

            return nivel;
        }

        /// <summary>
        /// Obtiene texto descriptivo del tiempo de respuesta
        /// </summary>
        public string ObtenerTextoTiempoRespuesta()
        {
            if (!TiempoPromedioRespuesta.HasValue || TiempoPromedioRespuesta == 0)
                return "Sin datos";

            var minutos = TiempoPromedioRespuesta.Value;

            if (minutos < 60)
                return $"~{minutos} min";
            if (minutos < 1440) // menos de 24h
                return $"~{minutos / 60}h";
            return $"~{minutos / 1440}d";
        }

        /// <summary>
        /// Obtiene el estado de actividad como texto
        /// </summary>
        public string ObtenerEstadoActividad()
        {
            if (!UltimaActividad.HasValue)
                return "Sin actividad";

            var diferencia = DateTime.Now - UltimaActividad.Value;

            if (diferencia.TotalMinutes < 5)
                return "En línea";
            if (diferencia.TotalHours < 1)
                return $"Hace {(int)diferencia.TotalMinutes} min";
            if (diferencia.TotalHours < 24)
                return $"Hace {(int)diferencia.TotalHours}h";
            if (diferencia.TotalDays < 7)
                return $"Hace {(int)diferencia.TotalDays}d";
            return "Hace más de 1 semana";
        }
    }
}
