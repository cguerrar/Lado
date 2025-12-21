using Lado.Data;
using Lado.Models;
using Lado.Middleware;
using Lado.Services;
using Lado.Hubs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.Security.Principal;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ========================================
// CONFIGURACION DE LOGGING A ARCHIVO
// ========================================
var logsPath = Path.Combine(builder.Environment.ContentRootPath, "logs");
if (!Directory.Exists(logsPath))
{
    Directory.CreateDirectory(logsPath);
}

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Provider de archivo simple
builder.Services.AddSingleton<ILoggerProvider>(sp =>
    new FileLoggerProvider(Path.Combine(logsPath, $"lado-{DateTime.Now:yyyy-MM-dd}.log")));

// ========================================
// CONFIGURACION PARA IIS/PLESK (Proxy Reverso)
// ========================================
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Configurar SQL Server - usa ConnectionStrings:DefaultConnection desde appsettings
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ========================================
// CONFIGURACION DATA PROTECTION (para IIS/Plesk)
// ========================================
var keysFolder = Path.Combine(builder.Environment.ContentRootPath, "keys");
if (!Directory.Exists(keysFolder))
{
    Directory.CreateDirectory(keysFolder);
}
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysFolder))
    .SetApplicationName("Lado");

// Configurar Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configurar Google Authentication - solo si hay credenciales configuradas
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

// ========================================
// CONFIGURACIÓN JWT BEARER (API Móvil)
// ========================================
var jwtKey = builder.Configuration["Jwt:Key"] ?? "LadoApp_DefaultKey_ChangeInProduction123!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "LadoApp";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "LadoAppMobile";

var authBuilder = builder.Services.AddAuthentication(options =>
{
    // Identity usa cookies por defecto para web
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
});

// Agregar JWT Bearer para API móvil
authBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero // Sin tolerancia de tiempo
    };

    // Soporte JWT en SignalR via query string
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chatHub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// Agregar Google si está configurado
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
    });
}

// ========================================
// CONFIGURACION SEGURA DE COOKIES (Compatible con IIS/Plesk)
// ========================================
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS en produccion
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Name = ".Lado.Auth";
    // Dominio para que funcione con www y sin www
    if (!builder.Environment.IsDevelopment())
    {
        options.Cookie.Domain = ".ladoapp.com";
    }
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// ========================================
// CONFIGURACION ANTIFORGERY PARA AJAX/JSON
// ========================================
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN"; // Token en header para peticiones AJAX
    options.Cookie.Name = ".Lado.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Registrar servicios personalizados
builder.Services.AddScoped<Lado.Services.MercadoPagoService>();

// Configurar tamaño máximo de archivos (100 MB)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // 100 MB
});

// ⭐ IMPORTANTE: Configurar límite de tamaño para Kestrel también
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 104857600; // 100 MB
});

// Configurar sesiones
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    if (!builder.Environment.IsDevelopment())
    {
        options.Cookie.Domain = ".ladoapp.com";
    }
});

builder.Services.AddScoped<Lado.Services.StripeSimuladoService>();
builder.Services.AddScoped<Lado.Services.IAdService, Lado.Services.AdService>();
builder.Services.AddSingleton<Lado.Services.IServerMetricsService, Lado.Services.ServerMetricsService>();
builder.Services.AddScoped<Lado.Services.IEmailService, Lado.Services.EmailService>();
builder.Services.AddScoped<Lado.Services.IVisitasService, Lado.Services.VisitasService>();
builder.Services.AddScoped<Lado.Services.INotificationService, Lado.Services.NotificationService>();
builder.Services.AddScoped<Lado.Services.IFeedAlgorithmService, Lado.Services.FeedAlgorithmService>();
builder.Services.AddScoped<Lado.Services.IImageService, Lado.Services.ImageService>();
builder.Services.AddScoped<Lado.Services.IDateTimeService, Lado.Services.DateTimeService>();
builder.Services.AddScoped<Lado.Services.IInteresesService, Lado.Services.InteresesService>();
builder.Services.AddScoped<Lado.Services.ILogEventoService, Lado.Services.LogEventoService>();
builder.Services.AddScoped<Lado.Services.ITrustService, Lado.Services.TrustService>();
builder.Services.AddHostedService<Lado.Services.LogCleanupService>();
builder.Services.AddHostedService<Lado.Services.TokenCleanupService>();
builder.Services.AddHostedService<Lado.Services.SuscripcionExpirationService>();

