using Lado.Data;
using Lado.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Lado.Controllers
{
    [Authorize]
    public class AgenciaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<AgenciaController> _logger;

        public AgenciaController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment,
            ILogger<AgenciaController> logger)
        {
            _context = context;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        // ========================================
        // DASHBOARD PRINCIPAL
        // ========================================
        public async Task<IActionResult> Index()
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var agencia = await _context.Agencias
                .Include(a => a.Anuncios)
                .Include(a => a.Transacciones)
                .FirstOrDefaultAsync(a => a.UsuarioId == usuarioId);

            if (agencia == null)
            {
                return RedirectToAction(nameof(Registrar));
            }

            if (agencia.Estado == EstadoAgencia.Pendiente)
            {
                return View("Pendiente", agencia);
            }

            if (agencia.Estado == EstadoAgencia.Rechazada)
            {
                return View("Rechazada", agencia);
            }

            if (agencia.Estado == EstadoAgencia.Suspendida)
            {
                return View("Suspendida", agencia);
            }

            // Metricas del dashboard
            var hoy = DateTime.Today;
            var hace7Dias = hoy.AddDays(-7);
            var hace30Dias = hoy.AddDays(-30);

            ViewBag.TotalAnuncios = agencia.Anuncios.Count;
            ViewBag.AnunciosActivos = agencia.Anuncios.Count(a => a.Estado == EstadoAnuncio.Activo);
            ViewBag.ImpresionesTotales = agencia.Anuncios.Sum(a => a.Impresiones);
            ViewBag.ClicsTotales = agencia.Anuncios.Sum(a => a.Clics);
            ViewBag.GastoTotal = agencia.TotalGastado;
            ViewBag.SaldoDisponible = agencia.SaldoPublicitario;

            // CTR promedio
            var totalImpresiones = agencia.Anuncios.Sum(a => a.Impresiones);
            var totalClics = agencia.Anuncios.Sum(a => a.Clics);
            ViewBag.CTRPromedio = totalImpresiones > 0 ? Math.Round((decimal)totalClics / totalImpresiones * 100, 2) : 0;

            // Anuncios recientes
            ViewBag.AnunciosRecientes = agencia.Anuncios
                .OrderByDescending(a => a.FechaCreacion)
                .Take(5)
                .ToList();

            // Transacciones recientes
            ViewBag.TransaccionesRecientes = agencia.Transacciones
                .OrderByDescending(t => t.FechaTransaccion)
                .Take(5)
                .ToList();

            return View(agencia);
        }

        // ========================================
        // REGISTRO DE AGENCIA
        // ========================================
        [HttpGet]
        public async Task<IActionResult> Registrar()
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var agenciaExistente = await _context.Agencias
                .FirstOrDefaultAsync(a => a.UsuarioId == usuarioId);

            if (agenciaExistente != null)
            {
                return RedirectToAction(nameof(Index));
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrar(Agencia model, IFormFile? logo)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Verificar si ya tiene agencia
            var agenciaExistente = await _context.Agencias
                .FirstOrDefaultAsync(a => a.UsuarioId == usuarioId);

            if (agenciaExistente != null)
            {
                TempData["Error"] = "Ya tienes una agencia registrada.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Procesar logo si se subio
            if (logo != null && logo.Length > 0)
            {
                var uploadsPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "agencias");
                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(logo.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await logo.CopyToAsync(stream);
                }

                model.LogoUrl = $"/uploads/agencias/{fileName}";
            }

            model.UsuarioId = usuarioId;
            model.FechaRegistro = DateTime.Now;
            model.Estado = EstadoAgencia.Pendiente;

            _context.Agencias.Add(model);

            // Actualizar tipo de usuario
            var usuario = await _userManager.FindByIdAsync(usuarioId);
            if (usuario != null)
            {
                usuario.TipoUsuario = 2; // Agencia
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Nueva agencia registrada: {model.NombreEmpresa} por usuario {usuarioId}");

            TempData["Success"] = "Tu solicitud de agencia ha sido enviada. Te notificaremos cuando sea aprobada.";
            return RedirectToAction(nameof(Index));
        }

        // ========================================
        // GESTION DE ANUNCIOS
        // ========================================
        public async Task<IActionResult> Anuncios()
        {
            var agencia = await ObtenerAgenciaActual();
            if (agencia == null) return RedirectToAction(nameof(Registrar));
            if (!EsAgenciaActiva(agencia)) return RedirectToAction(nameof(Index));

            var anuncios = await _context.Anuncios
                .Include(a => a.Segmentacion)
                .Where(a => a.AgenciaId == agencia.Id)
                .OrderByDescending(a => a.FechaCreacion)
                .ToListAsync();

            ViewBag.Agencia = agencia;
            return View(anuncios);
        }

        [HttpGet]
        public async Task<IActionResult> CrearAnuncio()
        {
            var agencia = await ObtenerAgenciaActual();
            if (agencia == null) return RedirectToAction(nameof(Registrar));
            if (!EsAgenciaActiva(agencia)) return RedirectToAction(nameof(Index));

            ViewBag.Agencia = agencia;
            return View(new Anuncio());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearAnuncio(Anuncio model, IFormFile? creativo,
            int? edadMinima, int? edadMaxima, GeneroUsuario? genero,
            string? paises, string? intereses)
        {
            var agencia = await ObtenerAgenciaActual();
            if (agencia == null) return RedirectToAction(nameof(Registrar));
            if (!EsAgenciaActiva(agencia)) return RedirectToAction(nameof(Index));

            // Validar presupuesto minimo
            if (model.PresupuestoDiario < 5)
            {
                ModelState.AddModelError("PresupuestoDiario", "El presupuesto diario minimo es $5.00");
            }

            if (model.PresupuestoTotal < 10)
            {
                ModelState.AddModelError("PresupuestoTotal", "El presupuesto total minimo es $10.00");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Agencia = agencia;
                return View(model);
            }

            // Procesar creativo
            if (creativo != null && creativo.Length > 0)
            {
                var uploadsPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "anuncios");
                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(creativo.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await creativo.CopyToAsync(stream);
                }

                model.UrlCreativo = $"/uploads/anuncios/{fileName}";

                // Determinar tipo de creativo
                var extension = Path.GetExtension(creativo.FileName).ToLower();
                model.TipoCreativo = extension switch
                {
                    ".mp4" or ".webm" or ".mov" => TipoCreativo.Video,
                    _ => TipoCreativo.Imagen
                };
            }

            model.AgenciaId = agencia.Id;
            model.FechaCreacion = DateTime.Now;
            model.UltimaActualizacion = DateTime.Now;
            model.Estado = EstadoAnuncio.Borrador;

            // Valores por defecto para CPM y CPC si no se especificaron
            if (model.CostoPorMilImpresiones == 0)
                model.CostoPorMilImpresiones = 2.50m;
            if (model.CostoPorClic == 0)
                model.CostoPorClic = 0.15m;

            _context.Anuncios.Add(model);
            await _context.SaveChangesAsync();

            // Crear segmentacion
            var segmentacion = new SegmentacionAnuncio
            {
                AnuncioId = model.Id,
                EdadMinima = edadMinima,
                EdadMaxima = edadMaxima,
                Genero = genero,
                PaisesJson = !string.IsNullOrEmpty(paises) ? paises : null,
                InteresesJson = !string.IsNullOrEmpty(intereses) ? intereses : null,
                DiasActivos = 127, // Todos los dias por defecto
                FechaCreacion = DateTime.Now,
                UltimaActualizacion = DateTime.Now
            };

            _context.SegmentacionesAnuncios.Add(segmentacion);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Anuncio creado: {model.Titulo} por agencia {agencia.Id}");

            TempData["Success"] = "Anuncio creado exitosamente. Puedes enviarlo a revision cuando estes listo.";
            return RedirectToAction(nameof(EditarAnuncio), new { id = model.Id });
        }

        [HttpGet]
        public async Task<IActionResult> EditarAnuncio(int id)
        {
            var agencia = await ObtenerAgenciaActual();
            if (agencia == null) return RedirectToAction(nameof(Registrar));
            if (!EsAgenciaActiva(agencia)) return RedirectToAction(nameof(Index));

            var anuncio = await _context.Anuncios
                .Include(a => a.Segmentacion)
                .FirstOrDefaultAsync(a => a.Id == id && a.AgenciaId == agencia.Id);

            if (anuncio == null)
            {
                TempData["Error"] = "Anuncio no encontrado.";
                return RedirectToAction(nameof(Anuncios));
            }

            ViewBag.Agencia = agencia;
            return View(anuncio);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarAnuncio(int id, Anuncio model, IFormFile? creativo,
            int? edadMinima, int? edadMaxima, GeneroUsuario? genero,
            string? paises, string? intereses)
        {
            var agencia = await ObtenerAgenciaActual();
            if (agencia == null) return RedirectToAction(nameof(Registrar));
            if (!EsAgenciaActiva(agencia)) return RedirectToAction(nameof(Index));

            var anuncio = await _context.Anuncios
                .Include(a => a.Segmentacion)
                .FirstOrDefaultAsync(a => a.Id == id && a.AgenciaId == agencia.Id);

            if (anuncio == null)
            {
                TempData["Error"] = "Anuncio no encontrado.";
                return RedirectToAction(nameof(Anuncios));
            }

            // Solo se puede editar si esta en borrador o rechazado
            if (anuncio.Estado != EstadoAnuncio.Borrador && anuncio.Estado != EstadoAnuncio.Rechazado)
            {
                TempData["Error"] = "Solo puedes editar anuncios en estado borrador o rechazado.";
                return RedirectToAction(nameof(Anuncios));
            }

            // Actualizar campos
            anuncio.Titulo = model.Titulo;
            anuncio.Descripcion = model.Descripcion;
            anuncio.UrlDestino = model.UrlDestino;
            anuncio.TextoBoton = model.TextoBoton;
            anuncio.TextoBotonPersonalizado = model.TextoBotonPersonalizado;
            anuncio.PresupuestoDiario = model.PresupuestoDiario;
            anuncio.PresupuestoTotal = model.PresupuestoTotal;
            anuncio.CostoPorMilImpresiones = model.CostoPorMilImpresiones;
            anuncio.CostoPorClic = model.CostoPorClic;
            anuncio.FechaInicio = model.FechaInicio;
            anuncio.FechaFin = model.FechaFin;
            anuncio.UltimaActualizacion = DateTime.Now;

            // Procesar nuevo creativo si se subio
            if (creativo != null && creativo.Length > 0)
            {
                var uploadsPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "anuncios");
                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(creativo.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await creativo.CopyToAsync(stream);
                }

                anuncio.UrlCreativo = $"/uploads/anuncios/{fileName}";

                var extension = Path.GetExtension(creativo.FileName).ToLower();
                anuncio.TipoCreativo = extension switch
                {
                    ".mp4" or ".webm" or ".mov" => TipoCreativo.Video,
                    _ => TipoCreativo.Imagen
                };
            }

            // Actualizar segmentacion
            if (anuncio.Segmentacion != null)
            {
                anuncio.Segmentacion.EdadMinima = edadMinima;
                anuncio.Segmentacion.EdadMaxima = edadMaxima;
                anuncio.Segmentacion.Genero = genero;
                anuncio.Segmentacion.PaisesJson = paises;
                anuncio.Segmentacion.InteresesJson = intereses;
                anuncio.Segmentacion.UltimaActualizacion = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Anuncio actualizado exitosamente.";
            return RedirectToAction(nameof(EditarAnuncio), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarARevision(int id)
        {
            var agencia = await ObtenerAgenciaActual();
            if (agencia == null) return RedirectToAction(nameof(Registrar));
            if (!EsAgenciaActiva(agencia)) return RedirectToAction(nameof(Index));

            var anuncio = await _context.Anuncios
                .FirstOrDefaultAsync(a => a.Id == id && a.AgenciaId == agencia.Id);

            if (anuncio == null)
            {
                return Json(new { success = false, message = "Anuncio no encontrado." });
            }

            if (anuncio.Estado != EstadoAnuncio.Borrador && anuncio.Estado != EstadoAnuncio.Rechazado)
            {
                return Json(new { success = false, message = "Solo puedes enviar a revision anuncios en estado borrador o rechazado." });
            }

            // Validar que tenga creativo
            if (string.IsNullOrEmpty(anuncio.UrlCreativo))
            {
                return Json(new { success = false, message = "Debes subir una imagen o video para el anuncio." });
            }

            anuncio.Estado = EstadoAnuncio.EnRevision;
            anuncio.UltimaActualizacion = DateTime.Now;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Anuncio {id} enviado a revision por agencia {agencia.Id}");

            return Json(new { success = true, message = "Anuncio enviado a revision. Te notificaremos cuando sea aprobado." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PausarAnuncio(int id)
        {
            var agencia = await ObtenerAgenciaActual();
            if (agencia == null) return Json(new { success = false, message = "Agencia no encontrada." });

            var anuncio = await _context.Anuncios
                .FirstOrDefaultAsync(a => a.Id == id && a.AgenciaId == agencia.Id);

            if (anuncio == null)
            {
                return Json(new { success = false, message = "Anuncio no encontrado." });
            }

            if (anuncio.Estado != EstadoAnuncio.Activo)
            {
                return Json(new { success = false, message = "Solo puedes pausar anuncios activos." });
            }

            anuncio.Estado = EstadoAnuncio.Pausado;
            anuncio.FechaPausa = DateTime.Now;
            anuncio.UltimaActualizacion = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Anuncio pausado exitosamente." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReanudarAnuncio(int id)
        {
            var agencia = await ObtenerAgenciaActual();
            if (agencia == null) return Json(new { success = false, message = "Agencia no encontrada." });

            var anuncio = await _context.Anuncios
                .FirstOrDefaultAsync(a => a.Id == id && a.AgenciaId == agencia.Id);

            if (anuncio == null)
            {
                return Json(new { success = false, message = "Anuncio no encontrado." });
            }

            if (anuncio.Estado != EstadoAnuncio.Pausado)
            {
                return Json(new { success = false, message = "Solo puedes reanudar anuncios pausados." });
            }

            // Verificar saldo
            if (agencia.SaldoPublicitario < anuncio.PresupuestoDiario)
            {
                return Json(new { success = false, message = "Saldo insuficiente para reanudar el anuncio." });
            }

            anuncio.Estado = EstadoAnuncio.Activo;
            anuncio.FechaPausa = null;
            anuncio.UltimaActualizacion = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Anuncio reanudado exitosamente." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarAnuncio(int id)
        {
            var agencia = await ObtenerAgenciaActual();
            if (agencia == null) return Json(new { success = false, message = "Agencia no encontrada." });

            var anuncio = await _context.Anuncios
                .Include(a => a.Segmentacion)
                .FirstOrDefaultAsync(a => a.Id == id && a.AgenciaId == agencia.Id);

            if (anuncio == null)
            {
                return Json(new { success = false, message = "Anuncio no encontrado." });
            }

            // Solo se puede eliminar si no esta activo
            if (anuncio.Estado == EstadoAnuncio.Activo)
            {
                return Json(new { success = false, message = "No puedes eliminar un anuncio activo. Pausalo primero." });
            }

            if (anuncio.Segmentacion != null)
            {
                _context.SegmentacionesAnuncios.Remove(anuncio.Segmentacion);
            }

            _context.Anuncios.Remove(anuncio);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Anuncio {id} eliminado por agencia {agencia.Id}");

            return Json(new { success = true, message = "Anuncio eliminado exitosamente." });
        }

        // ========================================
        // METRICAS Y ESTADISTICAS
        // ========================================
        public async Task<IActionResult> Metricas(int? anuncioId = null)
        {
            var agencia = await ObtenerAgenciaActual();
            if (agencia == null) return RedirectToAction(nameof(Registrar));
            if (!EsAgenciaActiva(agencia)) return RedirectToAction(nameof(Index));

            var hoy = DateTime.Today;
            var hace7Dias = hoy.AddDays(-7);
            var hace30Dias = hoy.AddDays(-30);

            if (anuncioId.HasValue)
            {
                // Metricas de un anuncio especifico
                var anuncio = await _context.Anuncios
                    .Include(a => a.ImpresionesDetalle)
                    .Include(a => a.ClicsDetalle)
                    .FirstOrDefaultAsync(a => a.Id == anuncioId && a.AgenciaId == agencia.Id);

                if (anuncio == null)
                {
                    TempData["Error"] = "Anuncio no encontrado.";
                    return RedirectToAction(nameof(Metricas));
                }

                ViewBag.Anuncio = anuncio;

                // Impresiones por dia (ultimos 7 dias)
                ViewBag.ImpresionesPorDia = anuncio.ImpresionesDetalle
                    .Where(i => i.FechaImpresion >= hace7Dias)
                    .GroupBy(i => i.FechaImpresion.Date)
                    .Select(g => new { Fecha = g.Key, Total = g.Count() })
                    .OrderBy(x => x.Fecha)
                    .ToList();

                // Clics por dia
                ViewBag.ClicsPorDia = anuncio.ClicsDetalle
                    .Where(c => c.FechaClic >= hace7Dias)
                    .GroupBy(c => c.FechaClic.Date)
                    .Select(g => new { Fecha = g.Key, Total = g.Count() })
                    .OrderBy(x => x.Fecha)
                    .ToList();

                return View("MetricasAnuncio", anuncio);
            }

            // Metricas generales de la agencia
            var anuncios = await _context.Anuncios
                .Where(a => a.AgenciaId == agencia.Id)
                .ToListAsync();

            ViewBag.Agencia = agencia;
            ViewBag.TotalAnuncios = anuncios.Count;
            ViewBag.AnunciosActivos = anuncios.Count(a => a.Estado == EstadoAnuncio.Activo);
            ViewBag.ImpresionesTotales = anuncios.Sum(a => a.Impresiones);
            ViewBag.ClicsTotales = anuncios.Sum(a => a.Clics);
            ViewBag.GastoTotal = agencia.TotalGastado;

            // CTR promedio
            var totalImpresiones = anuncios.Sum(a => a.Impresiones);
            var totalClics = anuncios.Sum(a => a.Clics);
            ViewBag.CTRPromedio = totalImpresiones > 0 ? Math.Round((decimal)totalClics / totalImpresiones * 100, 2) : 0;

            // Top 5 anuncios por rendimiento
            ViewBag.TopAnuncios = anuncios
                .OrderByDescending(a => a.Clics)
                .Take(5)
                .ToList();

            // Gasto por dia (ultimos 30 dias)
            ViewBag.GastoPorDia = await _context.TransaccionesAgencias
                .Where(t => t.AgenciaId == agencia.Id && t.FechaTransaccion >= hace30Dias &&
                       (t.Tipo == TipoTransaccionAgencia.CobroCPM || t.Tipo == TipoTransaccionAgencia.CobroCPC))
                .GroupBy(t => t.FechaTransaccion.Date)
                .Select(g => new { Fecha = g.Key, Total = g.Sum(t => Math.Abs(t.Monto)) })
                .OrderBy(x => x.Fecha)
                .ToListAsync();

            return View(anuncios);
        }

        // ========================================
        // RECARGA DE SALDO
        // ========================================
        public async Task<IActionResult> RecargarSaldo()
        {
            var agencia = await ObtenerAgenciaActual();
            if (agencia == null) return RedirectToAction(nameof(Registrar));
            if (!EsAgenciaActiva(agencia)) return RedirectToAction(nameof(Index));

            ViewBag.Agencia = agencia;
            ViewBag.Transacciones = await _context.TransaccionesAgencias
                .Where(t => t.AgenciaId == agencia.Id)
                .OrderByDescending(t => t.FechaTransaccion)
                .Take(20)
                .ToListAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcesarRecarga(decimal monto)
        {
            var agencia = await ObtenerAgenciaActual();
            if (agencia == null) return Json(new { success = false, message = "Agencia no encontrada." });
            if (!EsAgenciaActiva(agencia)) return Json(new { success = false, message = "Tu agencia no esta activa." });

            if (monto < 10)
            {
                return Json(new { success = false, message = "El monto minimo de recarga es $10.00" });
            }

            if (monto > 10000)
            {
                return Json(new { success = false, message = "El monto maximo por recarga es $10,000.00" });
            }

            // Aqui iria la integracion con pasarela de pago
            // Por ahora simulamos la recarga

            var saldoAnterior = agencia.SaldoPublicitario;

            agencia.SaldoPublicitario += monto;
            agencia.TotalRecargado += monto;

            var transaccion = new TransaccionAgencia
            {
                AgenciaId = agencia.Id,
                Tipo = TipoTransaccionAgencia.RecargaSaldo,
                Monto = monto,
                SaldoAnterior = saldoAnterior,
                SaldoPosterior = agencia.SaldoPublicitario,
                Descripcion = $"Recarga de saldo publicitario",
                FechaTransaccion = DateTime.Now
            };

            _context.TransaccionesAgencias.Add(transaccion);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Recarga de ${monto} procesada para agencia {agencia.Id}");

            return Json(new
            {
                success = true,
                message = $"Recarga de ${monto:N2} procesada exitosamente.",
                nuevoSaldo = agencia.SaldoPublicitario
            });
        }

        // ========================================
        // PERFIL DE AGENCIA
        // ========================================
        public async Task<IActionResult> Perfil()
        {
            var agencia = await ObtenerAgenciaActual();
            if (agencia == null) return RedirectToAction(nameof(Registrar));

            return View(agencia);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarPerfil(Agencia model, IFormFile? logo)
        {
            var agencia = await ObtenerAgenciaActual();
            if (agencia == null) return RedirectToAction(nameof(Registrar));

            // Actualizar campos editables
            agencia.NombreEmpresa = model.NombreEmpresa;
            agencia.RazonSocial = model.RazonSocial;
            agencia.NIF = model.NIF;
            agencia.Direccion = model.Direccion;
            agencia.Ciudad = model.Ciudad;
            agencia.Pais = model.Pais;
            agencia.CodigoPostal = model.CodigoPostal;
            agencia.Telefono = model.Telefono;
            agencia.SitioWeb = model.SitioWeb;
            agencia.Descripcion = model.Descripcion;

            // Procesar nuevo logo si se subio
            if (logo != null && logo.Length > 0)
            {
                var uploadsPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "agencias");
                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(logo.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await logo.CopyToAsync(stream);
                }

                agencia.LogoUrl = $"/uploads/agencias/{fileName}";
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Perfil actualizado exitosamente.";
            return RedirectToAction(nameof(Perfil));
        }

        // ========================================
        // HELPERS PRIVADOS
        // ========================================
        private async Task<Agencia?> ObtenerAgenciaActual()
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return await _context.Agencias
                .Include(a => a.Anuncios)
                .FirstOrDefaultAsync(a => a.UsuarioId == usuarioId);
        }

        private bool EsAgenciaActiva(Agencia agencia)
        {
            return agencia.Estado == EstadoAgencia.Activa;
        }
    }
}
