using Lado.Data;
using Lado.Models;
using Lado.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lado.Controllers
{
    /// <summary>
    /// Controlador para el Blog público (SEO) y gestión de artículos
    /// </summary>
    public class BlogController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<BlogController> _logger;

        public BlogController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<BlogController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        /// <summary>
        /// Lista de artículos públicos - /Blog
        /// </summary>
        [Route("Blog")]
        [Route("Blog/Index")]
        public async Task<IActionResult> Index(CategoriaBlog? categoria = null, int pagina = 1)
        {
            var pageSize = 12;

            var query = _context.ArticulosBlog
                .Include(a => a.Autor)
                .Where(a => a.EstaPublicado && a.FechaPublicacion != null)
                .AsQueryable();

            if (categoria.HasValue)
            {
                query = query.Where(a => a.Categoria == categoria.Value);
            }

            var totalArticulos = await query.CountAsync();
            var totalPaginas = (int)Math.Ceiling(totalArticulos / (double)pageSize);

            var articulos = await query
                .OrderByDescending(a => a.EsDestacado)
                .ThenByDescending(a => a.FechaPublicacion)
                .Skip((pagina - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Categoria = categoria;
            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = totalPaginas;
            ViewBag.TotalArticulos = totalArticulos;

            // SEO
            ViewData["Title"] = categoria.HasValue
                ? $"Blog - {categoria.Value} | Lado"
                : "Blog | Lado - Noticias, tutoriales y consejos";
            ViewData["MetaDescription"] = "Descubre las últimas noticias, tutoriales y consejos para creadores de contenido en Lado.";

            return View(articulos);
        }

        /// <summary>
        /// Ver artículo individual - /Blog/{slug}
        /// </summary>
        [Route("Blog/{slug}")]
        public async Task<IActionResult> Articulo(string slug)
        {
            if (string.IsNullOrEmpty(slug))
                return RedirectToAction(nameof(Index));

            var articulo = await _context.ArticulosBlog
                .Include(a => a.Autor)
                .FirstOrDefaultAsync(a => a.Slug == slug && a.EstaPublicado);

            if (articulo == null)
                return NotFound();

            // Incrementar vistas
            articulo.Vistas++;
            await _context.SaveChangesAsync();

            // Artículos relacionados
            var relacionados = await _context.ArticulosBlog
                .Where(a => a.EstaPublicado &&
                            a.Id != articulo.Id &&
                            a.Categoria == articulo.Categoria)
                .OrderByDescending(a => a.FechaPublicacion)
                .Take(3)
                .ToListAsync();

            ViewBag.Relacionados = relacionados;

            // SEO
            ViewData["Title"] = articulo.MetaTitulo ?? articulo.Titulo;
            ViewData["MetaDescription"] = articulo.MetaDescripcion ?? articulo.Resumen;
            ViewData["OgImage"] = articulo.ImagenPortada ?? "/images/og-default.jpg";
            ViewData["OgType"] = "article";
            ViewData["ArticleTitle"] = articulo.Titulo;
            ViewData["ArticleAuthor"] = articulo.Autor?.NombreCompleto ?? "Lado";
            ViewData["ArticleDatePublished"] = articulo.FechaPublicacion?.ToString("yyyy-MM-dd");
            ViewData["SchemaType"] = "Article";

            return View(articulo);
        }

        // ========================================
        // ADMINISTRACIÓN DE ARTÍCULOS
        // ========================================

        /// <summary>
        /// Lista de artículos para admin - /Admin/Blog
        /// </summary>
        [Authorize(Roles = "Admin")]
        [Route("Admin/Blog")]
        public async Task<IActionResult> AdminIndex()
        {
            var articulos = await _context.ArticulosBlog
                .Include(a => a.Autor)
                .OrderByDescending(a => a.FechaCreacion)
                .ToListAsync();

            return View("Admin/Index", articulos);
        }

        /// <summary>
        /// Crear artículo - GET
        /// </summary>
        [Authorize(Roles = "Admin")]
        [Route("Admin/Blog/Crear")]
        [HttpGet]
        public IActionResult AdminCrear()
        {
            return View("Admin/Crear", new ArticuloBlog());
        }

        /// <summary>
        /// Crear artículo - POST
        /// </summary>
        [Authorize(Roles = "Admin")]
        [Route("Admin/Blog/Crear")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminCrear(ArticuloBlog articulo, IFormFile? imagenPortada)
        {
            if (!ModelState.IsValid)
            {
                return View("Admin/Crear", articulo);
            }

            var user = await _userManager.GetUserAsync(User);
            articulo.AutorId = user?.Id;
            articulo.FechaCreacion = DateTime.UtcNow;

            // Generar slug
            if (string.IsNullOrEmpty(articulo.Slug))
            {
                articulo.Slug = ArticuloBlog.GenerarSlug(articulo.Titulo);
            }

            // Verificar slug único
            var slugExiste = await _context.ArticulosBlog.AnyAsync(a => a.Slug == articulo.Slug);
            if (slugExiste)
            {
                articulo.Slug = $"{articulo.Slug}-{DateTime.Now.Ticks}";
            }

            // Subir imagen
            if (imagenPortada != null && imagenPortada.Length > 0)
            {
                var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "blog");
                Directory.CreateDirectory(uploadsPath);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(imagenPortada.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imagenPortada.CopyToAsync(stream);
                }

                articulo.ImagenPortada = $"/uploads/blog/{fileName}";
            }

            // Calcular tiempo de lectura
            articulo.CalcularTiempoLectura();

            // Si se marca como publicado, establecer fecha
            if (articulo.EstaPublicado && !articulo.FechaPublicacion.HasValue)
            {
                articulo.FechaPublicacion = DateTime.UtcNow;
            }

            _context.ArticulosBlog.Add(articulo);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Artículo de blog creado: {Titulo} por {User}", articulo.Titulo, user?.UserName);
            TempData["Success"] = "Artículo creado exitosamente";

            return RedirectToAction(nameof(AdminIndex));
        }

        /// <summary>
        /// Editar artículo - GET
        /// </summary>
        [Authorize(Roles = "Admin")]
        [Route("Admin/Blog/Editar/{id}")]
        [HttpGet]
        public async Task<IActionResult> AdminEditar(int id)
        {
            var articulo = await _context.ArticulosBlog.FindAsync(id);
            if (articulo == null)
                return NotFound();

            return View("Admin/Editar", articulo);
        }

        /// <summary>
        /// Editar artículo - POST
        /// </summary>
        [Authorize(Roles = "Admin")]
        [Route("Admin/Blog/Editar/{id}")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminEditar(int id, ArticuloBlog articulo, IFormFile? imagenPortada)
        {
            if (id != articulo.Id)
                return NotFound();

            var articuloExistente = await _context.ArticulosBlog.FindAsync(id);
            if (articuloExistente == null)
                return NotFound();

            // Actualizar campos
            articuloExistente.Titulo = articulo.Titulo;
            articuloExistente.Resumen = articulo.Resumen;
            articuloExistente.Contenido = articulo.Contenido;
            articuloExistente.MetaTitulo = articulo.MetaTitulo;
            articuloExistente.MetaDescripcion = articulo.MetaDescripcion;
            articuloExistente.PalabrasClave = articulo.PalabrasClave;
            articuloExistente.Categoria = articulo.Categoria;
            articuloExistente.EsDestacado = articulo.EsDestacado;
            articuloExistente.FechaModificacion = DateTime.UtcNow;

            // Si se marca como publicado por primera vez
            if (articulo.EstaPublicado && !articuloExistente.EstaPublicado)
            {
                articuloExistente.FechaPublicacion = DateTime.UtcNow;
            }
            articuloExistente.EstaPublicado = articulo.EstaPublicado;

            // Subir nueva imagen
            if (imagenPortada != null && imagenPortada.Length > 0)
            {
                var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "blog");
                Directory.CreateDirectory(uploadsPath);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(imagenPortada.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imagenPortada.CopyToAsync(stream);
                }

                articuloExistente.ImagenPortada = $"/uploads/blog/{fileName}";
            }

            articuloExistente.CalcularTiempoLectura();

            await _context.SaveChangesAsync();

            TempData["Success"] = "Artículo actualizado";
            return RedirectToAction(nameof(AdminIndex));
        }

        /// <summary>
        /// Eliminar artículo
        /// </summary>
        [Authorize(Roles = "Admin")]
        [Route("Admin/Blog/Eliminar/{id}")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminEliminar(int id)
        {
            var articulo = await _context.ArticulosBlog.FindAsync(id);
            if (articulo == null)
                return NotFound();

            _context.ArticulosBlog.Remove(articulo);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Artículo de blog eliminado: {Titulo}", articulo.Titulo);
            TempData["Success"] = "Artículo eliminado";

            return RedirectToAction(nameof(AdminIndex));
        }

        /// <summary>
        /// Cambiar estado de publicación
        /// </summary>
        [Authorize(Roles = "Admin")]
        [Route("Admin/Blog/TogglePublicado/{id}")]
        [HttpPost]
        public async Task<IActionResult> TogglePublicado(int id)
        {
            var articulo = await _context.ArticulosBlog.FindAsync(id);
            if (articulo == null)
                return NotFound();

            articulo.EstaPublicado = !articulo.EstaPublicado;

            if (articulo.EstaPublicado && !articulo.FechaPublicacion.HasValue)
            {
                articulo.FechaPublicacion = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, publicado = articulo.EstaPublicado });
        }
    }
}