// ========================================
// CONFIGURACIÓN JWT SERVICE (API Móvil)
// ========================================
builder.Services.AddScoped<Lado.Services.IJwtService, Lado.Services.JwtService>();

// ========================================
// CONFIGURACIÓN DE CLAUDE API (Clasificación de contenido)
// ========================================
builder.Services.AddHttpClient("Claude", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<Lado.Services.IClaudeClassificationService, Lado.Services.ClaudeClassificationService>();

// ========================================
// CONFIGURACIÓN DE CACHING
// ========================================
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<Lado.Services.ICacheService, Lado.Services.CacheService>();
builder.Services.AddSingleton<Lado.Services.IRateLimitService, Lado.Services.RateLimitService>();

// ========================================
// SERVICIO DE EXTRACCIÓN EXIF (Ubicación)
// ========================================
builder.Services.AddHttpClient();
builder.Services.AddScoped<Lado.Services.IExifService, Lado.Services.ExifService>();

// ========================================
// CONFIGURACIÓN DE SIGNALR (Chat en tiempo real)
// ========================================
builder.Services.AddSignalR();

// ========================================
// CONFIGURACIÓN DE LOCALIZACIÓN (i18n)
// ========================================
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddSingleton<ILocalizationService, LocalizationService>();

// Configurar idiomas soportados
var supportedCultures = new[]
{
    new CultureInfo("es"), // Español (Latino)
    new CultureInfo("en"), // English
    new CultureInfo("pt")  // Português
};

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("es");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders.Clear();
    options.RequestCultureProviders.Add(new UserLanguageRequestCultureProvider());
});

var app = builder.Build();

// Configurar pipeline HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// ========================================
// LOGGING DE EXCEPCIONES A BASE DE DATOS
// ========================================
app.UseExceptionLogging();

// ========================================
// CRITICO: ForwardedHeaders DEBE ir PRIMERO para IIS/Plesk
// ========================================
app.UseForwardedHeaders();

// Solo redirigir a HTTPS en desarrollo (IIS/Plesk maneja SSL externamente)
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// ========================================
// HEADERS DE SEGURIDAD
// ========================================
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});

// ⭐ CRÍTICO: UseStaticFiles DEBE estar ANTES de UseRouting
app.UseStaticFiles(); // Sirve archivos desde wwwroot

app.UseRouting();

app.UseIpBlocking(); // Bloquear IPs en lista negra

app.UseAuthentication();
app.UseJwtValidation(); // Validación de tokens JWT contra BD (revocación inmediata)
app.UseRequestLocalization(); // Localización (i18n)
app.UseAgeVerification();
app.UseAuthorization();
app.UseSession();

// Contador de visitas
app.UseVisitasMiddleware();

// ⭐⭐⭐ CORRECCIÓN CRÍTICA: Línea estaba incompleta
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// ========================================
// SIGNALR HUB ENDPOINT
// ========================================
app.MapHub<ChatHub>("/chatHub");

