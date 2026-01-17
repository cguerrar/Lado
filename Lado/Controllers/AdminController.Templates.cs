using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Lado.Services;
using Lado.Models;

namespace Lado.Controllers
{
    public partial class AdminController
    {
        // ========================================
        // TEMPLATES DE RESPUESTAS - ENDPOINTS
        // ========================================

        /// <summary>
        /// Vista de gestión de templates
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Templates([FromServices] ITemplatesService templatesService)
        {
            var templates = await templatesService.ObtenerTemplatesAsync();
            return View(templates);
        }

        /// <summary>
        /// Obtener todos los templates (para AJAX)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerTemplates(
            [FromServices] ITemplatesService templatesService,
            CategoriaTemplate? categoria)
        {
            try
            {
                var templates = await templatesService.ObtenerTemplatesAsync(categoria);

                return Json(new
                {
                    success = true,
                    templates = templates.Select(t => new
                    {
                        id = t.Id,
                        nombre = t.Nombre,
                        categoria = t.Categoria.ToString(),
                        categoriaValor = (int)t.Categoria,
                        contenido = t.Contenido,
                        descripcion = t.Descripcion,
                        atajo = t.Atajo,
                        orden = t.Orden,
                        vecesUsado = t.VecesUsado
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error obteniendo templates");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Obtener template por atajo de teclado
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerTemplatePorAtajo(
            [FromServices] ITemplatesService templatesService,
            string atajo)
        {
            try
            {
                var template = await templatesService.ObtenerTemplatePorAtajoAsync(atajo);

                if (template == null)
                    return Json(new { success = false, error = "Template no encontrado" });

                return Json(new
                {
                    success = true,
                    template = new
                    {
                        id = template.Id,
                        nombre = template.Nombre,
                        contenido = template.Contenido
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error obteniendo template por atajo: {Atajo}", atajo);
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Crear nuevo template
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearTemplate(
            [FromServices] ITemplatesService templatesService,
            [FromBody] TemplateDto dto)
        {
            try
            {
                var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminId))
                    return Json(new { success = false, error = "No autenticado" });

                var template = new TemplateRespuesta
                {
                    Nombre = dto.Nombre,
                    Categoria = dto.Categoria,
                    Contenido = dto.Contenido,
                    Descripcion = dto.Descripcion,
                    Atajo = dto.Atajo,
                    Orden = dto.Orden
                };

                await templatesService.CrearTemplateAsync(template, adminId);

                return Json(new { success = true, id = template.Id, mensaje = "Template creado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error creando template");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Actualizar template existente
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarTemplate(
            [FromServices] ITemplatesService templatesService,
            [FromBody] TemplateDto dto)
        {
            try
            {
                var template = new TemplateRespuesta
                {
                    Nombre = dto.Nombre,
                    Categoria = dto.Categoria,
                    Contenido = dto.Contenido,
                    Descripcion = dto.Descripcion,
                    Atajo = dto.Atajo,
                    Orden = dto.Orden
                };

                var resultado = await templatesService.ActualizarTemplateAsync(dto.Id, template);

                if (resultado == null)
                    return Json(new { success = false, error = "Template no encontrado" });

                return Json(new { success = true, mensaje = "Template actualizado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error actualizando template {Id}", dto.Id);
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Eliminar template
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarTemplate(
            [FromServices] ITemplatesService templatesService,
            int id)
        {
            try
            {
                var resultado = await templatesService.EliminarTemplateAsync(id);

                if (!resultado)
                    return Json(new { success = false, error = "Template no encontrado" });

                return Json(new { success = true, mensaje = "Template eliminado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error eliminando template {Id}", id);
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Registrar uso de template
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UsarTemplate(
            [FromServices] ITemplatesService templatesService,
            int id)
        {
            await templatesService.IncrementarUsoAsync(id);
            return Json(new { success = true });
        }
    }

    public class TemplateDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public CategoriaTemplate Categoria { get; set; }
        public string Contenido { get; set; } = "";
        public string? Descripcion { get; set; }
        public string? Atajo { get; set; }
        public int Orden { get; set; }
    }
}
