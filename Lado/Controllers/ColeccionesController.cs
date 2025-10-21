using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Lado.Data;
using Lado.Models;
using System.ComponentModel.DataAnnotations;

namespace Lado.Controllers
{
    [Authorize]
    [Route("[controller]")]
    public class ColeccionesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ColeccionesController> _logger;

        public ColeccionesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<ColeccionesController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // ========================================
        // OBTENER COLECCIONES (API)
        // ========================================

        /// <summary>
        /// Obtener colecciones destacadas o de un creador específico
        /// </summary>
        [HttpGet("Obtener")]
        public async Task<IActionResult> ObtenerColecciones(string? creadorId = null)
        {
            try
            {
                var query = _context.Colecciones
                    .Include(c => c.Creador)
                    .Include(c => c.Contenidos)
                        .ThenInclude(cc => cc.Contenido)
                    .Where(c => c.EstaActiva);

                if (!string.IsNullOrEmpty(creadorId))
                {
                    query = query.Where(c => c.CreadorId == creadorId);
                }

                var colecciones = await query
                    .OrderByDescending(c => c.FechaCreacion)
                    .Take(20)
                    .Select(c => new
                    {
                        id = c.Id,
                        nombre = c.Nombre,
                        descripcion = c.Descripcion,
                        precio = c.Precio,
                        precioOriginal = c.PrecioOriginal,
                        descuento = c.DescuentoPorcentaje,
                        itemCount = c.Contenidos.Count,
                        creador = new
                        {
                            id = c.CreadorId,
                            nombre = c.Creador.NombreCompleto,
                            username = c.Creador.UserName,
                            avatar = c.Creador.FotoPerfil
                        },
                        imagenPortada = c.ImagenPortada,
                        fechaCreacion = c.FechaCreacion
                    })
                    .ToListAsync();

                return Json(new { success = true, colecciones });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener colecciones");
                return Json(new { success = false, message = "Error al cargar colecciones" });
            }
        }

        // ========================================
        // DETALLE DE COLECCIÓN (VISTA)
        // ========================================

        /// <summary>
        /// Ver detalle de una colección
        /// </summary>
        [HttpGet("Detalle/{id}")]
        public async Task<IActionResult> Detalle(int id)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var coleccion = await _context.Colecciones
                    .Include(c => c.Creador)
                    .Include(c => c.Contenidos)
                        .ThenInclude(cc => cc.Contenido)
                            .ThenInclude(cont => cont.Usuario)
                    .FirstOrDefaultAsync(c => c.Id == id && c.EstaActiva);

                if (coleccion == null)
                {
                    TempData["Error"] = "Colección no encontrada";
                    return RedirectToAction("Index", "Feed");
                }

                // Verificar si ya la compró
                var yaComprada = await _context.ComprasColeccion
                    .AnyAsync(cc => cc.ColeccionId == id && cc.CompradorId == usuarioId);

                ViewBag.YaComprada = yaComprada;
                ViewBag.TotalContenidos = coleccion.Contenidos.Count;

                // Calcular ahorro si hay descuento
                if (coleccion.PrecioOriginal.HasValue && coleccion.PrecioOriginal > coleccion.Precio)
                {
                    ViewBag.Ahorro = coleccion.PrecioOriginal.Value - coleccion.Precio;
                }