// ========================================
// ✅ INICIALIZACIÓN MEJORADA DEL SISTEMA
// ========================================
try
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("========================================");
        logger.LogInformation("🚀 INICIANDO CONFIGURACIÓN DE Lado");
        logger.LogInformation("========================================");

        // ✅ INFORMACIÓN DE DIAGNÓSTICO
        logger.LogInformation("📁 ContentRootPath: {Path}", app.Environment.ContentRootPath);
        logger.LogInformation("📁 WebRootPath: {Path}", app.Environment.WebRootPath ?? "NO DEFINIDO");
        // WindowsIdentity puede fallar en algunos entornos de hosting
        try
        {
            logger.LogInformation("👤 Usuario ejecutando app: {User}", WindowsIdentity.GetCurrent()?.Name ?? "DESCONOCIDO");
        }
        catch
        {
            logger.LogInformation("👤 Usuario ejecutando app: IIS AppPool");
        }
        logger.LogInformation("🖥️  Entorno: {Environment}", app.Environment.EnvironmentName);

        // Crear roles si no existen
        string[] roles = { "Admin", "Creador", "Fan" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("✅ Rol '{Role}' creado", role);
            }
        }

        // ✅ VERIFICAR Y ARREGLAR TABLA NOTIFICACIONES
        try
        {
            var dbContext = services.GetRequiredService<ApplicationDbContext>();

            // Verificar si la tabla existe y tiene todas las columnas
            await dbContext.Database.ExecuteSqlRawAsync(@"
                -- Agregar columnas faltantes si no existen
                IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Notificaciones')
                BEGIN
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Notificaciones' AND COLUMN_NAME = 'EstaActiva')
                        ALTER TABLE [Notificaciones] ADD [EstaActiva] bit NOT NULL DEFAULT 1

                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Notificaciones' AND COLUMN_NAME = 'ComentarioId')
                        ALTER TABLE [Notificaciones] ADD [ComentarioId] int NULL

                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Notificaciones' AND COLUMN_NAME = 'ContenidoId')
                        ALTER TABLE [Notificaciones] ADD [ContenidoId] int NULL

                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Notificaciones' AND COLUMN_NAME = 'DesafioId')
                        ALTER TABLE [Notificaciones] ADD [DesafioId] int NULL

                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Notificaciones' AND COLUMN_NAME = 'FechaLectura')
                        ALTER TABLE [Notificaciones] ADD [FechaLectura] datetime2 NULL

                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Notificaciones' AND COLUMN_NAME = 'ImagenUrl')
                        ALTER TABLE [Notificaciones] ADD [ImagenUrl] nvarchar(500) NULL

                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Notificaciones' AND COLUMN_NAME = 'MensajeId')
                        ALTER TABLE [Notificaciones] ADD [MensajeId] int NULL

                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Notificaciones' AND COLUMN_NAME = 'UrlDestino')
                        ALTER TABLE [Notificaciones] ADD [UrlDestino] nvarchar(500) NULL

                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Notificaciones' AND COLUMN_NAME = 'UsuarioOrigenId')
                        ALTER TABLE [Notificaciones] ADD [UsuarioOrigenId] nvarchar(450) NULL
                END

                -- Registrar migración si no existe
                IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20251216175610_AgregarNotificaciones')
                BEGIN
                    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                    VALUES ('20251216175610_AgregarNotificaciones', '8.0.0')
                END
            ");
            logger.LogInformation("✅ Tabla Notificaciones verificada y actualizada");
        }
        catch (Exception ex)
        {
            logger.LogWarning("⚠️ No se pudo verificar tabla Notificaciones: {Message}", ex.Message);
        }

        // ✅ VERIFICAR Y CREAR TABLAS JWT (RefreshTokens, ActiveTokens)
        try
        {
            var dbContext = services.GetRequiredService<ApplicationDbContext>();

            await dbContext.Database.ExecuteSqlRawAsync(@"
                -- 1. Agregar SecurityVersion a AspNetUsers si no existe
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'SecurityVersion')
                BEGIN
                    ALTER TABLE AspNetUsers ADD SecurityVersion INT NOT NULL DEFAULT 1;
                    PRINT 'SecurityVersion agregado a AspNetUsers';
                END

                -- 2. Crear tabla RefreshTokens si no existe
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RefreshTokens')
                BEGIN
                    CREATE TABLE RefreshTokens (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Token NVARCHAR(500) NOT NULL,
                        UserId NVARCHAR(450) NOT NULL,
                        ExpiryDate DATETIME2 NOT NULL,
                        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        IsRevoked BIT NOT NULL DEFAULT 0,
                        DeviceInfo NVARCHAR(500) NULL,
                        IpAddress NVARCHAR(50) NULL,
                        CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId)
                            REFERENCES AspNetUsers(Id) ON DELETE CASCADE
                    );

                    CREATE UNIQUE INDEX IX_RefreshTokens_Token ON RefreshTokens(Token);
                    CREATE INDEX IX_RefreshTokens_UserId ON RefreshTokens(UserId);
                    CREATE INDEX IX_RefreshTokens_User_Active ON RefreshTokens(UserId, IsRevoked, ExpiryDate);

                    PRINT 'Tabla RefreshTokens creada';
                END

                -- 3. Crear tabla ActiveTokens si no existe
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ActiveTokens')
                BEGIN
                    CREATE TABLE ActiveTokens (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Jti NVARCHAR(100) NOT NULL,
                        UserId NVARCHAR(450) NOT NULL,
                        ExpiresAt DATETIME2 NOT NULL,
                        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        IsRevoked BIT NOT NULL DEFAULT 0,
                        DeviceInfo NVARCHAR(500) NULL,
                        IpAddress NVARCHAR(50) NULL,
                        CONSTRAINT FK_ActiveTokens_Users FOREIGN KEY (UserId)
                            REFERENCES AspNetUsers(Id) ON DELETE CASCADE
                    );

                    CREATE UNIQUE INDEX IX_ActiveTokens_Jti ON ActiveTokens(Jti);
                    CREATE INDEX IX_ActiveTokens_UserId ON ActiveTokens(UserId);
                    CREATE INDEX IX_ActiveTokens_Cleanup ON ActiveTokens(ExpiresAt, IsRevoked);

                    PRINT 'Tabla ActiveTokens creada';
                END
            ");
            logger.LogInformation("✅ Tablas JWT (RefreshTokens, ActiveTokens, SecurityVersion) verificadas");
        }
        catch (Exception ex)
        {
            logger.LogWarning("⚠️ No se pudo verificar tablas JWT: {Message}", ex.Message);
        }

        // Crear usuario admin si no existe
        var adminEmail = "admin@ladoapp.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            var admin = new ApplicationUser
            {
                UserName = "admin",
                Email = adminEmail,
                NombreCompleto = "Administrador",
                EmailConfirmed = true,
                EstaActivo = true,
                FechaRegistro = DateTime.Now,
                AgeVerified = true,
                AgeVerifiedDate = DateTime.Now
            };

            var result = await userManager.CreateAsync(admin, "Admin123!");

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
                logger.LogInformation("✅ Usuario Admin creado exitosamente");
                logger.LogInformation("   📧 Email: {Email}", adminEmail);
                // NOTA: Password no se loguea por seguridad
            }
            else
            {
                logger.LogError("❌ Error al crear usuario Admin:");
                foreach (var error in result.Errors)
                {
                    logger.LogError("   - {Error}", error.Description);
                }
            }
        }
        else
        {
            logger.LogInformation("✅ Usuario Admin ya existe");
        }

        // ⭐ VERIFICAR Y CREAR CARPETAS NECESARIAS
        var wwwrootPath = app.Environment.WebRootPath;
        if (string.IsNullOrEmpty(wwwrootPath))
        {
            logger.LogError("❌ ERROR: WebRootPath es NULL");
        }
        else
        {
            logger.LogInformation("📁 WebRootPath: {Path}", wwwrootPath);
        }

        string uploadsBasePath = Path.Combine(wwwrootPath ?? app.Environment.ContentRootPath, "uploads");
        string[] carpetas = {
            "fotos",
            "videos",
            "audios",
            "documentos",
            "perfiles",
            "portadas"
        };

        foreach (var carpeta in carpetas)
        {
            string rutaCarpeta = Path.Combine(uploadsBasePath, carpeta);
            try
            {
                if (!Directory.Exists(rutaCarpeta))
                {
                    Directory.CreateDirectory(rutaCarpeta);
                    logger.LogInformation("✅ Carpeta creada: {Carpeta}", rutaCarpeta);
                }
                else
                {
                    logger.LogInformation("✅ Carpeta existe: {Carpeta}", rutaCarpeta);
                }

                // Verificar permisos de escritura
                try
                {
                    string testFile = Path.Combine(rutaCarpeta, "test.txt");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                    logger.LogInformation("✅ Permisos de escritura OK en '{Carpeta}'", carpeta);
                }
                catch (UnauthorizedAccessException)
                {
                    logger.LogError("❌ ERROR: NO HAY PERMISOS DE ESCRITURA en '{Carpeta}'", carpeta);
                    logger.LogError("SOLUCIÓN: Ejecuta este comando en PowerShell como Administrador:");
                    logger.LogError("icacls \"{Path}\" /grant IIS_IUSRS:(OI)(CI)F", rutaCarpeta);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Error al crear/verificar carpeta '{Carpeta}'", carpeta);
                logger.LogError("Ruta intentada: {Path}", rutaCarpeta);
            }
        }

        // ⭐ IMPORTANTE: Verificar que las rutas en BD sean correctas
        var context = services.GetRequiredService<ApplicationDbContext>();
        var contenidosConRutasIncorrectas = await context.Contenidos
            .Where(c => c.RutaArchivo != null && !c.RutaArchivo.StartsWith("/"))
            .ToListAsync();

        if (contenidosConRutasIncorrectas.Any())
        {
            logger.LogWarning("⚠️  Encontrados {Count} contenidos con rutas incorrectas", contenidosConRutasIncorrectas.Count);
            logger.LogWarning("Las rutas deben empezar con '/' para ser servidas correctamente");

            foreach (var contenido in contenidosConRutasIncorrectas.Take(5))
            {
                logger.LogWarning("   ID {Id}: {Ruta}", contenido.Id, contenido.RutaArchivo);
            }
        }

        // ⭐ ACTUALIZAR COMISIONES Y MONTOS MÍNIMOS DE USUARIOS EXISTENTES
        var usuariosSinComision = await context.Users
            .Where(u => u.ComisionRetiro == 0)
            .ToListAsync();

        if (usuariosSinComision.Any())
        {
            foreach (var usuario in usuariosSinComision)
            {
                usuario.ComisionRetiro = 20; // 20% por defecto
                if (usuario.MontoMinimoRetiro == 0)
                {
                    usuario.MontoMinimoRetiro = 50; // $50 mínimo por defecto
                }
            }
            await context.SaveChangesAsync();
            logger.LogInformation("✅ Actualizados {Count} usuarios con comisión por defecto (20%)", usuariosSinComision.Count);
        }

        // ⭐ NUEVO: Listar archivos existentes para debug
        if (Directory.Exists(uploadsBasePath))
        {
            var archivosEnUploads = Directory.GetFiles(uploadsBasePath, "*", SearchOption.AllDirectories);
            logger.LogInformation("📊 Total de archivos en uploads: {Count}", archivosEnUploads.Length);

            if (archivosEnUploads.Length > 0)
            {
                logger.LogInformation("📄 Primeros 5 archivos:");
                foreach (var archivo in archivosEnUploads.Take(5))
                {
                    var relativePath = archivo.Replace(wwwrootPath, "").Replace("\\", "/");
                    var fileInfo = new FileInfo(archivo);
                    logger.LogInformation("   - {Path} ({Size} KB)", relativePath, fileInfo.Length / 1024);
                }
            }
        }

        // ✅ RESUMEN FINAL
        logger.LogInformation("========================================");
        logger.LogInformation("📊 RESUMEN DE INICIALIZACIÓN:");
        logger.LogInformation("   Roles: ✅ Configurados");
        logger.LogInformation("   Admin: ✅ Configurado");
        logger.LogInformation("   Carpetas: ✅ Verificadas");
        logger.LogInformation("   Archivos estáticos: ✅ UseStaticFiles configurado");
        logger.LogInformation("========================================");
    }
}
catch (Exception ex)
{
    // Capturar y mostrar errores detallados
    Console.WriteLine("========================================");
    Console.WriteLine("❌❌❌ ERROR CRÍTICO EN INICIALIZACIÓN ❌❌❌");
    Console.WriteLine("========================================");
    Console.WriteLine($"Mensaje: {ex.Message}");
    Console.WriteLine($"Tipo: {ex.GetType().Name}");
    Console.WriteLine($"StackTrace: {ex.StackTrace}");

    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
        Console.WriteLine($"Inner StackTrace: {ex.InnerException.StackTrace}");
    }

    Console.WriteLine("========================================");
    Console.WriteLine("⚠️  LA APLICACIÓN INTENTARÁ CONTINUAR");
    Console.WriteLine("⚠️  PERO PUEDE HABER FUNCIONALIDADES LIMITADAS");
    Console.WriteLine("========================================");
}

Console.WriteLine("========================================");
Console.WriteLine("🚀 Lado iniciado correctamente");
Console.WriteLine("📍 URL: https://localhost:7162");
Console.WriteLine("🧪 Prueba: https://localhost:7162/uploads/test.txt");
Console.WriteLine("⚠️  Si hay errores de permisos, ejecuta:");
Console.WriteLine("   icacls \"C:\\tu\\ruta\\wwwroot\\uploads\" /grant IIS_IUSRS:(OI)(CI)F");
Console.WriteLine("========================================");

app.Run();
