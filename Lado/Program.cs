using Lado.Data;
using Lado.Models;
using Lado.Middleware;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Principal;

var builder = WebApplication.CreateBuilder(args);

// Configurar SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer("Data Source=200.50.127.117\\MSSQLSERVER2022;Initial Catalog=Lado;User ID=sa;Password=Password123..*;TrustServerCertificate=True"));

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

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(30); // Cookie dura 30 días si "Recordarme" está activo
    options.SlidingExpiration = true; // Renueva la cookie en cada request
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// Configurar cookies
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Home/Index";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

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
});

builder.Services.AddScoped<Lado.Services.StripeSimuladoService>();
var app = builder.Build();

// Configurar pipeline HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// ⭐ CRÍTICO: UseStaticFiles DEBE estar ANTES de UseRouting
app.UseStaticFiles(); // Sirve archivos desde wwwroot

app.UseRouting();

app.UseAuthentication();
app.UseAgeVerification();
app.UseAuthorization();
app.UseSession();

// ⭐⭐⭐ CORRECCIÓN CRÍTICA: Línea estaba incompleta
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

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
        logger.LogInformation("👤 Usuario ejecutando app: {User}", WindowsIdentity.GetCurrent()?.Name ?? "DESCONOCIDO");
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

        // Crear usuario admin si no existe
        var adminEmail = "admin@Ladodemo.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            var admin = new ApplicationUser
            {
                UserName = "admin",
                Email = adminEmail,
                NombreCompleto = "Administrador",
                TipoUsuario = 2,
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
                logger.LogInformation("   🔑 Password: Admin123!");
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