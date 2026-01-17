using System.Text.Json;

namespace Lado.Services
{
    public interface IGiphyService
    {
        Task<GiphySearchResult> BuscarGifsAsync(string query, int limit = 20, int offset = 0);
        Task<GiphySearchResult> ObtenerTrendingAsync(int limit = 20, int offset = 0);
        Task<GiphySearchResult> ObtenerPorCategoriaAsync(string categoria, int limit = 20);
    }

    public class GiphyService : IGiphyService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GiphyService> _logger;
        private readonly string _apiKey;
        private const string BaseUrl = "https://api.giphy.com/v1/gifs";

        // Categorías predefinidas con términos de búsqueda
        private static readonly Dictionary<string, string> Categorias = new()
        {
            { "trending", "" },
            { "reacciones", "reaction" },
            { "amor", "love heart" },
            { "fiesta", "party celebrate" },
            { "risa", "funny laugh lol" },
            { "sorpresa", "surprised shocked omg" },
            { "triste", "sad cry" },
            { "aplausos", "clap applause" },
            { "bailar", "dance dancing" },
            { "comida", "food eating yummy" },
            { "animales", "animals cute pets" },
            { "deportes", "sports goal win" }
        };

        public GiphyService(HttpClient httpClient, IConfiguration configuration, ILogger<GiphyService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["Giphy:ApiKey"] ?? "";

            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogWarning("Giphy API Key no configurada. Usando modo demo.");
            }
        }

        public async Task<GiphySearchResult> BuscarGifsAsync(string query, int limit = 20, int offset = 0)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    return GetDemoResults(query);
                }

                var url = $"{BaseUrl}/search?api_key={_apiKey}&q={Uri.EscapeDataString(query)}&limit={limit}&offset={offset}&rating=pg-13&lang=es";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<GiphyApiResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return MapToResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar GIFs: {Query}", query);
                return new GiphySearchResult { Success = false, Error = "Error al buscar GIFs" };
            }
        }

        public async Task<GiphySearchResult> ObtenerTrendingAsync(int limit = 20, int offset = 0)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    return GetDemoResults("trending");
                }

                var url = $"{BaseUrl}/trending?api_key={_apiKey}&limit={limit}&offset={offset}&rating=pg-13";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<GiphyApiResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return MapToResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener GIFs trending");
                return new GiphySearchResult { Success = false, Error = "Error al cargar GIFs" };
            }
        }

        public async Task<GiphySearchResult> ObtenerPorCategoriaAsync(string categoria, int limit = 20)
        {
            var categoriaLower = categoria.ToLowerInvariant();

            if (categoriaLower == "trending")
            {
                return await ObtenerTrendingAsync(limit);
            }

            if (Categorias.TryGetValue(categoriaLower, out var searchTerm))
            {
                return await BuscarGifsAsync(searchTerm, limit);
            }

            // Si no es una categoría conocida, buscar directamente
            return await BuscarGifsAsync(categoria, limit);
        }

        private static GiphySearchResult MapToResult(GiphyApiResponse? response)
        {
            if (response?.Data == null)
            {
                return new GiphySearchResult { Success = false, Error = "Sin resultados" };
            }

            return new GiphySearchResult
            {
                Success = true,
                Gifs = response.Data.Select(g => new GifItem
                {
                    Id = g.Id ?? "",
                    Title = g.Title ?? "",
                    Url = g.Images?.FixedHeight?.Url ?? g.Images?.Original?.Url ?? "",
                    PreviewUrl = g.Images?.FixedHeightSmall?.Url ?? g.Images?.PreviewGif?.Url ?? "",
                    Width = int.TryParse(g.Images?.FixedHeight?.Width, out var w) ? w : 200,
                    Height = int.TryParse(g.Images?.FixedHeight?.Height, out var h) ? h : 200,
                    WebpUrl = g.Images?.FixedHeight?.Webp ?? "",
                    Mp4Url = g.Images?.FixedHeight?.Mp4 ?? ""
                }).ToList(),
                TotalCount = response.Pagination?.TotalCount ?? response.Data.Count,
                Offset = response.Pagination?.Offset ?? 0
            };
        }

        private static GiphySearchResult GetDemoResults(string query)
        {
            // GIFs de demostración cuando no hay API key (URLs estables con IDs populares)
            var demoGifs = new List<GifItem>
            {
                new() { Id = "l0MYt5jPR6QX5pnqM", Title = "Corazón", Url = "https://i.giphy.com/media/l0MYt5jPR6QX5pnqM/giphy.gif", PreviewUrl = "https://i.giphy.com/media/l0MYt5jPR6QX5pnqM/200w.gif", Width = 200, Height = 200 },
                new() { Id = "26u4cqiYI30juCOGY", Title = "Celebrar", Url = "https://i.giphy.com/media/26u4cqiYI30juCOGY/giphy.gif", PreviewUrl = "https://i.giphy.com/media/26u4cqiYI30juCOGY/200w.gif", Width = 200, Height = 150 },
                new() { Id = "3o6Zt6KHxJTbXCnSvu", Title = "OK", Url = "https://i.giphy.com/media/3o6Zt6KHxJTbXCnSvu/giphy.gif", PreviewUrl = "https://i.giphy.com/media/3o6Zt6KHxJTbXCnSvu/200w.gif", Width = 200, Height = 112 },
                new() { Id = "l4pTfx2qLszoacZRS", Title = "Aplausos", Url = "https://i.giphy.com/media/l4pTfx2qLszoacZRS/giphy.gif", PreviewUrl = "https://i.giphy.com/media/l4pTfx2qLszoacZRS/200w.gif", Width = 200, Height = 150 },
                new() { Id = "artj92V8o75VPL7AeQ", Title = "WOW", Url = "https://i.giphy.com/media/artj92V8o75VPL7AeQ/giphy.gif", PreviewUrl = "https://i.giphy.com/media/artj92V8o75VPL7AeQ/200w.gif", Width = 200, Height = 150 },
                new() { Id = "xUPGcguWZHRC2HyBRS", Title = "Fuego", Url = "https://i.giphy.com/media/xUPGcguWZHRC2HyBRS/giphy.gif", PreviewUrl = "https://i.giphy.com/media/xUPGcguWZHRC2HyBRS/200w.gif", Width = 200, Height = 150 }
            };

            return new GiphySearchResult
            {
                Success = true,
                Gifs = demoGifs,
                TotalCount = demoGifs.Count,
                IsDemo = true
            };
        }
    }

    // DTOs para respuesta de Giphy API
    public class GiphyApiResponse
    {
        public List<GiphyGif>? Data { get; set; }
        public GiphyPagination? Pagination { get; set; }
    }

    public class GiphyGif
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public GiphyImages? Images { get; set; }
    }

    public class GiphyImages
    {
        public GiphyImageFormat? Original { get; set; }
        public GiphyImageFormat? FixedHeight { get; set; }
        public GiphyImageFormat? FixedHeightSmall { get; set; }
        public GiphyImageFormat? PreviewGif { get; set; }
    }

    public class GiphyImageFormat
    {
        public string? Url { get; set; }
        public string? Width { get; set; }
        public string? Height { get; set; }
        public string? Webp { get; set; }
        public string? Mp4 { get; set; }
    }

    public class GiphyPagination
    {
        public int TotalCount { get; set; }
        public int Count { get; set; }
        public int Offset { get; set; }
    }

    // DTOs para la respuesta al frontend
    public class GiphySearchResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public List<GifItem> Gifs { get; set; } = new();
        public int TotalCount { get; set; }
        public int Offset { get; set; }
        public bool IsDemo { get; set; }
    }

    public class GifItem
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string PreviewUrl { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public string WebpUrl { get; set; } = "";
        public string Mp4Url { get; set; } = "";
    }
}
