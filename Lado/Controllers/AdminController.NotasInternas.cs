using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Lado.Services;
using Lado.Models;

namespace Lado.Controllers
{
    public partial class AdminController
    {
        // ========================================
        // NOTAS INTERNAS - ENDPOINTS
        // ========================================

        /// <summary>
        /// Vista principal de notas internas
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> NotasInternas([FromServices] INotasInternasService notasService)
        {
            var notas = await notasService.ObtenerNotasRecientesAsync(100);
            return View(notas);
        }

        /// <summary>
        /// Crear una nueva nota interna
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearNotaInterna(
            [FromServices] INotasInternasService notasService,
            [FromBody] CrearNotaDto dto)
        {
            try
            {
                var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminId))
                    return Json(new { success = false, error = "No autenticado" });

                if (string.IsNullOrWhiteSpace(dto.Contenido))
                    return Json(new { success = false, error = "El contenido es requerido" });

                var nota = await notasService.CrearNotaAsync(
                    dto.TipoEntidad,
                    dto.EntidadId,
                    dto.Contenido,
                    adminId,
                    dto.Prioridad,
                    dto.Tags
                );

                return Json(new
                {
                    success = true,
                    nota = new
                    {
                        id = nota.Id,
                        contenido = nota.Contenido,
                        prioridad = nota.Prioridad.ToString(),
                        fechaCreacion = nota.FechaCreacion.ToString("dd/MM/yyyy HH:mm"),
                        tags = nota.Tags
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error creando nota interna");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Obtener notas de una entidad específica
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerNotasEntidad(
            [FromServices] INotasInternasService notasService,
            TipoEntidadNota tipo,
            string entidadId)
        {
            try
            {
                var notas = await notasService.ObtenerNotasPorEntidadAsync(tipo, entidadId);

                return Json(new
                {
                    success = true,
                    notas = notas.Select(n => new
                    {
                        id = n.Id,
                        contenido = n.Contenido,
                        prioridad = n.Prioridad.ToString(),
                        prioridadValor = (int)n.Prioridad,
                        esFijada = n.EsFijada,
                        fechaCreacion = n.FechaCreacion.ToString("dd/MM/yyyy HH:mm"),
                        fechaEdicion = n.FechaEdicion?.ToString("dd/MM/yyyy HH:mm"),
                        creadoPor = n.CreadoPor?.NombreCompleto ?? n.CreadoPor?.UserName ?? "Admin",
                        editadoPor = n.EditadoPor?.NombreCompleto ?? n.EditadoPor?.UserName,
                        tags = n.Tags
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error obteniendo notas para {Tipo}/{EntidadId}", tipo, entidadId);
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Editar una nota existente
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarNotaInterna(
            [FromServices] INotasInternasService notasService,
            [FromBody] EditarNotaDto dto)
        {
            try
            {
                var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminId))
                    return Json(new { success = false, error = "No autenticado" });

                var nota = await notasService.EditarNotaAsync(
                    dto.Id,
                    dto.Contenido,
                    adminId,
                    dto.Prioridad,
                    dto.Tags
                );

                if (nota == null)
                    return Json(new { success = false, error = "Nota no encontrada" });

                return Json(new { success = true, mensaje = "Nota actualizada" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error editando nota {Id}", dto.Id);
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Eliminar una nota (soft delete)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarNotaInterna(
            [FromServices] INotasInternasService notasService,
            int id)
        {
            try
            {
                var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminId))
                    return Json(new { success = false, error = "No autenticado" });

                var resultado = await notasService.EliminarNotaAsync(id, adminId);

                if (!resultado)
                    return Json(new { success = false, error = "Nota no encontrada" });

                return Json(new { success = true, mensaje = "Nota eliminada" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error eliminando nota {Id}", id);
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Fijar o desfijar una nota
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FijarNotaInterna(
            [FromServices] INotasInternasService notasService,
            int id,
            bool fijar)
        {
            try
            {
                var resultado = await notasService.FijarNotaAsync(id, fijar);

                if (!resultado)
                    return Json(new { success = false, error = "Nota no encontrada" });

                return Json(new { success = true, mensaje = fijar ? "Nota fijada" : "Nota desfijada" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error fijando nota {Id}", id);
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Buscar notas
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> BuscarNotasInternas(
            [FromServices] INotasInternasService notasService,
            string? termino,
            TipoEntidadNota? tipo)
        {
            try
            {
                var notas = await notasService.BuscarNotasAsync(termino ?? "", tipo);

                return Json(new
                {
                    success = true,
                    notas = notas.Select(n => new
                    {
                        id = n.Id,
                        tipoEntidad = n.TipoEntidad.ToString(),
                        entidadId = n.EntidadId,
                        contenido = n.Contenido,
                        prioridad = n.Prioridad.ToString(),
                        esFijada = n.EsFijada,
                        fechaCreacion = n.FechaCreacion.ToString("dd/MM/yyyy HH:mm"),
                        creadoPor = n.CreadoPor?.NombreCompleto ?? n.CreadoPor?.UserName ?? "Admin",
                        tags = n.Tags
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error buscando notas");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Contar notas de una entidad (para badges)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ContarNotasEntidad(
            [FromServices] INotasInternasService notasService,
            TipoEntidadNota tipo,
            string entidadId)
        {
            try
            {
                var cantidad = await notasService.ContarNotasPorEntidadAsync(tipo, entidadId);
                return Json(new { success = true, cantidad });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error contando notas");
                return Json(new { success = false, error = ex.Message });
            }
        }
    }

    // DTOs para notas internas
    public class CrearNotaDto
    {
        public TipoEntidadNota TipoEntidad { get; set; }
        public string EntidadId { get; set; } = "";
        public string Contenido { get; set; } = "";
        public PrioridadNota Prioridad { get; set; } = PrioridadNota.Normal;
        public string? Tags { get; set; }
    }

    public class EditarNotaDto
    {
        public int Id { get; set; }
        public string Contenido { get; set; } = "";
        public PrioridadNota? Prioridad { get; set; }
        public string? Tags { get; set; }
    }
}
