using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Lado.Services;

namespace Lado.Controllers
{
    public partial class AdminController
    {
        // ========================================
        // BÚSQUEDA GLOBAL - ENDPOINTS
        // ========================================

        /// <summary>
        /// Búsqueda global en todas las entidades
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> BusquedaGlobal(
            [FromServices] IBusquedaGlobalService busquedaService,
            string q,
            int limite = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                    return Json(new { success = true, total = 0, resultados = new { } });

                var resultado = await busquedaService.BuscarAsync(q, limite);

                return Json(new
                {
                    success = true,
                    total = resultado.TotalResultados,
                    resultados = new
                    {
                        usuarios = resultado.Usuarios.Select(r => new
                        {
                            id = r.Id,
                            titulo = r.Titulo,
                            subtitulo = r.Subtitulo,
                            icono = r.Icono,
                            url = r.Url,
                            metadata = r.Metadata
                        }),
                        contenidos = resultado.Contenidos.Select(r => new
                        {
                            id = r.Id,
                            titulo = r.Titulo,
                            subtitulo = r.Subtitulo,
                            icono = r.Icono,
                            url = r.Url,
                            metadata = r.Metadata
                        }),
                        reportes = resultado.Reportes.Select(r => new
                        {
                            id = r.Id,
                            titulo = r.Titulo,
                            subtitulo = r.Subtitulo,
                            icono = r.Icono,
                            url = r.Url,
                            metadata = r.Metadata
                        }),
                        apelaciones = resultado.Apelaciones.Select(r => new
                        {
                            id = r.Id,
                            titulo = r.Titulo,
                            subtitulo = r.Subtitulo,
                            icono = r.Icono,
                            url = r.Url,
                            metadata = r.Metadata
                        }),
                        transacciones = resultado.Transacciones.Select(r => new
                        {
                            id = r.Id,
                            titulo = r.Titulo,
                            subtitulo = r.Subtitulo,
                            icono = r.Icono,
                            url = r.Url,
                            metadata = r.Metadata
                        })
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error en búsqueda global: {Query}", q);
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Búsqueda solo de usuarios
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> BuscarUsuariosGlobal(
            [FromServices] IBusquedaGlobalService busquedaService,
            string q,
            int limite = 20)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(q))
                    return Json(new { success = true, usuarios = new List<object>() });

                var usuarios = await busquedaService.BuscarUsuariosAsync(q, limite);

                return Json(new
                {
                    success = true,
                    usuarios = usuarios.Select(u => new
                    {
                        id = u.Id,
                        titulo = u.Titulo,
                        subtitulo = u.Subtitulo,
                        url = u.Url,
                        metadata = u.Metadata
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error buscando usuarios: {Query}", q);
                return Json(new { success = false, error = ex.Message });
            }
        }
    }
}
