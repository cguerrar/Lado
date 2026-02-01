using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Lado.Services
{
    public interface IBienestarCreadorService
    {
        // Modo Vacaciones
        Task<ConfiguracionVacaciones?> ObtenerConfiguracionVacacionesAsync(string creadorId);
        Task<bool> ActivarModoVacacionesAsync(string creadorId, DateTime fechaInicio, DateTime fechaFin, string? mensajeAutorespuesta, string? mensajePerfilPublico);
        Task<bool> DesactivarModoVacacionesAsync(string creadorId);
        Task<string?> ObtenerAutorespuestaAsync(string creadorId);

        // Analisis de Burnout
        Task<List<AlertaBienestar>> AnalizarPatronesBurnoutAsync(string creadorId);

        // Celebraciones y Logros
        Task<List<Celebracion>> GenerarCelebracionesAsync(string creadorId);
        Task MarcarLogroVistoAsync(int logroId);

        // Proyeccion de Ingresos
        Task<ProyeccionIngresos> CalcularProyeccionIngresosAsync(string creadorId);

        // Metricas de Bienestar
        Task<MetricasBienestar> ObtenerMetricasBienestarAsync(string creadorId);

        // Contenido Programado
        Task<List<ContenidoProgramado>> ObtenerContenidoProgramadoAsync(string creadorId);
        Task<bool> ProgramarContenidoAsync(string creadorId, int contenidoBorradorId, DateTime fechaProgramada);
        Task<bool> CancelarContenidoProgramadoAsync(int programacionId, string creadorId);
    }

    public class BienestarCreadorService : IBienestarCreadorService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<BienestarCreadorService> _logger;
        private readonly ILogEventoService _logEventoService;

        public BienestarCreadorService(
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<BienestarCreadorService> logger,
            ILogEventoService logEventoService)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _logEventoService = logEventoService;
        }

        #region Modo Vacaciones

        public async Task<ConfiguracionVacaciones?> ObtenerConfiguracionVacacionesAsync(string creadorId)
        {
            return await _context.ConfiguracionesVacaciones
                .FirstOrDefaultAsync(c => c.CreadorId == creadorId);
        }

        public async Task<bool> ActivarModoVacacionesAsync(
            string creadorId,
            DateTime fechaInicio,
            DateTime fechaFin,
            string? mensajeAutorespuesta,
            string? mensajePerfilPublico)
        {
            try
            {
                var config = await _context.ConfiguracionesVacaciones
                    .FirstOrDefaultAsync(c => c.CreadorId == creadorId);

                if (config == null)
                {
                    config = new ConfiguracionVacaciones
                    {
                        CreadorId = creadorId,
                        EstaActivo = true,
                        FechaInicio = fechaInicio,
                        FechaFin = fechaFin,
                        MensajeAutorespuesta = mensajeAutorespuesta ?? "Estoy de vacaciones y respondere pronto. Gracias por tu paciencia.",
                        MensajePerfilPublico = mensajePerfilPublico ?? "De vacaciones, vuelvo pronto",
                        AutoResponderMensajes = true,
                        ProtegerSuscriptores = true
                    };
                    _context.ConfiguracionesVacaciones.Add(config);
                }
                else
                {
                    config.EstaActivo = true;
                    config.FechaInicio = fechaInicio;
                    config.FechaFin = fechaFin;
                    config.MensajeAutorespuesta = mensajeAutorespuesta ?? config.MensajeAutorespuesta;
                    config.MensajePerfilPublico = mensajePerfilPublico ?? config.MensajePerfilPublico;
                    config.FechaModificacion = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                var creador = await _context.Users.FindAsync(creadorId);
                await _logEventoService.RegistrarEventoAsync(
                    $"Modo vacaciones activado del {fechaInicio:dd/MM} al {fechaFin:dd/MM}",
                    CategoriaEvento.Usuario,
                    TipoLogEvento.Evento,
                    creadorId,
                    creador?.NombreCompleto);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activando modo vacaciones para {CreadorId}", creadorId);
                return false;
            }
        }

        public async Task<bool> DesactivarModoVacacionesAsync(string creadorId)
        {
            try
            {
                var config = await _context.ConfiguracionesVacaciones
                    .FirstOrDefaultAsync(c => c.CreadorId == creadorId);

                if (config != null)
                {
                    config.EstaActivo = false;
                    config.FechaModificacion = DateTime.Now;
                    await _context.SaveChangesAsync();

                    var creador = await _context.Users.FindAsync(creadorId);
                    await _logEventoService.RegistrarEventoAsync(
                        "Modo vacaciones desactivado",
                        CategoriaEvento.Usuario,
                        TipoLogEvento.Evento,
                        creadorId,
                        creador?.NombreCompleto);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error desactivando modo vacaciones para {CreadorId}", creadorId);
                return false;
            }
        }

        public async Task<string?> ObtenerAutorespuestaAsync(string creadorId)
        {
            var config = await _context.ConfiguracionesVacaciones
                .Where(c => c.CreadorId == creadorId && c.EstaActivo && c.AutoResponderMensajes)
                .FirstOrDefaultAsync();

            if (config == null) return null;

            // Verificar que estamos dentro del rango de fechas
            var ahora = DateTime.Now;
            if (config.FechaInicio.HasValue && config.FechaFin.HasValue)
            {
                if (ahora >= config.FechaInicio.Value && ahora <= config.FechaFin.Value)
                {
                    return config.MensajeAutorespuesta;
                }
            }

            return null;
        }

        #endregion

        #region Analisis de Burnout

        public async Task<List<AlertaBienestar>> AnalizarPatronesBurnoutAsync(string creadorId)
        {
            var alertas = new List<AlertaBienestar>();
            var ahora = DateTime.Now;
            var hace30Dias = ahora.AddDays(-30);
            var hace7Dias = ahora.AddDays(-7);

            try
            {
                // 1. Verificar dias consecutivos publicando
                var diasConPublicacion = await _context.Contenidos
                    .Where(c => c.UsuarioId == creadorId && c.FechaPublicacion >= hace30Dias && !c.EsBorrador)
                    .Select(c => c.FechaPublicacion.Date)
                    .Distinct()
                    .OrderByDescending(d => d)
                    .ToListAsync();

                int diasConsecutivos = 0;
                var fechaActual = DateTime.Today;
                foreach (var fecha in diasConPublicacion)
                {
                    if (fecha == fechaActual)
                    {
                        diasConsecutivos++;
                        fechaActual = fechaActual.AddDays(-1);
                    }
                    else
                    {
                        break;
                    }
                }

                if (diasConsecutivos >= 14)
                {
                    alertas.Add(new AlertaBienestar
                    {
                        Tipo = TipoAlertaBienestar.PublicacionExcesiva,
                        Titulo = "Llevas muchos dias seguidos publicando",
                        Mensaje = $"Has publicado durante {diasConsecutivos} dias seguidos. Tomarte un descanso puede ayudarte a recargar energia y crear contenido de mejor calidad.",
                        Icono = "coffee",
                        ColorClase = "warning",
                        Severidad = diasConsecutivos >= 21 ? 3 : 2,
                        Sugerencia = "Considera activar el modo vacaciones por unos dias"
                    });
                }
                else if (diasConsecutivos >= 7)
                {
                    alertas.Add(new AlertaBienestar
                    {
                        Tipo = TipoAlertaBienestar.PublicacionExcesiva,
                        Titulo = "Una semana de creacion continua",
                        Mensaje = $"Llevas {diasConsecutivos} dias publicando. Recuerda que descansar tambien es importante.",
                        Icono = "smile",
                        ColorClase = "info",
                        Severidad = 1,
                        Sugerencia = "Un dia de descanso puede renovar tu creatividad"
                    });
                }

                // 2. Verificar horarios de publicacion (publicaciones muy tarde en la noche)
                var publicacionesNocturnas = await _context.Contenidos
                    .Where(c => c.UsuarioId == creadorId &&
                           c.FechaPublicacion >= hace7Dias &&
                           !c.EsBorrador &&
                           (c.FechaPublicacion.Hour >= 23 || c.FechaPublicacion.Hour < 5))
                    .CountAsync();

                if (publicacionesNocturnas >= 5)
                {
                    alertas.Add(new AlertaBienestar
                    {
                        Tipo = TipoAlertaBienestar.HorariosIrregulares,
                        Titulo = "Muchas publicaciones nocturnas",
                        Mensaje = $"Has publicado {publicacionesNocturnas} veces de madrugada esta semana. Un buen descanso te ayuda a crear mejor contenido.",
                        Icono = "moon",
                        ColorClase = "warning",
                        Severidad = 2,
                        Sugerencia = "Intenta programar tus publicaciones para el dia siguiente"
                    });
                }

                // 3. Verificar volumen de trabajo (muchos contenidos en poco tiempo)
                var contenidosSemana = await _context.Contenidos
                    .CountAsync(c => c.UsuarioId == creadorId && c.FechaPublicacion >= hace7Dias && !c.EsBorrador);

                if (contenidosSemana >= 21) // 3 por dia
                {
                    alertas.Add(new AlertaBienestar
                    {
                        Tipo = TipoAlertaBienestar.VolumenAlto,
                        Titulo = "Ritmo de publicacion muy alto",
                        Mensaje = $"Has publicado {contenidosSemana} contenidos esta semana. La calidad a veces importa mas que la cantidad.",
                        Icono = "zap",
                        ColorClase = "warning",
                        Severidad = 2,
                        Sugerencia = "Considera espaciar tus publicaciones"
                    });
                }

                // 4. Verificar tiempo sin descanso
                var ultimoDescanso = diasConPublicacion.Count > 0
                    ? diasConPublicacion.Where(d => d < DateTime.Today.AddDays(-1))
                        .OrderByDescending(d => d)
                        .FirstOrDefault()
                    : DateTime.Today;

                var diasSinDescanso = (DateTime.Today - ultimoDescanso).Days;
                if (diasSinDescanso == 0 && diasConPublicacion.Count >= 7)
                {
                    // Calculamos cuantos dias van sin que falte un dia de publicacion
                    var ultimaLaguna = DateTime.Today;
                    for (int i = 0; i < diasConPublicacion.Count - 1; i++)
                    {
                        if ((diasConPublicacion[i] - diasConPublicacion[i + 1]).Days > 1)
                        {
                            diasSinDescanso = (DateTime.Today - diasConPublicacion[i]).Days;
                            break;
                        }
                    }
                }

                // 5. Si no hay alertas, agregar mensaje positivo
                if (!alertas.Any())
                {
                    alertas.Add(new AlertaBienestar
                    {
                        Tipo = TipoAlertaBienestar.TodoBien,
                        Titulo = "Tu ritmo se ve saludable",
                        Mensaje = "Estas manteniendo un buen balance entre creacion y descanso. Sigue asi.",
                        Icono = "heart",
                        ColorClase = "success",
                        Severidad = 0,
                        Sugerencia = null
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analizando patrones de burnout para {CreadorId}", creadorId);
            }

            return alertas.OrderByDescending(a => a.Severidad).ToList();
        }

        #endregion

        #region Celebraciones

        public async Task<List<Celebracion>> GenerarCelebracionesAsync(string creadorId)
        {
            var celebraciones = new List<Celebracion>();

            try
            {
                var creador = await _context.Users.FindAsync(creadorId);
                if (creador == null) return celebraciones;

                // Obtener logros ya celebrados
                var logrosCelebrados = await _context.LogrosCelebrados
                    .Where(l => l.CreadorId == creadorId)
                    .Select(l => new { l.TipoLogro, l.ValorHito })
                    .ToListAsync();

                // Contar metricas actuales
                var totalSuscriptores = await _context.Suscripciones
                    .CountAsync(s => s.CreadorId == creadorId && s.EstaActiva);

                var totalContenidos = await _context.Contenidos
                    .CountAsync(c => c.UsuarioId == creadorId && !c.EsBorrador && c.EstaActivo);

                var totalPropinas = await _context.Tips
                    .Where(t => t.CreadorId == creadorId)
                    .SumAsync(t => (decimal?)t.Monto) ?? 0;

                var totalIngresos = await _context.Transacciones
                    .Where(t => t.UsuarioId == creadorId &&
                           t.TipoTransaccion != TipoTransaccion.Retiro &&
                           t.EstadoTransaccion == EstadoTransaccion.Completada)
                    .SumAsync(t => (decimal?)t.Monto) ?? 0;

                var diasEnPlataforma = (DateTime.Now - creador.FechaRegistro).Days;

                // Verificar hitos de suscriptores
                var hitosSuscriptores = new[] { 1, 10, 50, 100, 500, 1000, 5000, 10000 };
                foreach (var hito in hitosSuscriptores)
                {
                    if (totalSuscriptores >= hito)
                    {
                        var tipoLogro = hito switch
                        {
                            1 => TipoLogro.PrimerSuscriptor,
                            10 => TipoLogro.Suscriptores10,
                            50 => TipoLogro.Suscriptores50,
                            100 => TipoLogro.Suscriptores100,
                            500 => TipoLogro.Suscriptores500,
                            _ => TipoLogro.Suscriptores1000
                        };

                        if (!logrosCelebrados.Any(l => l.TipoLogro == tipoLogro))
                        {
                            var logro = new LogroCelebrado
                            {
                                CreadorId = creadorId,
                                TipoLogro = tipoLogro,
                                ValorHito = hito
                            };
                            _context.LogrosCelebrados.Add(logro);

                            celebraciones.Add(new Celebracion
                            {
                                LogroId = 0, // Se actualizara despues del save
                                Titulo = hito == 1 ? "Tu primer suscriptor" : $"{hito} suscriptores",
                                Mensaje = hito == 1
                                    ? "Felicidades, alguien confio en tu contenido. Este es el comienzo de algo grande."
                                    : $"Increible, ya tienes {hito} suscriptores. Tu comunidad esta creciendo.",
                                Icono = "users",
                                ColorClase = "primary",
                                EsNuevo = true
                            });
                        }
                    }
                }

                // Verificar hitos de ingresos
                var hitosIngresos = new[] { 100, 500, 1000, 5000, 10000 };
                foreach (var hito in hitosIngresos)
                {
                    if (totalIngresos >= hito)
                    {
                        var tipoLogro = hito switch
                        {
                            100 => TipoLogro.Ingresos100,
                            500 => TipoLogro.Ingresos500,
                            1000 => TipoLogro.Ingresos1000,
                            5000 => TipoLogro.Ingresos5000,
                            _ => TipoLogro.Ingresos10000
                        };

                        if (!logrosCelebrados.Any(l => l.TipoLogro == tipoLogro))
                        {
                            var logro = new LogroCelebrado
                            {
                                CreadorId = creadorId,
                                TipoLogro = tipoLogro,
                                ValorHito = hito
                            };
                            _context.LogrosCelebrados.Add(logro);

                            celebraciones.Add(new Celebracion
                            {
                                LogroId = 0,
                                Titulo = $"${hito} en ganancias",
                                Mensaje = $"Has generado ${hito} en la plataforma. Tu trabajo esta dando frutos.",
                                Icono = "dollar-sign",
                                ColorClase = "success",
                                EsNuevo = true
                            });
                        }
                    }
                }

                // Verificar aniversario
                if (diasEnPlataforma >= 365)
                {
                    if (!logrosCelebrados.Any(l => l.TipoLogro == TipoLogro.Aniversario1Ano))
                    {
                        var logro = new LogroCelebrado
                        {
                            CreadorId = creadorId,
                            TipoLogro = TipoLogro.Aniversario1Ano,
                            ValorHito = 1
                        };
                        _context.LogrosCelebrados.Add(logro);

                        celebraciones.Add(new Celebracion
                        {
                            LogroId = 0,
                            Titulo = "1 ano en la plataforma",
                            Mensaje = "Felicidades por un ano de creacion. Tu dedicacion es admirable.",
                            Icono = "award",
                            ColorClase = "accent",
                            EsNuevo = true
                        });
                    }
                }
                else if (diasEnPlataforma >= 30)
                {
                    if (!logrosCelebrados.Any(l => l.TipoLogro == TipoLogro.PrimerMes))
                    {
                        var logro = new LogroCelebrado
                        {
                            CreadorId = creadorId,
                            TipoLogro = TipoLogro.PrimerMes,
                            ValorHito = 1
                        };
                        _context.LogrosCelebrados.Add(logro);

                        celebraciones.Add(new Celebracion
                        {
                            LogroId = 0,
                            Titulo = "Primer mes completado",
                            Mensaje = "Ya llevas un mes en la plataforma. Sigue adelante.",
                            Icono = "calendar",
                            ColorClase = "info",
                            EsNuevo = true
                        });
                    }
                }

                // Verificar primer contenido
                if (totalContenidos >= 1 && !logrosCelebrados.Any(l => l.TipoLogro == TipoLogro.PrimerContenido))
                {
                    var logro = new LogroCelebrado
                    {
                        CreadorId = creadorId,
                        TipoLogro = TipoLogro.PrimerContenido,
                        ValorHito = 1
                    };
                    _context.LogrosCelebrados.Add(logro);

                    celebraciones.Add(new Celebracion
                    {
                        LogroId = 0,
                        Titulo = "Tu primera publicacion",
                        Mensaje = "Diste el primer paso. Ahora sigue creando y conectando con tu audiencia.",
                        Icono = "edit",
                        ColorClase = "primary",
                        EsNuevo = true
                    });
                }

                if (celebraciones.Any())
                {
                    await _context.SaveChangesAsync();
                }

                // Cargar celebraciones pendientes de ver
                var logrosPendientes = await _context.LogrosCelebrados
                    .Where(l => l.CreadorId == creadorId && !l.Visto)
                    .OrderByDescending(l => l.FechaLogro)
                    .ToListAsync();

                foreach (var logro in logrosPendientes)
                {
                    if (!celebraciones.Any(c => c.LogroId == logro.Id))
                    {
                        celebraciones.Add(new Celebracion
                        {
                            LogroId = logro.Id,
                            Titulo = ObtenerTituloLogro(logro.TipoLogro, logro.ValorHito),
                            Mensaje = ObtenerMensajeLogro(logro.TipoLogro, logro.ValorHito),
                            Icono = ObtenerIconoLogro(logro.TipoLogro),
                            ColorClase = ObtenerColorLogro(logro.TipoLogro),
                            EsNuevo = true,
                            FechaLogro = logro.FechaLogro
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando celebraciones para {CreadorId}", creadorId);
            }

            return celebraciones.OrderByDescending(c => c.EsNuevo).ThenByDescending(c => c.FechaLogro).ToList();
        }

        public async Task MarcarLogroVistoAsync(int logroId)
        {
            var logro = await _context.LogrosCelebrados.FindAsync(logroId);
            if (logro != null)
            {
                logro.Visto = true;
                await _context.SaveChangesAsync();
            }
        }

        private string ObtenerTituloLogro(TipoLogro tipo, int valor)
        {
            return tipo switch
            {
                TipoLogro.PrimerSuscriptor => "Tu primer suscriptor",
                TipoLogro.Suscriptores10 => "10 suscriptores",
                TipoLogro.Suscriptores50 => "50 suscriptores",
                TipoLogro.Suscriptores100 => "100 suscriptores",
                TipoLogro.Suscriptores500 => "500 suscriptores",
                TipoLogro.Suscriptores1000 => "1000 suscriptores",
                TipoLogro.Ingresos100 => "$100 en ganancias",
                TipoLogro.Ingresos500 => "$500 en ganancias",
                TipoLogro.Ingresos1000 => "$1,000 en ganancias",
                TipoLogro.Ingresos5000 => "$5,000 en ganancias",
                TipoLogro.Ingresos10000 => "$10,000 en ganancias",
                TipoLogro.PrimerMes => "Primer mes completado",
                TipoLogro.Aniversario1Ano => "1 ano en la plataforma",
                TipoLogro.PrimerContenido => "Tu primera publicacion",
                _ => "Logro alcanzado"
            };
        }

        private string ObtenerMensajeLogro(TipoLogro tipo, int valor)
        {
            return tipo switch
            {
                TipoLogro.PrimerSuscriptor => "Felicidades, alguien confio en tu contenido.",
                TipoLogro.Suscriptores100 => "Tu comunidad crece. Sigue asi.",
                TipoLogro.Ingresos1000 => "Tu trabajo esta dando frutos.",
                TipoLogro.Aniversario1Ano => "Un ano de dedicacion. Admirable.",
                _ => "Sigue adelante, vas muy bien."
            };
        }

        private string ObtenerIconoLogro(TipoLogro tipo)
        {
            return tipo switch
            {
                TipoLogro.PrimerSuscriptor or TipoLogro.Suscriptores10 or
                TipoLogro.Suscriptores50 or TipoLogro.Suscriptores100 or
                TipoLogro.Suscriptores500 or TipoLogro.Suscriptores1000 => "users",
                TipoLogro.Ingresos100 or TipoLogro.Ingresos500 or
                TipoLogro.Ingresos1000 or TipoLogro.Ingresos5000 or
                TipoLogro.Ingresos10000 => "dollar-sign",
                TipoLogro.PrimerMes or TipoLogro.Aniversario1Ano => "award",
                _ => "star"
            };
        }

        private string ObtenerColorLogro(TipoLogro tipo)
        {
            return tipo switch
            {
                TipoLogro.Ingresos100 or TipoLogro.Ingresos500 or
                TipoLogro.Ingresos1000 or TipoLogro.Ingresos5000 or
                TipoLogro.Ingresos10000 => "success",
                TipoLogro.Aniversario1Ano => "accent",
                _ => "primary"
            };
        }

        #endregion

        #region Proyeccion de Ingresos

        public async Task<ProyeccionIngresos> CalcularProyeccionIngresosAsync(string creadorId)
        {
            var proyeccion = new ProyeccionIngresos();
            var ahora = DateTime.Now;
            var hace30Dias = ahora.AddDays(-30);
            var hace60Dias = ahora.AddDays(-60);
            var hace90Dias = ahora.AddDays(-90);

            try
            {
                // 1. Suscriptores actuales y su valor
                var suscripcionesActivas = await _context.Suscripciones
                    .Where(s => s.CreadorId == creadorId && s.EstaActiva)
                    .ToListAsync();

                proyeccion.SuscriptoresActuales = suscripcionesActivas.Count;
                proyeccion.IngresoSuscripcionesActual = suscripcionesActivas.Sum(s => s.PrecioMensual);

                // 2. Calcular churn rate (tasa de cancelacion)
                var cancelaciones30Dias = await _context.Suscripciones
                    .CountAsync(s => s.CreadorId == creadorId &&
                               s.FechaCancelacion >= hace30Dias &&
                               s.FechaCancelacion <= ahora);

                var suscriptoresInicio = await _context.Suscripciones
                    .CountAsync(s => s.CreadorId == creadorId &&
                               s.FechaInicio <= hace30Dias &&
                               (s.EstaActiva || s.FechaCancelacion >= hace30Dias));

                proyeccion.ChurnRate = suscriptoresInicio > 0
                    ? Math.Round((decimal)cancelaciones30Dias / suscriptoresInicio * 100, 1)
                    : 0;

                // 3. Nuevos suscriptores por mes (promedio)
                var nuevosSuscriptores30Dias = await _context.Suscripciones
                    .CountAsync(s => s.CreadorId == creadorId && s.FechaInicio >= hace30Dias);
                var nuevosSuscriptores60Dias = await _context.Suscripciones
                    .CountAsync(s => s.CreadorId == creadorId && s.FechaInicio >= hace60Dias && s.FechaInicio < hace30Dias);
                var nuevosSuscriptores90Dias = await _context.Suscripciones
                    .CountAsync(s => s.CreadorId == creadorId && s.FechaInicio >= hace90Dias && s.FechaInicio < hace60Dias);

                proyeccion.NuevosSuscriptoresMesAnterior = nuevosSuscriptores30Dias;
                var promedioNuevos = (nuevosSuscriptores30Dias + nuevosSuscriptores60Dias + nuevosSuscriptores90Dias) / 3.0;

                // 4. Propinas promedio
                var propinas30Dias = await _context.Tips
                    .Where(t => t.CreadorId == creadorId && t.FechaEnvio >= hace30Dias)
                    .SumAsync(t => (decimal?)t.Monto) ?? 0;
                var propinas60Dias = await _context.Tips
                    .Where(t => t.CreadorId == creadorId && t.FechaEnvio >= hace60Dias && t.FechaEnvio < hace30Dias)
                    .SumAsync(t => (decimal?)t.Monto) ?? 0;
                var propinas90Dias = await _context.Tips
                    .Where(t => t.CreadorId == creadorId && t.FechaEnvio >= hace90Dias && t.FechaEnvio < hace60Dias)
                    .SumAsync(t => (decimal?)t.Monto) ?? 0;

                proyeccion.PropinasMesActual = propinas30Dias;
                proyeccion.PropinasPromedio = (propinas30Dias + propinas60Dias + propinas90Dias) / 3;

                // 5. Calcular proyeccion del proximo mes
                // Suscriptores proyectados = actuales - (actuales * churn%) + nuevos promedio
                var churnProyectado = (int)Math.Round(proyeccion.SuscriptoresActuales * (proyeccion.ChurnRate / 100));
                var suscriptoresProyectados = proyeccion.SuscriptoresActuales - churnProyectado + (int)Math.Round(promedioNuevos);
                proyeccion.SuscriptoresProyectados = Math.Max(0, suscriptoresProyectados);

                // Precio promedio de suscripcion
                var precioPromedio = suscripcionesActivas.Any()
                    ? suscripcionesActivas.Average(s => s.PrecioMensual)
                    : 9.99m;

                // Ingreso proyectado = suscriptores proyectados * precio promedio + propinas promedio
                proyeccion.IngresoSuscripcionesProyectado = proyeccion.SuscriptoresProyectados * precioPromedio;
                proyeccion.IngresoProyectadoTotal = proyeccion.IngresoSuscripcionesProyectado + proyeccion.PropinasPromedio;

                // 6. Calcular tendencia
                var ingresosMes1 = await _context.Transacciones
                    .Where(t => t.UsuarioId == creadorId &&
                           t.FechaTransaccion >= hace30Dias &&
                           t.TipoTransaccion != TipoTransaccion.Retiro &&
                           t.EstadoTransaccion == EstadoTransaccion.Completada)
                    .SumAsync(t => (decimal?)t.Monto) ?? 0;

                var ingresosMes2 = await _context.Transacciones
                    .Where(t => t.UsuarioId == creadorId &&
                           t.FechaTransaccion >= hace60Dias &&
                           t.FechaTransaccion < hace30Dias &&
                           t.TipoTransaccion != TipoTransaccion.Retiro &&
                           t.EstadoTransaccion == EstadoTransaccion.Completada)
                    .SumAsync(t => (decimal?)t.Monto) ?? 0;

                proyeccion.IngresoMesActual = ingresosMes1;
                proyeccion.IngresoMesAnterior = ingresosMes2;

                if (ingresosMes2 > 0)
                {
                    proyeccion.CambioMensual = Math.Round((ingresosMes1 - ingresosMes2) / ingresosMes2 * 100, 1);
                }
                else if (ingresosMes1 > 0)
                {
                    proyeccion.CambioMensual = 100;
                }

                // 7. Generar datos historicos para grafico (ultimos 6 meses)
                proyeccion.HistorialIngresos = new List<DataPointProyeccion>();
                for (int i = 5; i >= 0; i--)
                {
                    var inicioMes = new DateTime(ahora.Year, ahora.Month, 1).AddMonths(-i);
                    var finMes = inicioMes.AddMonths(1);

                    var ingresoMes = await _context.Transacciones
                        .Where(t => t.UsuarioId == creadorId &&
                               t.FechaTransaccion >= inicioMes &&
                               t.FechaTransaccion < finMes &&
                               t.TipoTransaccion != TipoTransaccion.Retiro &&
                               t.EstadoTransaccion == EstadoTransaccion.Completada)
                        .SumAsync(t => (decimal?)t.Monto) ?? 0;

                    proyeccion.HistorialIngresos.Add(new DataPointProyeccion
                    {
                        Etiqueta = inicioMes.ToString("MMM"),
                        Valor = ingresoMes,
                        EsProyeccion = false
                    });
                }

                // Agregar proyeccion del proximo mes
                var proximoMes = new DateTime(ahora.Year, ahora.Month, 1).AddMonths(1);
                proyeccion.HistorialIngresos.Add(new DataPointProyeccion
                {
                    Etiqueta = proximoMes.ToString("MMM"),
                    Valor = proyeccion.IngresoProyectadoTotal,
                    EsProyeccion = true
                });

                // 8. Generar alertas de cambios significativos
                proyeccion.Alertas = new List<string>();

                if (proyeccion.ChurnRate >= 20)
                {
                    proyeccion.Alertas.Add($"Atencion: Tasa de cancelacion alta ({proyeccion.ChurnRate}%). Considera ofrecer contenido exclusivo.");
                }

                if (proyeccion.CambioMensual <= -20)
                {
                    proyeccion.Alertas.Add($"Los ingresos bajaron {Math.Abs(proyeccion.CambioMensual)}% respecto al mes anterior.");
                }
                else if (proyeccion.CambioMensual >= 50)
                {
                    proyeccion.Alertas.Add($"Excelente, tus ingresos crecieron {proyeccion.CambioMensual}% este mes.");
                }

                if (promedioNuevos < 1 && proyeccion.SuscriptoresActuales > 0)
                {
                    proyeccion.Alertas.Add("Pocos suscriptores nuevos. Considera promocionar mas tu perfil.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculando proyeccion de ingresos para {CreadorId}", creadorId);
            }

            return proyeccion;
        }

        #endregion

        #region Metricas de Bienestar

        public async Task<MetricasBienestar> ObtenerMetricasBienestarAsync(string creadorId)
        {
            var metricas = new MetricasBienestar();
            var ahora = DateTime.Now;
            var hace7Dias = ahora.AddDays(-7);
            var hace30Dias = ahora.AddDays(-30);

            try
            {
                var creador = await _context.Users.FindAsync(creadorId);
                if (creador == null) return metricas;

                // Dias en la plataforma
                metricas.DiasEnPlataforma = (int)(ahora - creador.FechaRegistro).TotalDays;

                // Publicaciones esta semana
                metricas.PublicacionesEstaSemana = await _context.Contenidos
                    .CountAsync(c => c.UsuarioId == creadorId && c.FechaPublicacion >= hace7Dias && !c.EsBorrador);

                // Publicaciones este mes
                metricas.PublicacionesEsteMes = await _context.Contenidos
                    .CountAsync(c => c.UsuarioId == creadorId && c.FechaPublicacion >= hace30Dias && !c.EsBorrador);

                // Ultimo dia de descanso (sin publicar)
                var fechasPublicacion = await _context.Contenidos
                    .Where(c => c.UsuarioId == creadorId && c.FechaPublicacion >= hace30Dias && !c.EsBorrador)
                    .Select(c => c.FechaPublicacion.Date)
                    .Distinct()
                    .OrderByDescending(d => d)
                    .ToListAsync();

                var ultimoDescanso = DateTime.Today;
                foreach (var fecha in Enumerable.Range(0, 30).Select(i => DateTime.Today.AddDays(-i)))
                {
                    if (!fechasPublicacion.Contains(fecha))
                    {
                        ultimoDescanso = fecha;
                        break;
                    }
                }
                metricas.UltimoDiaDescanso = ultimoDescanso;
                metricas.DiasSinDescanso = (int)(DateTime.Today - ultimoDescanso).TotalDays;

                // Hora promedio de publicacion
                var horasPublicacion = await _context.Contenidos
                    .Where(c => c.UsuarioId == creadorId && c.FechaPublicacion >= hace30Dias && !c.EsBorrador)
                    .Select(c => c.FechaPublicacion.Hour)
                    .ToListAsync();

                if (horasPublicacion.Any())
                {
                    metricas.HoraPromedioPublicacion = (int)Math.Round(horasPublicacion.Average());
                }

                // Tasa de respuesta a mensajes
                if (creador.MensajesRecibidosTotal > 0)
                {
                    metricas.TasaRespuestaMensajes = Math.Round(
                        (decimal)creador.MensajesRespondidosTotal / creador.MensajesRecibidosTotal * 100, 1);
                }

                // Modo vacaciones
                var configVacaciones = await ObtenerConfiguracionVacacionesAsync(creadorId);
                metricas.ModoVacacionesActivo = configVacaciones?.EstaActivo ?? false;
                if (metricas.ModoVacacionesActivo)
                {
                    metricas.FechaFinVacaciones = configVacaciones?.FechaFin;
                }

                // Score de bienestar (0-100)
                // Penaliza: muchos dias sin descanso, horarios nocturnos, alta carga
                // Premia: descansos regulares, horarios saludables
                int score = 80; // Base

                if (metricas.DiasSinDescanso > 14) score -= 25;
                else if (metricas.DiasSinDescanso > 7) score -= 10;

                if (metricas.HoraPromedioPublicacion >= 23 || metricas.HoraPromedioPublicacion < 6)
                    score -= 15;

                if (metricas.PublicacionesEstaSemana > 21) score -= 15;
                else if (metricas.PublicacionesEstaSemana > 14) score -= 5;

                if (metricas.ModoVacacionesActivo) score += 10;

                metricas.ScoreBienestar = Math.Max(0, Math.Min(100, score));

                // Mensaje segun score
                metricas.MensajeBienestar = metricas.ScoreBienestar switch
                {
                    >= 80 => "Tu equilibrio trabajo-descanso se ve saludable.",
                    >= 60 => "Considera tomar algunos descansos.",
                    >= 40 => "Tu ritmo es intenso. Un break te vendria bien.",
                    _ => "Es importante que descanses. Tu bienestar importa."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo metricas de bienestar para {CreadorId}", creadorId);
            }

            return metricas;
        }

        #endregion

        #region Contenido Programado

        public async Task<List<ContenidoProgramado>> ObtenerContenidoProgramadoAsync(string creadorId)
        {
            return await _context.ContenidosProgramados
                .Where(c => c.CreadorId == creadorId && !c.Publicado && !c.Cancelado)
                .Include(c => c.ContenidoBorrador)
                .OrderBy(c => c.FechaProgramada)
                .ToListAsync();
        }

        public async Task<bool> ProgramarContenidoAsync(string creadorId, int contenidoBorradorId, DateTime fechaProgramada)
        {
            try
            {
                // Verificar que el borrador existe y pertenece al creador
                var borrador = await _context.Contenidos
                    .FirstOrDefaultAsync(c => c.Id == contenidoBorradorId &&
                                         c.UsuarioId == creadorId &&
                                         c.EsBorrador);

                if (borrador == null) return false;

                var programacion = new ContenidoProgramado
                {
                    CreadorId = creadorId,
                    ContenidoBorradorId = contenidoBorradorId,
                    FechaProgramada = fechaProgramada
                };

                _context.ContenidosProgramados.Add(programacion);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error programando contenido para {CreadorId}", creadorId);
                return false;
            }
        }

        public async Task<bool> CancelarContenidoProgramadoAsync(int programacionId, string creadorId)
        {
            try
            {
                var programacion = await _context.ContenidosProgramados
                    .FirstOrDefaultAsync(c => c.Id == programacionId && c.CreadorId == creadorId);

                if (programacion == null) return false;

                programacion.Cancelado = true;
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelando contenido programado {ProgramacionId}", programacionId);
                return false;
            }
        }

        #endregion
    }

    #region DTOs y Modelos

    public class AlertaBienestar
    {
        public TipoAlertaBienestar Tipo { get; set; }
        public string Titulo { get; set; } = "";
        public string Mensaje { get; set; } = "";
        public string Icono { get; set; } = "info";
        public string ColorClase { get; set; } = "info";
        public int Severidad { get; set; } = 0; // 0=info, 1=leve, 2=moderado, 3=alto
        public string? Sugerencia { get; set; }
    }

    public enum TipoAlertaBienestar
    {
        TodoBien = 0,
        PublicacionExcesiva = 1,
        HorariosIrregulares = 2,
        VolumenAlto = 3,
        FaltaDescanso = 4,
        BajaInteraccion = 5
    }

    public class Celebracion
    {
        public int LogroId { get; set; }
        public string Titulo { get; set; } = "";
        public string Mensaje { get; set; } = "";
        public string Icono { get; set; } = "star";
        public string ColorClase { get; set; } = "primary";
        public bool EsNuevo { get; set; } = false;
        public DateTime? FechaLogro { get; set; }
    }

    public class ProyeccionIngresos
    {
        // Suscriptores
        public int SuscriptoresActuales { get; set; }
        public int SuscriptoresProyectados { get; set; }
        public decimal ChurnRate { get; set; }
        public int NuevosSuscriptoresMesAnterior { get; set; }

        // Ingresos por suscripciones
        public decimal IngresoSuscripcionesActual { get; set; }
        public decimal IngresoSuscripcionesProyectado { get; set; }

        // Propinas
        public decimal PropinasMesActual { get; set; }
        public decimal PropinasPromedio { get; set; }

        // Totales
        public decimal IngresoMesActual { get; set; }
        public decimal IngresoMesAnterior { get; set; }
        public decimal IngresoProyectadoTotal { get; set; }
        public decimal CambioMensual { get; set; } // Porcentaje

        // Grafico
        public List<DataPointProyeccion> HistorialIngresos { get; set; } = new();

        // Alertas
        public List<string> Alertas { get; set; } = new();
    }

    public class DataPointProyeccion
    {
        public string Etiqueta { get; set; } = "";
        public decimal Valor { get; set; }
        public bool EsProyeccion { get; set; }
    }

    public class MetricasBienestar
    {
        public int DiasEnPlataforma { get; set; }
        public int PublicacionesEstaSemana { get; set; }
        public int PublicacionesEsteMes { get; set; }
        public DateTime UltimoDiaDescanso { get; set; }
        public int DiasSinDescanso { get; set; }
        public int HoraPromedioPublicacion { get; set; }
        public decimal TasaRespuestaMensajes { get; set; }
        public bool ModoVacacionesActivo { get; set; }
        public DateTime? FechaFinVacaciones { get; set; }
        public int ScoreBienestar { get; set; } // 0-100
        public string MensajeBienestar { get; set; } = "";
    }

    #endregion
}
