using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lado.Services
{
    #region Settings

    public class GooglePhotosSettings
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
    }

    #endregion

    #region Interface

    public interface IGooglePhotosService
    {
        string GenerarUrlAutorizacion(string state, string? redirectUri = null);
        Task<GooglePhotosTokenResponse?> IntercambiarCodigoAsync(string code, string? redirectUri = null);
        Task<GooglePhotosTokenResponse?> RefrescarTokenAsync(string refreshToken);
        Task<GooglePhotosAlbumList?> ObtenerAlbumesAsync(string accessToken, string? pageToken = null);
        Task<GooglePhotosMediaList?> ObtenerMediaItemsAsync(string accessToken, string? albumId = null, string? pageToken = null);
        Task<byte[]?> DescargarMediaAsync(string accessToken, string baseUrl, bool esVideo);
    }

    #endregion

    #region DTOs

    public class GooglePhotosTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }
    }

    public class GooglePhotosAlbumList
    {
        [JsonPropertyName("albums")]
        public List<GooglePhotosAlbum> Albums { get; set; } = new();

        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; set; }
    }

    public class GooglePhotosAlbum
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("productUrl")]
        public string? ProductUrl { get; set; }

        [JsonPropertyName("coverPhotoBaseUrl")]
        public string? CoverPhotoBaseUrl { get; set; }

        [JsonPropertyName("coverPhotoMediaItemId")]
        public string? CoverPhotoMediaItemId { get; set; }

        [JsonPropertyName("mediaItemsCount")]
        public string? MediaItemsCountStr { get; set; }

        public int MediaItemsCount => int.TryParse(MediaItemsCountStr, out var count) ? count : 0;
    }

    public class GooglePhotosMediaList
    {
        [JsonPropertyName("mediaItems")]
        public List<GooglePhotosMediaItem> MediaItems { get; set; } = new();

        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; set; }
    }

    public class GooglePhotosMediaItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("productUrl")]
        public string? ProductUrl { get; set; }

        [JsonPropertyName("baseUrl")]
        public string BaseUrl { get; set; } = string.Empty;

        [JsonPropertyName("mimeType")]
        public string MimeType { get; set; } = string.Empty;

        [JsonPropertyName("filename")]
        public string Filename { get; set; } = string.Empty;

        [JsonPropertyName("mediaMetadata")]
        public GooglePhotosMediaMetadata? MediaMetadata { get; set; }

        [JsonPropertyName("contributorInfo")]
        public GooglePhotosContributorInfo? ContributorInfo { get; set; }

        public bool EsVideo => MimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
        public bool EsImagen => MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    public class GooglePhotosMediaMetadata
    {
        [JsonPropertyName("creationTime")]
        public string? CreationTime { get; set; }

        [JsonPropertyName("width")]
        public string? Width { get; set; }

        [JsonPropertyName("height")]
        public string? Height { get; set; }

        [JsonPropertyName("photo")]
        public GooglePhotosPhotoMetadata? Photo { get; set; }

        [JsonPropertyName("video")]
        public GooglePhotosVideoMetadata? Video { get; set; }

        public int WidthInt => int.TryParse(Width, out var w) ? w : 0;
        public int HeightInt => int.TryParse(Height, out var h) ? h : 0;
    }

    public class GooglePhotosPhotoMetadata
    {
        [JsonPropertyName("cameraMake")]
        public string? CameraMake { get; set; }

        [JsonPropertyName("cameraModel")]
        public string? CameraModel { get; set; }

        [JsonPropertyName("focalLength")]
        public double? FocalLength { get; set; }

        [JsonPropertyName("apertureFNumber")]
        public double? ApertureFNumber { get; set; }

        [JsonPropertyName("isoEquivalent")]
        public int? IsoEquivalent { get; set; }

        [JsonPropertyName("exposureTime")]
        public string? ExposureTime { get; set; }
    }

    public class GooglePhotosVideoMetadata
    {
        [JsonPropertyName("cameraMake")]
        public string? CameraMake { get; set; }

        [JsonPropertyName("cameraModel")]
        public string? CameraModel { get; set; }

        [JsonPropertyName("fps")]
        public double? Fps { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        public bool EstaListo => Status?.Equals("READY", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public class GooglePhotosContributorInfo
    {
        [JsonPropertyName("profilePictureBaseUrl")]
        public string? ProfilePictureBaseUrl { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }
    }

    // Request DTOs para búsqueda
    public class GooglePhotosSearchRequest
    {
        [JsonPropertyName("albumId")]
        public string? AlbumId { get; set; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; } = 50;

        [JsonPropertyName("pageToken")]
        public string? PageToken { get; set; }

        [JsonPropertyName("filters")]
        public GooglePhotosFilters? Filters { get; set; }
    }

    public class GooglePhotosFilters
    {
        [JsonPropertyName("mediaTypeFilter")]
        public GooglePhotosMediaTypeFilter? MediaTypeFilter { get; set; }

        [JsonPropertyName("dateFilter")]
        public GooglePhotosDateFilter? DateFilter { get; set; }
    }

    public class GooglePhotosMediaTypeFilter
    {
        [JsonPropertyName("mediaTypes")]
        public List<string> MediaTypes { get; set; } = new();
    }

    public class GooglePhotosDateFilter
    {
        [JsonPropertyName("ranges")]
        public List<GooglePhotosDateRange>? Ranges { get; set; }
    }

    public class GooglePhotosDateRange
    {
        [JsonPropertyName("startDate")]
        public GooglePhotosDate? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public GooglePhotosDate? EndDate { get; set; }
    }

    public class GooglePhotosDate
    {
        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("month")]
        public int Month { get; set; }

        [JsonPropertyName("day")]
        public int Day { get; set; }
    }

    #endregion

    #region Service Implementation

    public class GooglePhotosService : IGooglePhotosService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GooglePhotosService> _logger;
        private readonly GooglePhotosSettings _settings;

        private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
        private const string AlbumsEndpoint = "https://photoslibrary.googleapis.com/v1/albums";
        private const string MediaItemsEndpoint = "https://photoslibrary.googleapis.com/v1/mediaItems";
        private const string SearchEndpoint = "https://photoslibrary.googleapis.com/v1/mediaItems:search";
        private const string Scope = "https://www.googleapis.com/auth/photoslibrary.readonly";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public GooglePhotosService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<GooglePhotosService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            _settings = new GooglePhotosSettings();
            configuration.GetSection("GooglePhotos").Bind(_settings);

            if (string.IsNullOrEmpty(_settings.ClientId))
            {
                _logger.LogWarning("Google Photos ClientId no configurado");
            }
        }

        /// <summary>
        /// Genera la URL de autorizacion de OAuth2 para Google Photos
        /// </summary>
        public string GenerarUrlAutorizacion(string state, string? redirectUri = null)
        {
            var finalRedirectUri = redirectUri ?? _settings.RedirectUri;

            var queryParams = new Dictionary<string, string>
            {
                ["client_id"] = _settings.ClientId,
                ["redirect_uri"] = finalRedirectUri,
                ["response_type"] = "code",
                ["scope"] = Scope,
                ["access_type"] = "offline",
                ["prompt"] = "consent",
                ["state"] = state
            };

            var queryString = string.Join("&", queryParams.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

            return $"{AuthorizationEndpoint}?{queryString}";
        }

        /// <summary>
        /// Intercambia el codigo de autorizacion por tokens de acceso
        /// </summary>
        public async Task<GooglePhotosTokenResponse?> IntercambiarCodigoAsync(string code, string? redirectUri = null)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var finalRedirectUri = redirectUri ?? _settings.RedirectUri;

                var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = _settings.ClientId,
                    ["client_secret"] = _settings.ClientSecret,
                    ["redirect_uri"] = finalRedirectUri,
                    ["grant_type"] = "authorization_code"
                });

                var response = await client.PostAsync(TokenEndpoint, requestContent);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error al intercambiar codigo Google Photos: {StatusCode} - {Response}",
                        response.StatusCode, responseContent);
                    return null;
                }

                var tokenResponse = JsonSerializer.Deserialize<GooglePhotosTokenResponse>(responseContent, JsonOptions);

                _logger.LogInformation("Token de Google Photos obtenido exitosamente");
                return tokenResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al intercambiar codigo de autorizacion de Google Photos");
                return null;
            }
        }

        /// <summary>
        /// Refresca el token de acceso usando el refresh token
        /// </summary>
        public async Task<GooglePhotosTokenResponse?> RefrescarTokenAsync(string refreshToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();

                var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = _settings.ClientId,
                    ["client_secret"] = _settings.ClientSecret,
                    ["grant_type"] = "refresh_token"
                });

                var response = await client.PostAsync(TokenEndpoint, requestContent);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error al refrescar token Google Photos: {StatusCode} - {Response}",
                        response.StatusCode, responseContent);
                    return null;
                }

                var tokenResponse = JsonSerializer.Deserialize<GooglePhotosTokenResponse>(responseContent, JsonOptions);

                _logger.LogInformation("Token de Google Photos refrescado exitosamente");
                return tokenResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al refrescar token de Google Photos");
                return null;
            }
        }

        /// <summary>
        /// Obtiene la lista de albumes del usuario
        /// </summary>
        public async Task<GooglePhotosAlbumList?> ObtenerAlbumesAsync(string accessToken, string? pageToken = null)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var url = AlbumsEndpoint;
                var queryParams = new List<string> { "pageSize=50" };

                if (!string.IsNullOrEmpty(pageToken))
                {
                    queryParams.Add($"pageToken={Uri.EscapeDataString(pageToken)}");
                }

                if (queryParams.Count > 0)
                {
                    url += "?" + string.Join("&", queryParams);
                }

                var response = await client.GetAsync(url);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error al obtener albumes Google Photos: {StatusCode} - {Response}",
                        response.StatusCode, responseContent);
                    return null;
                }

                var albumList = JsonSerializer.Deserialize<GooglePhotosAlbumList>(responseContent, JsonOptions);

                _logger.LogDebug("Obtenidos {Count} albumes de Google Photos", albumList?.Albums?.Count ?? 0);
                return albumList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener albumes de Google Photos");
                return null;
            }
        }

        /// <summary>
        /// Obtiene los media items (fotos y videos)
        /// Si se proporciona albumId, busca en ese album especifico
        /// </summary>
        public async Task<GooglePhotosMediaList?> ObtenerMediaItemsAsync(string accessToken, string? albumId = null, string? pageToken = null)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                string responseContent;

                if (!string.IsNullOrEmpty(albumId))
                {
                    // Usar endpoint de busqueda para filtrar por album
                    var searchRequest = new GooglePhotosSearchRequest
                    {
                        AlbumId = albumId,
                        PageSize = 50,
                        PageToken = pageToken
                    };

                    var jsonContent = new StringContent(
                        JsonSerializer.Serialize(searchRequest, JsonOptions),
                        Encoding.UTF8,
                        "application/json");

                    var response = await client.PostAsync(SearchEndpoint, jsonContent);
                    responseContent = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("Error al buscar media items en album Google Photos: {StatusCode} - {Response}",
                            response.StatusCode, responseContent);
                        return null;
                    }
                }
                else
                {
                    // Obtener todos los media items
                    var url = MediaItemsEndpoint;
                    var queryParams = new List<string> { "pageSize=50" };

                    if (!string.IsNullOrEmpty(pageToken))
                    {
                        queryParams.Add($"pageToken={Uri.EscapeDataString(pageToken)}");
                    }

                    url += "?" + string.Join("&", queryParams);

                    var response = await client.GetAsync(url);
                    responseContent = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("Error al obtener media items Google Photos: {StatusCode} - {Response}",
                            response.StatusCode, responseContent);
                        return null;
                    }
                }

                var mediaList = JsonSerializer.Deserialize<GooglePhotosMediaList>(responseContent, JsonOptions);

                _logger.LogDebug("Obtenidos {Count} media items de Google Photos", mediaList?.MediaItems?.Count ?? 0);
                return mediaList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener media items de Google Photos");
                return null;
            }
        }

        /// <summary>
        /// Descarga el contenido de un media item
        /// Para fotos: baseUrl + =d (download)
        /// Para videos: baseUrl + =dv (download video)
        /// </summary>
        public async Task<byte[]?> DescargarMediaAsync(string accessToken, string baseUrl, bool esVideo)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                // Configurar timeout mas largo para videos grandes
                client.Timeout = TimeSpan.FromMinutes(10);

                // Construir URL de descarga
                // =d para fotos (full resolution download)
                // =dv para videos (download video)
                var downloadUrl = esVideo ? $"{baseUrl}=dv" : $"{baseUrl}=d";

                _logger.LogDebug("Descargando media de Google Photos: {Url}",
                    downloadUrl.Substring(0, Math.Min(100, downloadUrl.Length)));

                var response = await client.GetAsync(downloadUrl);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Error al descargar media de Google Photos: {StatusCode} - {Response}",
                        response.StatusCode, errorContent);
                    return null;
                }

                var data = await response.Content.ReadAsByteArrayAsync();

                _logger.LogInformation("Media descargado de Google Photos: {Size} bytes", data.Length);
                return data;
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("Timeout al descargar media de Google Photos");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al descargar media de Google Photos");
                return null;
            }
        }
    }

    #endregion
}
