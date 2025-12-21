using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    public interface IClaudeClassificationService
    {
        /// <summary>
        /// Clasifica contenido (imagen + descripcion) y devuelve el ID de la categoria
        /// </summary>
        Task<int?> ClasificarContenidoAsync(byte[]? imagenBytes, string? descripcion, string? mimeType = null);

        /// <summary>
        /// Clasifica contenido y devuelve resultado detallado
        /// </summary>
        Task<ClasificacionResultado> ClasificarContenidoDetalladoAsync(byte[]? imagenBytes, string? descripcion, string? mimeType = null);

        /// <summary>
        /// Clasifica solo por descripcion de texto
        /// </summary>
        Task<int?> ClasificarPorTextoAsync(string descripcion);
    }

    public class ClasificacionResultado
    {
        public bool Exito { get; set; }
        public int? CategoriaId { get; set; }
        public string? CategoriaNombre { get; set; }
        public bool CategoriaCreada { get; set; }
        public string? Error { get; set; }
        public string? DetalleError { get; set; }
        public long TiempoMs { get; set; }
    }

    public class ClaudeClassificationService : IClaudeClassificationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ClaudeClassificationService> _logger;

        private const string CLAUDE_API_URL = "https://api.anthropic.com/v1/messages";
        private const string CLAUDE_MODEL = "claude-3-haiku-20240307"; // Modelo rapido y economico

        public ClaudeClassificationService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ApplicationDbContext context,
            ILogger<ClaudeClassificationService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("Claude");
            _configuration = configuration;
            _context = context;
            _logger = logger;
        }

        public async Task<int?> ClasificarContenidoAsync(byte[]? imagenBytes, string? descripcion, string? mimeType = null)
        {
            try
            {
                var apiKey = _configuration["Claude:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogWarning("Claude API key no configurada");
                    return null;
                }

                // Obtener categorias disponibles
                var categorias = await ObtenerCategoriasAsync();
                if (!categorias.Any())
                {
                    _logger.LogWarning("No hay categorias de interes disponibles");
                    return null;
                }

                var categoriasTexto = string.Join("\n", categorias.Select(c => $"- ID:{c.Id} = {c.Nombre}"));

                // Construir el mensaje para Claude
                var content = new List<object>();

                // Agregar imagen si existe
                if (imagenBytes != null && imagenBytes.Length > 0)
                {
                    var base64Image = Convert.ToBase64String(imagenBytes);
                    var mediaType = mimeType ?? "image/jpeg";

                    content.Add(new
                    {
                        type = "image",
                        source = new
                        {
                            type = "base64",
                            media_type = mediaType,
                            data = base64Image
                        }
                    });
                }

                // Agregar texto con instrucciones
                var prompt = $@"Analiza el siguiente contenido y clasifícalo en UNA de estas categorías.

CATEGORÍAS DISPONIBLES:
{categoriasTexto}

{(string.IsNullOrEmpty(descripcion) ? "" : $"DESCRIPCIÓN DEL CONTENIDO:\n{descripcion}\n")}
{(imagenBytes != null ? "IMAGEN: Analiza la imagen adjunta.\n" : "")}

INSTRUCCIONES:
1. Analiza el contenido visual y/o la descripción
2. Determina la categoría más apropiada
3. Responde SOLO con el número ID de la categoría, nada más

RESPUESTA (solo el número ID):";

                content.Add(new
                {
                    type = "text",
                    text = prompt
                });

                var requestBody = new
                {
                    model = CLAUDE_MODEL,
                    max_tokens = 10,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = content
                        }
                    }
                };

                var request = new HttpRequestMessage(HttpMethod.Post, CLAUDE_API_URL);
                request.Headers.Add("x-api-key", apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error en Claude API: {StatusCode} - {Content}",
                        response.StatusCode, responseContent);
                    return null;
                }

                // Parsear respuesta
                var jsonResponse = JsonDocument.Parse(responseContent);
                var textResponse = jsonResponse.RootElement
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString();

                // Extraer el ID de la categoria
                if (int.TryParse(textResponse?.Trim(), out int categoriaId))
                {
                    // Verificar que la categoria existe
                    var categoriaValida = categorias.Any(c => c.Id == categoriaId);
                    if (categoriaValida)
                    {
                        _logger.LogInformation("Contenido clasificado en categoria {CategoriaId}", categoriaId);
                        return categoriaId;
                    }
                }

                _logger.LogWarning("Claude respondió con valor no válido: {Response}", textResponse);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al clasificar contenido con Claude");
                return null;
            }
        }

        public async Task<ClasificacionResultado> ClasificarContenidoDetalladoAsync(byte[]? imagenBytes, string? descripcion, string? mimeType = null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var resultado = new ClasificacionResultado();

            try
            {
                // Validar que hay contenido para clasificar
                if ((imagenBytes == null || imagenBytes.Length == 0) && string.IsNullOrWhiteSpace(descripcion))
                {
                    resultado.Error = "Sin contenido";
                    resultado.DetalleError = "No hay imagen ni descripcion para clasificar";
                    _logger.LogWarning("Intento de clasificacion sin contenido");
                    return resultado;
                }

                // Validar tamano de imagen (max 20MB para Claude)
                if (imagenBytes != null && imagenBytes.Length > 20 * 1024 * 1024)
                {
                    resultado.Error = "Imagen muy grande";
                    resultado.DetalleError = $"La imagen pesa {imagenBytes.Length / (1024 * 1024)}MB. Maximo permitido: 20MB";
                    _logger.LogWarning("Imagen demasiado grande para clasificar: {Size}MB", imagenBytes.Length / (1024 * 1024));
                    return resultado;
                }

                var apiKey = _configuration["Claude:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    resultado.Error = "API no configurada";
                    resultado.DetalleError = "La API Key de Claude no esta configurada en el servidor";
                    _logger.LogError("Claude API key no configurada");
                    return resultado;
                }

                // Obtener categorias disponibles
                var categorias = await ObtenerCategoriasAsync();
                if (!categorias.Any())
                {
                    resultado.Error = "Sin categorias";
                    resultado.DetalleError = "No hay categorias de interes configuradas en el sistema";
                    _logger.LogWarning("No hay categorias de interes disponibles");
                    return resultado;
                }

                var categoriasTexto = string.Join("\n", categorias.Select(c => $"- ID:{c.Id} = {c.Nombre}"));

                // Construir el mensaje para Claude
                var content = new List<object>();

                // Agregar imagen si existe
                if (imagenBytes != null && imagenBytes.Length > 0)
                {
                    var base64Image = Convert.ToBase64String(imagenBytes);
                    var mediaType = mimeType ?? "image/jpeg";

                    content.Add(new
                    {
                        type = "image",
                        source = new
                        {
                            type = "base64",
                            media_type = mediaType,
                            data = base64Image
                        }
                    });
                }

                // Agregar texto con instrucciones
                var prompt = $@"Analiza el siguiente contenido y clasifícalo.

CATEGORÍAS EXISTENTES:
{categoriasTexto}

{(string.IsNullOrEmpty(descripcion) ? "" : $"DESCRIPCIÓN DEL CONTENIDO:\n{descripcion}\n")}
{(imagenBytes != null ? "IMAGEN: Analiza la imagen adjunta.\n" : "")}

INSTRUCCIONES:
1. Analiza el contenido visual y/o la descripción
2. Si encaja en una categoría existente, responde SOLO con el número ID
3. Si NO encaja en ninguna categoría existente, responde con: NUEVA:NombreCategoria
   - El nombre debe ser corto (1-2 palabras), en español, sin tildes
   - Ejemplos: NUEVA:Mascotas, NUEVA:Deportes, NUEVA:Arte

RESPUESTA:";

                content.Add(new
                {
                    type = "text",
                    text = prompt
                });

                var requestBody = new
                {
                    model = CLAUDE_MODEL,
                    max_tokens = 50, // Aumentado para permitir NUEVA:NombreCategoria
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = content
                        }
                    }
                };

                var request = new HttpRequestMessage(HttpMethod.Post, CLAUDE_API_URL);
                request.Headers.Add("x-api-key", apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // Intentar extraer mensaje de error de la API
                    string errorMessage = $"Status: {(int)response.StatusCode}";
                    try
                    {
                        var errorJson = JsonDocument.Parse(responseContent);
                        if (errorJson.RootElement.TryGetProperty("error", out var errorProp))
                        {
                            if (errorProp.TryGetProperty("message", out var msgProp))
                            {
                                errorMessage = msgProp.GetString() ?? errorMessage;
                            }
                        }
                    }
                    catch { }

                    resultado.Error = $"Error API ({(int)response.StatusCode})";
                    resultado.DetalleError = errorMessage;
                    _logger.LogError("Error en Claude API: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return resultado;
                }

                // Parsear respuesta
                var jsonResponse = JsonDocument.Parse(responseContent);
                var textResponse = jsonResponse.RootElement
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString()?.Trim();

                // Verificar si es una categoria nueva
                if (textResponse != null && textResponse.StartsWith("NUEVA:", StringComparison.OrdinalIgnoreCase))
                {
                    var nombreNuevaCategoria = textResponse.Substring(6).Trim();
                    if (!string.IsNullOrEmpty(nombreNuevaCategoria) && nombreNuevaCategoria.Length <= 50)
                    {
                        // Crear la nueva categoria
                        var nuevaCategoria = await CrearNuevaCategoriaAsync(nombreNuevaCategoria);
                        if (nuevaCategoria != null)
                        {
                            resultado.Exito = true;
                            resultado.CategoriaId = nuevaCategoria.Id;
                            resultado.CategoriaNombre = nuevaCategoria.Nombre;
                            resultado.CategoriaCreada = true;
                            _logger.LogInformation("Nueva categoria creada por IA: {Nombre} (ID: {Id})", nuevaCategoria.Nombre, nuevaCategoria.Id);
                            return resultado;
                        }
                        else
                        {
                            resultado.Error = "Error al crear categoria";
                            resultado.DetalleError = $"No se pudo crear la categoria '{nombreNuevaCategoria}'";
                            return resultado;
                        }
                    }
                }

                // Extraer el ID de la categoria existente
                // Manejar formatos: "808", "ID:808", "ID: 808", etc.
                var cleanedResponse = textResponse ?? "";
                if (cleanedResponse.StartsWith("ID:", StringComparison.OrdinalIgnoreCase))
                {
                    cleanedResponse = cleanedResponse.Substring(3).Trim();
                }

                if (int.TryParse(cleanedResponse, out int categoriaId))
                {
                    // Verificar que la categoria existe
                    var categoriaValida = categorias.FirstOrDefault(c => c.Id == categoriaId);
                    if (categoriaValida != null)
                    {
                        resultado.Exito = true;
                        resultado.CategoriaId = categoriaId;
                        resultado.CategoriaNombre = categoriaValida.Nombre;
                        _logger.LogInformation("Contenido clasificado en categoria {CategoriaId}", categoriaId);
                        return resultado;
                    }
                    else
                    {
                        resultado.Error = "Categoria invalida";
                        resultado.DetalleError = $"La IA respondio con categoria {categoriaId} que no existe en el sistema";
                        _logger.LogWarning("Claude respondio con categoria invalida: {Response}", textResponse);
                        return resultado;
                    }
                }

                resultado.Error = "Respuesta invalida";
                resultado.DetalleError = $"La IA respondio: '{textResponse}' (se esperaba un numero o NUEVA:Nombre)";
                _logger.LogWarning("Claude respondio con valor no valido: {Response}", textResponse);
                return resultado;
            }
            catch (HttpRequestException ex)
            {
                resultado.Error = "Error de conexion";
                resultado.DetalleError = $"No se pudo conectar con la API de Claude: {ex.Message}";
                _logger.LogError(ex, "Error de conexion al clasificar contenido");
                return resultado;
            }
            catch (TaskCanceledException)
            {
                resultado.Error = "Timeout";
                resultado.DetalleError = "La solicitud a la API de Claude tardo demasiado";
                _logger.LogError("Timeout al clasificar contenido con Claude");
                return resultado;
            }
            catch (Exception ex)
            {
                resultado.Error = "Error interno";
                resultado.DetalleError = ex.Message;
                _logger.LogError(ex, "Error al clasificar contenido con Claude");
                return resultado;
            }
            finally
            {
                stopwatch.Stop();
                resultado.TiempoMs = stopwatch.ElapsedMilliseconds;
            }
        }

        public async Task<int?> ClasificarPorTextoAsync(string descripcion)
        {
            if (string.IsNullOrWhiteSpace(descripcion))
                return null;

            return await ClasificarContenidoAsync(null, descripcion);
        }

        private async Task<List<CategoriaSimple>> ObtenerCategoriasAsync()
        {
            return await _context.CategoriasIntereses
                .Where(c => c.EstaActiva && c.CategoriaPadreId == null) // Solo categorias principales
                .OrderBy(c => c.Orden)
                .Select(c => new CategoriaSimple { Id = c.Id, Nombre = c.Nombre })
                .ToListAsync();
        }

        private async Task<CategoriaInteres?> CrearNuevaCategoriaAsync(string nombre)
        {
            try
            {
                // Verificar si ya existe una categoria con ese nombre (case insensitive)
                var existente = await _context.CategoriasIntereses
                    .FirstOrDefaultAsync(c => c.Nombre.ToLower() == nombre.ToLower());

                if (existente != null)
                {
                    _logger.LogInformation("La categoria '{Nombre}' ya existe, usando existente (ID: {Id})", nombre, existente.Id);
                    return existente;
                }

                // Obtener el orden maximo actual
                var maxOrden = await _context.CategoriasIntereses
                    .Where(c => c.CategoriaPadreId == null)
                    .MaxAsync(c => (int?)c.Orden) ?? 0;

                // Generar un color aleatorio agradable
                var colores = new[] { "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4", "#FFEAA7",
                                      "#DDA0DD", "#98D8C8", "#F7DC6F", "#BB8FCE", "#85C1E9" };
                var random = new Random();
                var colorAleatorio = colores[random.Next(colores.Length)];

                // Crear la nueva categoria
                var nuevaCategoria = new CategoriaInteres
                {
                    Nombre = nombre,
                    Descripcion = $"Categoria creada automaticamente por IA",
                    Icono = "fas fa-tag", // Icono generico
                    Color = colorAleatorio,
                    Orden = maxOrden + 1,
                    EstaActiva = true,
                    CategoriaPadreId = null
                };

                _context.CategoriasIntereses.Add(nuevaCategoria);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Categoria creada automaticamente: {Nombre} (ID: {Id}, Color: {Color})",
                    nuevaCategoria.Nombre, nuevaCategoria.Id, nuevaCategoria.Color);

                return nuevaCategoria;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear categoria automatica: {Nombre}", nombre);
                return null;
            }
        }

        private class CategoriaSimple
        {
            public int Id { get; set; }
            public string Nombre { get; set; } = string.Empty;
        }
    }
}
