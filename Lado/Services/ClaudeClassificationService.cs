using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

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

        /// <summary>
        /// Detecta objetos visuales en una imagen y devuelve una lista de objetos con su confianza.
        /// Usado para busquedas tipo "mostrar contenido con motos".
        /// </summary>
        Task<List<ObjetoDetectado>> DetectarObjetosAsync(byte[]? imagenBytes, string? mimeType = null);

        /// <summary>
        /// Clasifica contenido Y detecta objetos en una sola llamada a Claude.
        /// Más eficiente que llamar a ambos métodos por separado.
        /// </summary>
        Task<ClasificacionConObjetosResultado> ClasificarYDetectarObjetosAsync(byte[]? imagenBytes, string? descripcion, string? mimeType = null);
    }

    /// <summary>
    /// Resultado combinado de clasificación y detección de objetos
    /// </summary>
    public class ClasificacionConObjetosResultado
    {
        public ClasificacionResultado Clasificacion { get; set; } = new();
        public List<ObjetoDetectado> ObjetosDetectados { get; set; } = new();
    }

    /// <summary>
    /// Representa un objeto detectado en una imagen con su nivel de confianza
    /// </summary>
    public class ObjetoDetectado
    {
        public string Nombre { get; set; } = string.Empty;
        public float Confianza { get; set; }
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
        /// <summary>
        /// Indica si el error es permanente (imagen corrupta, formato no soportado) y no se debe reintentar
        /// </summary>
        public bool EsErrorPermanente { get; set; }
    }

    public class ClaudeClassificationService : IClaudeClassificationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ClaudeClassificationService> _logger;
        private readonly ILogEventoService _logEventoService;

        private const string CLAUDE_API_URL = "https://api.anthropic.com/v1/messages";
        private const string CLAUDE_MODEL = "claude-3-haiku-20240307"; // Modelo rapido y economico

        // Rate limiting interno para evitar saturar la API
        private static readonly SemaphoreSlim _rateLimitSemaphore = new(3, 3); // Max 3 llamadas concurrentes
        private static DateTime _lastCallTime = DateTime.MinValue;
        private static readonly object _lockObj = new();
        private const int MIN_DELAY_MS = 500; // Minimo 500ms entre llamadas

        // Flag para registrar solo el primer error por sesión (evitar llenar logs)
        // Se resetea cada 5 minutos para capturar nuevos errores
        private static bool _primerErrorRegistrado = false;
        private static DateTime _ultimoResetError = DateTime.MinValue;
        private static readonly object _errorLock = new();

        public ClaudeClassificationService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ApplicationDbContext context,
            ILogger<ClaudeClassificationService> logger,
            ILogEventoService logEventoService)
        {
            _httpClient = httpClientFactory.CreateClient("Claude");
            _configuration = configuration;
            _context = context;
            _logger = logger;
            _logEventoService = logEventoService;
        }

        /// <summary>
        /// Registra solo el primer error en LogEventos para debug.
        /// Se resetea cada 5 minutos para capturar nuevos errores.
        /// </summary>
        private async Task RegistrarPrimerErrorAsync(string mensaje, string? detalle = null)
        {
            bool debeRegistrar = false;

            lock (_errorLock)
            {
                // Resetear cada 5 minutos
                if ((DateTime.UtcNow - _ultimoResetError).TotalMinutes > 5)
                {
                    _primerErrorRegistrado = false;
                    _ultimoResetError = DateTime.UtcNow;
                }

                if (!_primerErrorRegistrado)
                {
                    _primerErrorRegistrado = true;
                    debeRegistrar = true;
                }
            }

            if (debeRegistrar)
            {
                try
                {
                    await _logEventoService.RegistrarEventoAsync(
                        $"[Claude API] {mensaje}",
                        Models.CategoriaEvento.Sistema,
                        Models.TipoLogEvento.Error,
                        detalle: detalle
                    );
                }
                catch
                {
                    // Ignorar errores de logging
                }
            }
        }

        /// <summary>
        /// Normaliza una imagen a JPEG limpio para evitar errores de Claude ("Could not process image")
        /// Elimina metadatos problemáticos, convierte formatos no soportados, etc.
        /// </summary>
        private async Task<(byte[]? normalizada, string mimeType)> NormalizarImagenParaClaudeAsync(byte[] imagenOriginal)
        {
            try
            {
                using var inputStream = new MemoryStream(imagenOriginal);
                using var image = await Image.LoadAsync(inputStream);

                // Convertir a JPEG limpio con calidad alta
                using var outputStream = new MemoryStream();
                await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = 90 });

                var resultado = outputStream.ToArray();
                _logger.LogDebug("Imagen normalizada: {Original}KB -> {Nueva}KB",
                    imagenOriginal.Length / 1024, resultado.Length / 1024);

                return (resultado, "image/jpeg");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo normalizar imagen, usando original");
                return (null, "image/jpeg"); // Retornar null para usar la original
            }
        }

        /// <summary>
        /// Comprime una imagen para que no exceda el limite de Claude (5MB en base64 ≈ 3.5MB en bytes)
        /// </summary>
        private async Task<(byte[]? comprimida, string mimeType)> ComprimirImagenParaClaudeAsync(byte[] imagenOriginal, string? mimeType)
        {
            try
            {
                using var inputStream = new MemoryStream(imagenOriginal);
                using var image = await Image.LoadAsync(inputStream);

                // Calcular nueva resolucion manteniendo aspect ratio
                // Maximo 1920px en cualquier dimension para clasificacion (suficiente calidad)
                const int MAX_DIMENSION = 1920;
                int newWidth = image.Width;
                int newHeight = image.Height;

                if (image.Width > MAX_DIMENSION || image.Height > MAX_DIMENSION)
                {
                    if (image.Width > image.Height)
                    {
                        newWidth = MAX_DIMENSION;
                        newHeight = (int)(image.Height * (MAX_DIMENSION / (double)image.Width));
                    }
                    else
                    {
                        newHeight = MAX_DIMENSION;
                        newWidth = (int)(image.Width * (MAX_DIMENSION / (double)image.Height));
                    }

                    image.Mutate(x => x.Resize(newWidth, newHeight));
                }

                // Comprimir como JPEG con calidad progresivamente menor hasta que quepa
                int[] calidades = { 80, 70, 60, 50, 40 };
                const int LIMITE_BYTES = 3 * 1024 * 1024; // 3MB para que en base64 no exceda 5MB

                foreach (var calidad in calidades)
                {
                    using var outputStream = new MemoryStream();
                    var encoder = new JpegEncoder { Quality = calidad };
                    await image.SaveAsync(outputStream, encoder);

                    if (outputStream.Length <= LIMITE_BYTES)
                    {
                        return (outputStream.ToArray(), "image/jpeg");
                    }
                }

                // Si aun es muy grande, reducir mas la resolucion
                image.Mutate(x => x.Resize(1280, 0)); // 1280px de ancho, alto proporcional
                using var finalStream = new MemoryStream();
                await image.SaveAsync(finalStream, new JpegEncoder { Quality = 50 });

                if (finalStream.Length <= LIMITE_BYTES)
                {
                    return (finalStream.ToArray(), "image/jpeg");
                }

                // Ultimo intento: 800px
                image.Mutate(x => x.Resize(800, 0));
                using var lastStream = new MemoryStream();
                await image.SaveAsync(lastStream, new JpegEncoder { Quality = 50 });

                return (lastStream.ToArray(), "image/jpeg");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al comprimir imagen para Claude");
                return (null, "image/jpeg");
            }
        }

        /// <summary>
        /// Aplica rate limiting interno antes de llamar a Claude
        /// </summary>
        private async Task AplicarRateLimitAsync()
        {
            // Esperar turno en el semáforo (max 3 concurrentes)
            await _rateLimitSemaphore.WaitAsync();

            // Aplicar delay mínimo entre llamadas
            lock (_lockObj)
            {
                var elapsed = (DateTime.UtcNow - _lastCallTime).TotalMilliseconds;
                if (elapsed < MIN_DELAY_MS)
                {
                    var waitTime = (int)(MIN_DELAY_MS - elapsed);
                    if (waitTime > 0)
                    {
                        Thread.Sleep(waitTime);
                    }
                }
                _lastCallTime = DateTime.UtcNow;
            }
            // NO liberamos el semáforo aquí - se libera en LiberarRateLimit()
        }

        /// <summary>
        /// Libera el semáforo de rate limiting después de la llamada
        /// </summary>
        private void LiberarRateLimit()
        {
            try
            {
                _rateLimitSemaphore.Release();
            }
            catch (SemaphoreFullException)
            {
                // Ignorar si ya está lleno
            }
        }

        /// <summary>
        /// Registra un error de clasificación en /Admin/Logs
        /// </summary>
        private async Task RegistrarErrorEnLogAsync(string error, string? detalle)
        {
            try
            {
                await _logEventoService.RegistrarEventoAsync(
                    $"[Claude IA] {error}",
                    Models.CategoriaEvento.Sistema,
                    Models.TipoLogEvento.Error,
                    detalle: detalle?.Length > 500 ? detalle[..500] : detalle
                );
            }
            catch
            {
                // Ignorar errores de logging
            }
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

                // Obtener categorias con subcategorias
                var categoriasConSub = await ObtenerCategoriasConSubcategoriasAsync();
                bool sinCategorias = !categoriasConSub.Any();

                // Construir texto de categorías con subcategorías
                var categoriasTexto = new StringBuilder();
                if (sinCategorias)
                {
                    categoriasTexto.AppendLine("(No hay categorías existentes - debes crear nuevas)");
                }
                else
                {
                    foreach (var cat in categoriasConSub)
                    {
                        categoriasTexto.AppendLine($"- ID:{cat.Id} = {cat.Nombre} ({cat.ContadorContenido} items)");
                        foreach (var sub in cat.Subcategorias)
                        {
                            categoriasTexto.AppendLine($"  - ID:{sub.Id} = {sub.Nombre} (sub de {cat.Nombre})");
                        }
                    }
                }

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

                // Agregar texto con instrucciones - PROMPT MEJORADO PARA SUBCATEGORÍAS
                string prompt;
                if (sinCategorias)
                {
                    // Prompt especial cuando no hay categorías
                    prompt = $@"Analiza el siguiente contenido y CREA una categoría apropiada para él.

{(string.IsNullOrEmpty(descripcion) ? "" : $"DESCRIPCIÓN DEL CONTENIDO:\n{descripcion}\n")}
{(imagenBytes != null ? "IMAGEN: Analiza la imagen adjunta.\n" : "")}

INSTRUCCIONES:
1. Analiza el contenido visual y/o la descripción
2. Sugiere una categoría principal que agrupe este tipo de contenido
3. El nombre debe ser corto (1-2 palabras), en español, sin tildes
4. Ejemplos de categorías: Fitness, Moda, Cocina, Viajes, Arte, Musica, Mascotas, Naturaleza, etc.

FORMATO DE RESPUESTA (obligatorio usar NUEVA:):
NUEVA:NombreCategoria

Ejemplos válidos:
- NUEVA:Fitness
- NUEVA:Moda
- NUEVA:Arte

RESPUESTA:";
                }
                else
                {
                    prompt = $@"Analiza el siguiente contenido y clasifícalo en la categoría más específica posible.

CATEGORÍAS Y SUBCATEGORÍAS EXISTENTES:
{categoriasTexto}

{(string.IsNullOrEmpty(descripcion) ? "" : $"DESCRIPCIÓN DEL CONTENIDO:\n{descripcion}\n")}
{(imagenBytes != null ? "IMAGEN: Analiza la imagen adjunta.\n" : "")}

INSTRUCCIONES:
1. Analiza el contenido visual y/o la descripción
2. PRIORIZA categorías/subcategorías existentes si el contenido encaja bien
3. Formatos de respuesta válidos:
   - Solo ID: si encaja en una categoría/subcategoría existente (ej: 15)
   - NUEVA:Nombre: si necesita una categoría principal nueva (ej: NUEVA:Mascotas)
   - SUB:IdPadre/Nombre: si encaja en una categoría pero necesita subcategoría más específica
     (ej: SUB:5/Yoga significa crear subcategoría 'Yoga' dentro de categoría ID 5)

REGLAS PARA SUBCATEGORÍAS:
- Solo sugiere SUB: si la categoría padre tiene más de 10 items
- El nombre de subcategoría debe ser corto (1-2 palabras), en español, sin tildes
- Ejemplos: SUB:3/Retratos, SUB:7/Cardio, SUB:2/Postres

RESPUESTA (solo el formato indicado):";
                }

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

                // 1. Verificar si es una SUBCATEGORÍA nueva (formato SUB:IdPadre/Nombre)
                if (textResponse != null && textResponse.StartsWith("SUB:", StringComparison.OrdinalIgnoreCase))
                {
                    var subParts = textResponse.Substring(4).Trim();
                    var slashIndex = subParts.IndexOf('/');
                    if (slashIndex > 0)
                    {
                        var padreIdStr = subParts.Substring(0, slashIndex).Trim();
                        var nombreSubcategoria = subParts.Substring(slashIndex + 1).Trim();

                        if (int.TryParse(padreIdStr, out int padreId) &&
                            !string.IsNullOrEmpty(nombreSubcategoria) &&
                            nombreSubcategoria.Length <= 50)
                        {
                            // Verificar que la categoría padre existe y tiene suficiente contenido
                            var categoriaPadre = categoriasConSub.FirstOrDefault(c => c.Id == padreId);
                            if (categoriaPadre != null)
                            {
                                // Solo crear subcategoría si el padre tiene más de 10 items
                                if (categoriaPadre.ContadorContenido >= 10)
                                {
                                    var nuevaSubcategoria = await CrearSubcategoriaAsync(nombreSubcategoria, padreId);
                                    if (nuevaSubcategoria != null)
                                    {
                                        resultado.Exito = true;
                                        resultado.CategoriaId = nuevaSubcategoria.Id;
                                        resultado.CategoriaNombre = $"{categoriaPadre.Nombre} > {nuevaSubcategoria.Nombre}";
                                        resultado.CategoriaCreada = true;
                                        _logger.LogInformation("Nueva subcategoría creada por IA: {Nombre} bajo {Padre}",
                                            nuevaSubcategoria.Nombre, categoriaPadre.Nombre);
                                        return resultado;
                                    }
                                }
                                else
                                {
                                    // Si no tiene suficiente contenido, usar la categoría padre
                                    resultado.Exito = true;
                                    resultado.CategoriaId = padreId;
                                    resultado.CategoriaNombre = categoriaPadre.Nombre;
                                    _logger.LogInformation("Categoría padre {Nombre} tiene pocos items ({Count}), no se crea subcategoría",
                                        categoriaPadre.Nombre, categoriaPadre.ContadorContenido);
                                    return resultado;
                                }
                            }
                        }
                    }
                }

                // 2. Verificar si es una CATEGORÍA PRINCIPAL nueva
                if (textResponse != null && textResponse.StartsWith("NUEVA:", StringComparison.OrdinalIgnoreCase))
                {
                    var nombreNuevaCategoria = textResponse.Substring(6).Trim();
                    if (!string.IsNullOrEmpty(nombreNuevaCategoria) && nombreNuevaCategoria.Length <= 50)
                    {
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

                // 3. Extraer el ID de la categoria/subcategoria existente
                var cleanedResponse = textResponse ?? "";
                if (cleanedResponse.StartsWith("ID:", StringComparison.OrdinalIgnoreCase))
                {
                    cleanedResponse = cleanedResponse.Substring(3).Trim();
                }

                if (int.TryParse(cleanedResponse, out int categoriaId))
                {
                    // Buscar en categorías principales
                    var categoriaValida = categoriasConSub.FirstOrDefault(c => c.Id == categoriaId);
                    if (categoriaValida != null)
                    {
                        resultado.Exito = true;
                        resultado.CategoriaId = categoriaId;
                        resultado.CategoriaNombre = categoriaValida.Nombre;
                        _logger.LogInformation("Contenido clasificado en categoria {CategoriaId}", categoriaId);
                        return resultado;
                    }

                    // Buscar en subcategorías
                    foreach (var cat in categoriasConSub)
                    {
                        var subValida = cat.Subcategorias.FirstOrDefault(s => s.Id == categoriaId);
                        if (subValida != null)
                        {
                            resultado.Exito = true;
                            resultado.CategoriaId = categoriaId;
                            resultado.CategoriaNombre = $"{cat.Nombre} > {subValida.Nombre}";
                            _logger.LogInformation("Contenido clasificado en subcategoria {CategoriaId}", categoriaId);
                            return resultado;
                        }
                    }

                    // ID no encontrado en ninguna categoría
                    resultado.Error = "Categoria invalida";
                    resultado.DetalleError = $"La IA respondio con categoria {categoriaId} que no existe en el sistema";
                    _logger.LogWarning("Claude respondio con categoria invalida: {Response}", textResponse);
                    return resultado;
                }

                resultado.Error = "Respuesta invalida";
                resultado.DetalleError = $"La IA respondio: '{textResponse}' (se esperaba ID, NUEVA:Nombre o SUB:Id/Nombre)";
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

        public async Task<List<ObjetoDetectado>> DetectarObjetosAsync(byte[]? imagenBytes, string? mimeType = null)
        {
            var objetosDetectados = new List<ObjetoDetectado>();

            try
            {
                // Validar que hay imagen
                if (imagenBytes == null || imagenBytes.Length == 0)
                {
                    _logger.LogDebug("No hay imagen para detectar objetos");
                    return objetosDetectados;
                }

                // Validar tamaño de imagen (max 5MB para detección de objetos)
                if (imagenBytes.Length > 5 * 1024 * 1024)
                {
                    _logger.LogWarning("Imagen muy grande para detección de objetos: {Size}MB", imagenBytes.Length / (1024 * 1024));
                    return objetosDetectados;
                }

                var apiKey = _configuration["Claude:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogWarning("Claude API key no configurada para detección de objetos");
                    return objetosDetectados;
                }

                // Construir el mensaje para Claude
                var content = new List<object>();

                // Agregar imagen
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

                // Prompt para detectar objetos
                var prompt = @"Analiza esta imagen y lista los objetos principales visibles.

INSTRUCCIONES:
1. Identifica entre 3 y 8 objetos principales
2. Usa palabras simples en español, minúsculas, sin tildes, en singular
3. Asigna un nivel de confianza entre 0.7 y 1.0 a cada objeto
4. Prioriza objetos relevantes y distintivos (no incluyas objetos genéricos como 'fondo', 'luz')

FORMATO DE RESPUESTA (JSON estricto):
[{""objeto"": ""moto"", ""confianza"": 0.95}, {""objeto"": ""casco"", ""confianza"": 0.88}]

Solo responde con el JSON, sin explicaciones adicionales.";

                content.Add(new
                {
                    type = "text",
                    text = prompt
                });

                var requestBody = new
                {
                    model = CLAUDE_MODEL,
                    max_tokens = 300, // Suficiente para lista de objetos
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
                    _logger.LogError("Error en Claude API (detección objetos): {StatusCode} - {Content}",
                        response.StatusCode, responseContent);
                    return objetosDetectados;
                }

                // Parsear respuesta
                var jsonResponse = JsonDocument.Parse(responseContent);
                var textResponse = jsonResponse.RootElement
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString()?.Trim();

                if (string.IsNullOrEmpty(textResponse))
                {
                    _logger.LogWarning("Claude no devolvió objetos detectados");
                    return objetosDetectados;
                }

                // Intentar parsear el JSON de objetos
                try
                {
                    // Limpiar la respuesta en caso de que tenga texto extra
                    var jsonStart = textResponse.IndexOf('[');
                    var jsonEnd = textResponse.LastIndexOf(']');
                    if (jsonStart >= 0 && jsonEnd > jsonStart)
                    {
                        textResponse = textResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    }

                    var objetosJson = JsonSerializer.Deserialize<List<ObjetoJsonResponse>>(textResponse,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (objetosJson != null)
                    {
                        foreach (var obj in objetosJson)
                        {
                            if (!string.IsNullOrEmpty(obj.Objeto) && obj.Confianza >= 0.7f)
                            {
                                // Normalizar: minúsculas, sin tildes, sin espacios extra
                                var nombreNormalizado = NormalizarNombreObjeto(obj.Objeto);

                                if (!string.IsNullOrEmpty(nombreNormalizado) && nombreNormalizado.Length <= 50)
                                {
                                    objetosDetectados.Add(new ObjetoDetectado
                                    {
                                        Nombre = nombreNormalizado,
                                        Confianza = Math.Min(1.0f, obj.Confianza)
                                    });
                                }
                            }
                        }
                    }

                    _logger.LogInformation("Detectados {Count} objetos en imagen", objetosDetectados.Count);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Error al parsear JSON de objetos: {Response}", textResponse);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al detectar objetos con Claude");
            }

            return objetosDetectados;
        }

        /// <summary>
        /// Normaliza el nombre del objeto: minúsculas, sin tildes, sin espacios extra
        /// </summary>
        private string NormalizarNombreObjeto(string nombre)
        {
            if (string.IsNullOrEmpty(nombre))
                return string.Empty;

            // Convertir a minúsculas
            nombre = nombre.ToLowerInvariant().Trim();

            // Reemplazar tildes
            nombre = nombre
                .Replace('á', 'a').Replace('é', 'e').Replace('í', 'i')
                .Replace('ó', 'o').Replace('ú', 'u').Replace('ü', 'u')
                .Replace('ñ', 'n');

            // Solo permitir letras, números y espacios
            var chars = nombre.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray();
            nombre = new string(chars);

            // Eliminar espacios múltiples
            while (nombre.Contains("  "))
                nombre = nombre.Replace("  ", " ");

            return nombre.Trim();
        }

        /// <summary>
        /// Clase auxiliar para deserializar respuesta de objetos de Claude
        /// </summary>
        private class ObjetoJsonResponse
        {
            public string Objeto { get; set; } = string.Empty;
            public float Confianza { get; set; }
        }

        /// <summary>
        /// Clase auxiliar para deserializar respuesta combinada de Claude
        /// </summary>
        private class RespuestaCombinada
        {
            public string? Categoria { get; set; }
            public List<ObjetoJsonResponse>? Objetos { get; set; }
        }

        public async Task<ClasificacionConObjetosResultado> ClasificarYDetectarObjetosAsync(byte[]? imagenBytes, string? descripcion, string? mimeType = null)
        {
            var resultado = new ClasificacionConObjetosResultado();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Validar que hay contenido para clasificar
                if ((imagenBytes == null || imagenBytes.Length == 0) && string.IsNullOrWhiteSpace(descripcion))
                {
                    resultado.Clasificacion.Error = "Sin contenido";
                    resultado.Clasificacion.DetalleError = "No hay imagen ni descripcion para clasificar";
                    return resultado;
                }

                // Normalizar imagen para evitar errores de Claude ("Could not process image")
                // Convertir a JPEG limpio sin metadatos problemáticos
                if (imagenBytes != null && imagenBytes.Length > 0)
                {
                    try
                    {
                        var (normalizada, nuevoMime) = await NormalizarImagenParaClaudeAsync(imagenBytes);
                        if (normalizada != null && normalizada.Length > 0)
                        {
                            imagenBytes = normalizada;
                            mimeType = nuevoMime;
                        }
                        // Si normalizada es null, se usa la imagen original
                    }
                    catch (Exception ex)
                    {
                        // Si falla la normalización, continuar con la imagen original
                        _logger.LogWarning(ex, "Error en normalización, usando imagen original");
                    }
                }

                // Si la imagen sigue siendo muy grande, comprimirla más
                // Limite real de Claude es 5MB en base64, que aumenta ~37%, asi que usamos 3MB como limite seguro
                const int LIMITE_BYTES = 3 * 1024 * 1024; // 3MB
                if (imagenBytes != null && imagenBytes.Length > LIMITE_BYTES)
                {
                    _logger.LogInformation("Imagen de {Size}MB excede limite, comprimiendo...",
                        imagenBytes.Length / (1024.0 * 1024.0));

                    var (comprimida, nuevoMimeType) = await ComprimirImagenParaClaudeAsync(imagenBytes, mimeType);
                    if (comprimida != null)
                    {
                        imagenBytes = comprimida;
                        mimeType = nuevoMimeType;
                        _logger.LogInformation("Imagen comprimida a {Size}MB",
                            imagenBytes.Length / (1024.0 * 1024.0));
                    }
                    else
                    {
                        resultado.Clasificacion.Error = "Error al comprimir imagen";
                        resultado.Clasificacion.DetalleError = "No se pudo reducir el tamano de la imagen";
                        return resultado;
                    }
                }

                var apiKey = _configuration["Claude:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    resultado.Clasificacion.Error = "API no configurada";
                    return resultado;
                }

                // Obtener categorias con subcategorias
                var categoriasConSub = await ObtenerCategoriasConSubcategoriasAsync();
                bool sinCategorias = !categoriasConSub.Any();

                // Construir texto de categorías
                var categoriasTexto = new StringBuilder();
                if (sinCategorias)
                {
                    categoriasTexto.AppendLine("(No hay categorías existentes)");
                }
                else
                {
                    foreach (var cat in categoriasConSub)
                    {
                        categoriasTexto.AppendLine($"- ID:{cat.Id} = {cat.Nombre}");
                        foreach (var sub in cat.Subcategorias)
                        {
                            categoriasTexto.AppendLine($"  - ID:{sub.Id} = {sub.Nombre}");
                        }
                    }
                }

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

                // Prompt COMBINADO para clasificacion Y deteccion de objetos
                var prompt = $@"Analiza la IMAGEN y clasifica segun el SUJETO PRINCIPAL.

CATEGORIAS EXISTENTES:
{categoriasTexto}
{(string.IsNullOrEmpty(descripcion) ? "" : $"\n(Descripcion: {descripcion})\n")}

INSTRUCCIONES:
1. Identifica el SUJETO PRINCIPAL de la imagen
2. Si existe una categoria apropiada en la lista, usa su ID
3. Si NO existe categoria apropiada, CREA UNA NUEVA con nombre descriptivo

EJEMPLOS de cuando CREAR categoria nueva:
- Mujer en bikini/playa -> NUEVA:Playa o NUEVA:Verano
- Mujer en vestido elegante -> NUEVA:Moda
- Hombre fitness/gym -> NUEVA:Fitness
- Persona con mascota -> NUEVA:Mascotas
- Comida/plato -> NUEVA:Gastronomia
- Paisaje natural -> NUEVA:Naturaleza
- Moto/carro (sin persona) -> NUEVA:Vehiculos
- Selfie casual -> NUEVA:Selfies

NO uses categorias de objetos secundarios. El sujeto principal manda.

Lista tambien 3-6 objetos visibles (espanol, minusculas, sin tildes).

RESPONDE SOLO JSON (usa numero de ID si existe, o NUEVA:Nombre si no):
{{""categoria"": ""5"", ""objetos"": [{{""objeto"": ""mujer"", ""confianza"": 0.95}}]}}

Otro ejemplo creando categoria:
{{""categoria"": ""NUEVA:Ciencia Ficcion"", ""objetos"": [{{""objeto"": ""personaje"", ""confianza"": 0.95}}]}}";

                content.Add(new
                {
                    type = "text",
                    text = prompt
                });

                var requestBody = new
                {
                    model = CLAUDE_MODEL,
                    max_tokens = 400,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = content
                        }
                    }
                };

                // Intentar hasta 2 veces en caso de error temporal
                HttpResponseMessage response = null!;
                string responseContent = string.Empty;
                int maxRetries = 2;

                for (int intento = 1; intento <= maxRetries; intento++)
                {
                    // Aplicar rate limiting antes de llamar a Claude
                    await AplicarRateLimitAsync();

                    try
                    {
                        var request = new HttpRequestMessage(HttpMethod.Post, CLAUDE_API_URL);
                        request.Headers.Add("x-api-key", apiKey);
                        request.Headers.Add("anthropic-version", "2023-06-01");
                        request.Content = new StringContent(
                            JsonSerializer.Serialize(requestBody),
                            Encoding.UTF8,
                            "application/json"
                        );

                        response = await _httpClient.SendAsync(request);
                        responseContent = await response.Content.ReadAsStringAsync();

                        // Si tuvo éxito o es un error permanente, salir del loop
                        if (response.IsSuccessStatusCode ||
                            (int)response.StatusCode != 429 && (int)response.StatusCode != 500 && (int)response.StatusCode != 503)
                        {
                            break;
                        }

                        // Error temporal (429, 500, 503) - reintentar después de esperar
                        if (intento < maxRetries)
                        {
                            _logger.LogWarning("Reintentando clasificación (intento {Intento}/{Max}) después de error {Code}",
                                intento, maxRetries, (int)response.StatusCode);
                            await Task.Delay(1000 * intento); // Esperar 1s, 2s, etc.
                        }
                    }
                    finally
                    {
                        // Siempre liberar el semáforo después de la llamada HTTP
                        LiberarRateLimit();
                    }
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = $"Error API ({(int)response.StatusCode})";
                    var errorDetalle = responseContent.Length > 500 ? responseContent[..500] : responseContent;

                    // Detectar errores específicos de Claude
                    if ((int)response.StatusCode == 429)
                    {
                        resultado.Clasificacion.Error = "Rate limit";
                    }
                    else if (responseContent.Contains("Could not process image"))
                    {
                        resultado.Clasificacion.Error = "Imagen corrupta";
                        resultado.Clasificacion.EsErrorPermanente = true; // Marcar para no reintentar
                    }
                    else if (responseContent.Contains("exceeds 5 MB"))
                    {
                        resultado.Clasificacion.Error = "Imagen muy grande";
                        resultado.Clasificacion.EsErrorPermanente = true; // No se puede comprimir más
                    }
                    else
                    {
                        resultado.Clasificacion.Error = errorMsg;
                    }
                    resultado.Clasificacion.DetalleError = errorDetalle;

                    // Registrar TODOS los errores en /Admin/Logs
                    await RegistrarErrorEnLogAsync(resultado.Clasificacion.Error, errorDetalle);

                    return resultado;
                }

                // Parsear respuesta
                var jsonResponse = JsonDocument.Parse(responseContent);
                var textResponse = jsonResponse.RootElement
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString()?.Trim();

                if (string.IsNullOrEmpty(textResponse))
                {
                    resultado.Clasificacion.Error = "Respuesta vacia";
                    return resultado;
                }

                // Limpiar y parsear JSON
                try
                {
                    var jsonStart = textResponse.IndexOf('{');
                    var jsonEnd = textResponse.LastIndexOf('}');
                    if (jsonStart >= 0 && jsonEnd > jsonStart)
                    {
                        textResponse = textResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    }

                    var respuestaCombinada = JsonSerializer.Deserialize<RespuestaCombinada>(textResponse,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (respuestaCombinada != null)
                    {
                        // Procesar CATEGORIA
                        if (!string.IsNullOrEmpty(respuestaCombinada.Categoria))
                        {
                            var catStr = respuestaCombinada.Categoria.Trim();

                            // Limpiar prefijo "ID:" si Claude lo incluye
                            if (catStr.StartsWith("ID:", StringComparison.OrdinalIgnoreCase))
                            {
                                catStr = catStr.Substring(3).Trim();
                            }

                            if (catStr.StartsWith("NUEVA:", StringComparison.OrdinalIgnoreCase))
                            {
                                var nombreNueva = catStr.Substring(6).Trim();
                                if (!string.IsNullOrEmpty(nombreNueva) && nombreNueva.Length <= 50)
                                {
                                    var nuevaCategoria = await CrearNuevaCategoriaAsync(nombreNueva);
                                    if (nuevaCategoria != null)
                                    {
                                        resultado.Clasificacion.Exito = true;
                                        resultado.Clasificacion.CategoriaId = nuevaCategoria.Id;
                                        resultado.Clasificacion.CategoriaNombre = nuevaCategoria.Nombre;
                                        resultado.Clasificacion.CategoriaCreada = true;
                                    }
                                }
                            }
                            else if (int.TryParse(catStr, out int categoriaId))
                            {
                                var categoriaValida = categoriasConSub.FirstOrDefault(c => c.Id == categoriaId);
                                if (categoriaValida != null)
                                {
                                    resultado.Clasificacion.Exito = true;
                                    resultado.Clasificacion.CategoriaId = categoriaId;
                                    resultado.Clasificacion.CategoriaNombre = categoriaValida.Nombre;
                                }
                                else
                                {
                                    // Buscar en subcategorías
                                    foreach (var cat in categoriasConSub)
                                    {
                                        var subValida = cat.Subcategorias.FirstOrDefault(s => s.Id == categoriaId);
                                        if (subValida != null)
                                        {
                                            resultado.Clasificacion.Exito = true;
                                            resultado.Clasificacion.CategoriaId = categoriaId;
                                            resultado.Clasificacion.CategoriaNombre = $"{cat.Nombre} > {subValida.Nombre}";
                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Claude devolvió un nombre de categoría en lugar de ID
                                // Buscar por nombre o crear nueva
                                var catPorNombre = categoriasConSub.FirstOrDefault(c =>
                                    c.Nombre.Equals(catStr, StringComparison.OrdinalIgnoreCase));

                                if (catPorNombre != null)
                                {
                                    resultado.Clasificacion.Exito = true;
                                    resultado.Clasificacion.CategoriaId = catPorNombre.Id;
                                    resultado.Clasificacion.CategoriaNombre = catPorNombre.Nombre;
                                }
                                else
                                {
                                    // Crear nueva categoría con el nombre dado
                                    var nuevaCategoria = await CrearNuevaCategoriaAsync(catStr);
                                    if (nuevaCategoria != null)
                                    {
                                        resultado.Clasificacion.Exito = true;
                                        resultado.Clasificacion.CategoriaId = nuevaCategoria.Id;
                                        resultado.Clasificacion.CategoriaNombre = nuevaCategoria.Nombre;
                                        resultado.Clasificacion.CategoriaCreada = true;
                                    }
                                }
                            }
                        }

                        // Procesar OBJETOS
                        if (respuestaCombinada.Objetos != null)
                        {
                            foreach (var obj in respuestaCombinada.Objetos)
                            {
                                if (!string.IsNullOrEmpty(obj.Objeto) && obj.Confianza >= 0.7f)
                                {
                                    var nombreNormalizado = NormalizarNombreObjeto(obj.Objeto);
                                    if (!string.IsNullOrEmpty(nombreNormalizado) && nombreNormalizado.Length <= 50)
                                    {
                                        resultado.ObjetosDetectados.Add(new ObjetoDetectado
                                        {
                                            Nombre = nombreNormalizado,
                                            Confianza = Math.Min(1.0f, obj.Confianza)
                                        });
                                    }
                                }
                            }
                        }

                        _logger.LogInformation("Clasificacion+Objetos: Cat={CatId}, Objetos={Count}",
                            resultado.Clasificacion.CategoriaId, resultado.ObjetosDetectados.Count);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Error al parsear JSON combinado: {Response}", textResponse);
                    resultado.Clasificacion.Error = "Error parsing JSON";
                    resultado.Clasificacion.DetalleError = textResponse?.Length > 200 ? textResponse[..200] : textResponse;
                    await RegistrarErrorEnLogAsync("Error parsing JSON", resultado.Clasificacion.DetalleError);
                }
            }
            catch (HttpRequestException ex)
            {
                resultado.Clasificacion.Error = "Error de conexión";
                resultado.Clasificacion.DetalleError = ex.Message;
                await RegistrarErrorEnLogAsync("Error de conexión HTTP", ex.Message);
            }
            catch (TaskCanceledException ex)
            {
                resultado.Clasificacion.Error = "Timeout";
                resultado.Clasificacion.DetalleError = "La solicitud tardó demasiado";
                await RegistrarErrorEnLogAsync("Timeout en solicitud", ex.Message);
            }
            catch (Exception ex)
            {
                resultado.Clasificacion.Error = "Error interno";
                resultado.Clasificacion.DetalleError = ex.Message;
                await RegistrarErrorEnLogAsync($"Exception: {ex.GetType().Name}", ex.Message);
            }
            finally
            {
                stopwatch.Stop();
                resultado.Clasificacion.TiempoMs = stopwatch.ElapsedMilliseconds;
            }

            return resultado;
        }

        private async Task<List<CategoriaSimple>> ObtenerCategoriasAsync()
        {
            return await _context.CategoriasIntereses
                .Where(c => c.EstaActiva && c.CategoriaPadreId == null) // Solo categorias principales
                .OrderBy(c => c.Orden)
                .Select(c => new CategoriaSimple { Id = c.Id, Nombre = c.Nombre })
                .ToListAsync();
        }

        private async Task<List<CategoriaConSubcategorias>> ObtenerCategoriasConSubcategoriasAsync()
        {
            var categoriasPrincipales = await _context.CategoriasIntereses
                .Where(c => c.EstaActiva && c.CategoriaPadreId == null)
                .OrderBy(c => c.Orden)
                .Select(c => new CategoriaConSubcategorias
                {
                    Id = c.Id,
                    Nombre = c.Nombre,
                    ContadorContenido = _context.Contenidos.Count(con => con.CategoriaInteresId == c.Id && con.EstaActivo)
                })
                .ToListAsync();

            // Obtener subcategorías para cada categoría principal
            var categoriasIds = categoriasPrincipales.Select(c => c.Id).ToList();
            var subcategorias = await _context.CategoriasIntereses
                .Where(c => c.EstaActiva && c.CategoriaPadreId.HasValue && categoriasIds.Contains(c.CategoriaPadreId.Value))
                .Select(c => new { c.Id, c.Nombre, c.CategoriaPadreId })
                .ToListAsync();

            foreach (var cat in categoriasPrincipales)
            {
                cat.Subcategorias = subcategorias
                    .Where(s => s.CategoriaPadreId == cat.Id)
                    .Select(s => new CategoriaSimple { Id = s.Id, Nombre = s.Nombre })
                    .ToList();
            }

            return categoriasPrincipales;
        }

        private async Task<int> ContarContenidoEnCategoriaAsync(int categoriaId)
        {
            return await _context.Contenidos
                .CountAsync(c => c.CategoriaInteresId == categoriaId && c.EstaActivo && !c.EsBorrador);
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

        private class CategoriaConSubcategorias
        {
            public int Id { get; set; }
            public string Nombre { get; set; } = string.Empty;
            public int ContadorContenido { get; set; }
            public List<CategoriaSimple> Subcategorias { get; set; } = new();
        }

        private async Task<CategoriaInteres?> CrearSubcategoriaAsync(string nombre, int categoriaPadreId)
        {
            try
            {
                // Verificar que la categoría padre existe
                var categoriaPadre = await _context.CategoriasIntereses
                    .FirstOrDefaultAsync(c => c.Id == categoriaPadreId && c.EstaActiva);

                if (categoriaPadre == null)
                {
                    _logger.LogWarning("Categoria padre {Id} no existe", categoriaPadreId);
                    return null;
                }

                // Verificar si ya existe una subcategoría con ese nombre en esa categoría
                var existente = await _context.CategoriasIntereses
                    .FirstOrDefaultAsync(c => c.Nombre.ToLower() == nombre.ToLower()
                        && c.CategoriaPadreId == categoriaPadreId);

                if (existente != null)
                {
                    _logger.LogInformation("La subcategoria '{Nombre}' ya existe en {Padre}, usando existente (ID: {Id})",
                        nombre, categoriaPadre.Nombre, existente.Id);
                    return existente;
                }

                // Obtener el orden máximo de subcategorías de esta categoría
                var maxOrden = await _context.CategoriasIntereses
                    .Where(c => c.CategoriaPadreId == categoriaPadreId)
                    .MaxAsync(c => (int?)c.Orden) ?? 0;

                // Heredar color del padre o generar uno similar
                var colorBase = categoriaPadre.Color ?? "#808080";
                var colorSubcategoria = AjustarColor(colorBase);

                // Crear la nueva subcategoría
                var nuevaSubcategoria = new CategoriaInteres
                {
                    Nombre = nombre,
                    Descripcion = $"Subcategoría de {categoriaPadre.Nombre} (creada por IA)",
                    Icono = categoriaPadre.Icono ?? "fas fa-tag",
                    Color = colorSubcategoria,
                    Orden = maxOrden + 1,
                    EstaActiva = true,
                    CategoriaPadreId = categoriaPadreId
                };

                _context.CategoriasIntereses.Add(nuevaSubcategoria);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Subcategoría creada: {Nombre} (ID: {Id}) bajo {Padre}",
                    nuevaSubcategoria.Nombre, nuevaSubcategoria.Id, categoriaPadre.Nombre);

                return nuevaSubcategoria;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear subcategoría: {Nombre} bajo categoría {PadreId}", nombre, categoriaPadreId);
                return null;
            }
        }

        private string AjustarColor(string colorBase)
        {
            try
            {
                // Ajustar ligeramente el color para diferenciar subcategorías
                if (colorBase.StartsWith("#") && colorBase.Length == 7)
                {
                    var r = Convert.ToInt32(colorBase.Substring(1, 2), 16);
                    var g = Convert.ToInt32(colorBase.Substring(3, 2), 16);
                    var b = Convert.ToInt32(colorBase.Substring(5, 2), 16);

                    // Aclarar un poco el color
                    r = Math.Min(255, r + 30);
                    g = Math.Min(255, g + 30);
                    b = Math.Min(255, b + 30);

                    return $"#{r:X2}{g:X2}{b:X2}";
                }
            }
            catch { }
            return colorBase;
        }
    }
}