                return View(coleccion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar detalle de colección {ColeccionId}", id);
                TempData["Error"] = "Error al cargar la colección";
                return RedirectToAction("Index", "Feed");
            }
        }

        // ========================================
        // COMPRAR COLECCIÓN
        // ========================================

        /// <summary>
        /// Comprar una colección completa
        /// </summary>
        [HttpPost("Comprar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ComprarColeccion(int coleccionId)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var coleccion = await _context.Colecciones
                    .Include(c => c.Contenidos)
                    .FirstOrDefaultAsync(c => c.Id == coleccionId && c.EstaActiva);

                if (coleccion == null)
                {
                    return Json(new { success = false, message = "Colección no encontrada" });
                }

                // Verificar que no la haya comprado ya
                var yaComprada = await _context.ComprasColeccion
                    .AnyAsync(cc => cc.ColeccionId == coleccionId && cc.CompradorId == usuarioId);

                if (yaComprada)
                {
                    return Json(new { success = false, message = "Ya compraste esta colección" });
                }

                // Verificar saldo
                var usuario = await _userManager.FindByIdAsync(usuarioId);
                if (usuario.Saldo < coleccion.Precio)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Saldo insuficiente",
                        saldoActual = usuario.Saldo,
                        precioColeccion = coleccion.Precio
                    });
                }

                // Crear compra de colección
                var compra = new CompraColeccion
                {
                    ColeccionId = coleccionId,
                    CompradorId = usuarioId,
                    Precio = coleccion.Precio,
                    FechaCompra = DateTime.Now
                };

                _context.ComprasColeccion.Add(compra);

                // Descontar saldo
                usuario.Saldo -= coleccion.Precio;

                // Crear transacción
                var transaccion = new Transaccion
                {
                    UsuarioId = usuarioId,
                    TipoTransaccion = TipoTransaccion.CompraContenido,
                    Monto = -coleccion.Precio,
                    Descripcion = $"Compra de colección: {coleccion.Nombre}",
                    FechaTransaccion = DateTime.Now,
                    EstadoTransaccion = EstadoTransaccion.Completada
                };

                _context.Transacciones.Add(transaccion);

                // Dar acceso a todos los contenidos de la colección
                foreach (var contenidoColeccion in coleccion.Contenidos)
                {
                    // Verificar que no haya comprado ya ese contenido individualmente
                    var yaComproContenido = await _context.ComprasContenido
                        .AnyAsync(cc => cc.UsuarioId == usuarioId
                                     && cc.ContenidoId == contenidoColeccion.ContenidoId);

                    if (!yaComproContenido)
                    {
                        var compraContenido = new CompraContenido
                        {
                            UsuarioId = usuarioId,
                            ContenidoId = contenidoColeccion.ContenidoId,
                            Monto = 0, // Ya pagó en la colección
                            FechaCompra = DateTime.Now
                        };

                        _context.ComprasContenido.Add(compraContenido);
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Colección {ColeccionId} comprada por usuario {UserId} por ${Precio}",
                    coleccionId, usuarioId, coleccion.Precio);

                return Json(new
                {
                    success = true,
                    message = "Colección comprada exitosamente",
                    saldoRestante = usuario.Saldo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al comprar colección {ColeccionId}", coleccionId);
                return Json(new { success = false, message = "Error al procesar la compra" });
            }
        }

        // ========================================
        // CREAR COLECCIÓN (SOLO CREADORES)
        // ========================================

        /// <summary>
        /// Crear nueva colección (solo creadores)
        /// </summary>
        [HttpPost("Crear")]
        [Authorize(Roles = "Creador")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearColeccion([FromBody] CrearColeccionDto dto)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (!ModelState.IsValid)
                {
                    return Json(new { success = false, message = "Datos inválidos" });
                }

                // Validar contenidos
                if (dto.ContenidosIds == null || !dto.ContenidosIds.Any())
                {
                    return Json(new { success = false, message = "Debe seleccionar al menos un contenido" });
                }

                // Verificar que todos los contenidos pertenezcan al creador
                var contenidos = await _context.Contenidos
                    .Where(c => dto.ContenidosIds.Contains(c.Id) && c.UsuarioId == usuarioId)
                    .ToListAsync();

                if (contenidos.Count != dto.ContenidosIds.Count)
                {
                    return Json(new { success = false, message = "Algunos contenidos no son válidos" });
                }

                // Calcular descuento si se proporciona precio original
                int? descuento = null;
                if (dto.PrecioOriginal.HasValue && dto.PrecioOriginal > dto.Precio)
                {
                    descuento = (int)Math.Round(((dto.PrecioOriginal.Value - dto.Precio) / dto.PrecioOriginal.Value) * 100);
                }

                // Crear colección
                var coleccion = new Coleccion
                {
                    CreadorId = usuarioId,
                    Nombre = dto.Nombre,
                    Descripcion = dto.Descripcion,
                    Precio = dto.Precio,
                    PrecioOriginal = dto.PrecioOriginal,
                    DescuentoPorcentaje = descuento,
                    ImagenPortada = dto.ImagenPortada,
                    EstaActiva = true,
                    FechaCreacion = DateTime.Now
                };

                _context.Colecciones.Add(coleccion);
                await _context.SaveChangesAsync();

                // Agregar contenidos a la colección
                int orden = 0;
                foreach (var contenidoId in dto.ContenidosIds)
                {
                    var contenidoColeccion = new ContenidoColeccion
                    {
                        ColeccionId = coleccion.Id,
                        ContenidoId = contenidoId,
                        Orden = orden++
                    };

                    _context.ContenidoColecciones.Add(contenidoColeccion);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Colección {ColeccionId} creada por usuario {UserId} con {Count} contenidos",
                    coleccion.Id, usuarioId, dto.ContenidosIds.Count);

                return Json(new
                {
                    success = true,
                    coleccionId = coleccion.Id,
                    message = "Colección creada exitosamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear colección");
                return Json(new { success = false, message = "Error al crear la colección" });
            }
        }

        // ========================================
        // EDITAR COLECCIÓN
        // ========================================

        /// <summary>
        /// Editar colección existente
        /// </summary>
        [HttpPost("Editar/{id}")]
        [Authorize(Roles = "Creador")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarColeccion(int id, [FromBody] CrearColeccionDto dto)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var coleccion = await _context.Colecciones
                    .Include(c => c.Contenidos)
                    .FirstOrDefaultAsync(c => c.Id == id && c.CreadorId == usuarioId);

                if (coleccion == null)
                {
                    return Json(new { success = false, message = "Colección no encontrada" });
                }

                // Actualizar datos
                coleccion.Nombre = dto.Nombre;
                coleccion.Descripcion = dto.Descripcion;
                coleccion.Precio = dto.Precio;
                coleccion.PrecioOriginal = dto.PrecioOriginal;
                coleccion.ImagenPortada = dto.ImagenPortada;
                coleccion.FechaActualizacion = DateTime.Now;

                // Recalcular descuento
                if (dto.PrecioOriginal.HasValue && dto.PrecioOriginal > dto.Precio)
                {
                    coleccion.DescuentoPorcentaje = (int)Math.Round(((dto.PrecioOriginal.Value - dto.Precio) / dto.PrecioOriginal.Value) * 100);
                }
                else
                {
                    coleccion.DescuentoPorcentaje = null;
                }

                // Actualizar contenidos si se proporcionan
                if (dto.ContenidosIds != null && dto.ContenidosIds.Any())
                {
                    // Eliminar contenidos existentes
                    var contenidosExistentes = await _context.ContenidoColecciones
                        .Where(cc => cc.ColeccionId == id)
                        .ToListAsync();

                    _context.ContenidoColecciones.RemoveRange(contenidosExistentes);

                    // Agregar nuevos contenidos
                    int orden = 0;
                    foreach (var contenidoId in dto.ContenidosIds)
                    {
                        var contenidoColeccion = new ContenidoColeccion
                        {
                            ColeccionId = id,
                            ContenidoId = contenidoId,
                            Orden = orden++
                        };

                        _context.ContenidoColecciones.Add(contenidoColeccion);
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Colección {ColeccionId} editada por usuario {UserId}", id, usuarioId);

                return Json(new { success = true, message = "Colección actualizada exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al editar colección {ColeccionId}", id);
                return Json(new { success = false, message = "Error al actualizar la colección" });
            }
        }

        // ========================================
        // ELIMINAR COLECCIÓN
        // ========================================

        /// <summary>
        /// Eliminar colección (soft delete)
        /// </summary>
        [HttpPost("Eliminar/{id}")]
        [Authorize(Roles = "Creador")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarColeccion(int id)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var coleccion = await _context.Colecciones
                    .FirstOrDefaultAsync(c => c.Id == id && c.CreadorId == usuarioId);

                if (coleccion == null)
                {
                    return Json(new { success = false, message = "Colección no encontrada" });
                }

                // Soft delete
                coleccion.EstaActiva = false;
                coleccion.FechaActualizacion = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Colección {ColeccionId} eliminada por usuario {UserId}", id, usuarioId);

                return Json(new { success = true, message = "Colección eliminada" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar colección {ColeccionId}", id);
                return Json(new { success = false, message = "Error al eliminar la colección" });
            }
        }

        // ========================================
        // MIS COLECCIONES COMPRADAS
        // ========================================

        /// <summary>
        /// Ver colecciones que el usuario ha comprado
        /// </summary>
        [HttpGet("MisCompras")]
        public async Task<IActionResult> MisCompras()
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var compras = await _context.ComprasColeccion
                    .Include(cc => cc.Coleccion)
                        .ThenInclude(c => c.Creador)
                    .Include(cc => cc.Coleccion)
                        .ThenInclude(c => c.Contenidos)
                    .Where(cc => cc.CompradorId == usuarioId)
                    .OrderByDescending(cc => cc.FechaCompra)
                    .ToListAsync();

                _logger.LogInformation("Usuario {UserId} consultó sus {Count} colecciones compradas",
                    usuarioId, compras.Count);

                return View(compras);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener colecciones compradas");
                TempData["Error"] = "Error al cargar tus colecciones";
                return RedirectToAction("Index", "Feed");
            }
        }

        // ========================================
        // MIS COLECCIONES CREADAS (CREADOR)
        // ========================================

        /// <summary>
        /// Ver colecciones que el creador ha creado
        /// </summary>
        [HttpGet("MisColecciones")]
        [Authorize(Roles = "Creador")]
        public async Task<IActionResult> MisColecciones()
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var colecciones = await _context.Colecciones
                    .Include(c => c.Contenidos)
                    .Include(c => c.Compras)
                    .Where(c => c.CreadorId == usuarioId)
                    .OrderByDescending(c => c.FechaCreacion)
                    .ToListAsync();

                _logger.LogInformation("Creador {UserId} consultó sus {Count} colecciones",
                    usuarioId, colecciones.Count);

                return View(colecciones);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener colecciones del creador");
                TempData["Error"] = "Error al cargar tus colecciones";
                return RedirectToAction("Index", "Feed");
            }
        }

        // ========================================
        // VISTA PARA CREAR COLECCIÓN
        // ========================================

        /// <summary>
        /// Vista para crear una nueva colección
        /// </summary>
        [HttpGet("Nueva")]
        [Authorize(Roles = "Creador")]
        public async Task<IActionResult> Nueva()
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Obtener contenidos del creador disponibles para agregar a colección
                var contenidosDisponibles = await _context.Contenidos
                    .Where(c => c.UsuarioId == usuarioId
                            && c.EstaActivo
                            && !c.EsBorrador)
                    .OrderByDescending(c => c.FechaPublicacion)
                    .ToListAsync();

                ViewBag.ContenidosDisponibles = contenidosDisponibles;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar formulario de nueva colección");
                TempData["Error"] = "Error al cargar el formulario";
                return RedirectToAction("MisColecciones");
            }
        }

        // ========================================
        // VISTA PARA EDITAR COLECCIÓN
        // ========================================

        /// <summary>
        /// Vista para editar una colección existente
        /// </summary>
        [HttpGet("Editar/{id}")]
        [Authorize(Roles = "Creador")]
        public async Task<IActionResult> Editar(int id)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var coleccion = await _context.Colecciones
                    .Include(c => c.Contenidos)
                        .ThenInclude(cc => cc.Contenido)
                    .FirstOrDefaultAsync(c => c.Id == id && c.CreadorId == usuarioId);

                if (coleccion == null)
                {
                    TempData["Error"] = "Colección no encontrada";
                    return RedirectToAction("MisColecciones");
                }

                // Obtener todos los contenidos disponibles
                var contenidosDisponibles = await _context.Contenidos
                    .Where(c => c.UsuarioId == usuarioId
                            && c.EstaActivo
                            && !c.EsBorrador)
                    .OrderByDescending(c => c.FechaPublicacion)
                    .ToListAsync();

                ViewBag.ContenidosDisponibles = contenidosDisponibles;

                return View(coleccion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar formulario de edición de colección {ColeccionId}", id);
                TempData["Error"] = "Error al cargar la colección";
                return RedirectToAction("MisColecciones");
            }
        }
    }

    // ========================================
    // DTOs
    // ========================================

    /// <summary>
    /// DTO para crear/editar colección
    /// </summary>
    public class CrearColeccionDto
    {
        [Required(ErrorMessage = "El nombre es requerido")]
        [MaxLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
        public string Nombre { get; set; }

        [MaxLength(1000, ErrorMessage = "La descripción no puede exceder 1000 caracteres")]
        public string? Descripcion { get; set; }

        [Required(ErrorMessage = "El precio es requerido")]
        [Range(0.01, 10000, ErrorMessage = "El precio debe estar entre $0.01 y $10,000")]
        public decimal Precio { get; set; }

        [Range(0.01, 10000, ErrorMessage = "El precio original debe estar entre $0.01 y $10,000")]
        public decimal? PrecioOriginal { get; set; }

        [Range(0, 100, ErrorMessage = "El descuento debe estar entre 0% y 100%")]
        public int? DescuentoPorcentaje { get; set; }

        [MaxLength(500, ErrorMessage = "La URL de la imagen no puede exceder 500 caracteres")]
        public string? ImagenPortada { get; set; }

        [Required(ErrorMessage = "Debe seleccionar al menos un contenido")]
        [MinLength(1, ErrorMessage = "Debe seleccionar al menos un contenido")]
        public List<int> ContenidosIds { get; set; }
    }
}