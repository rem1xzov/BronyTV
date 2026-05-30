using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BronyTV.DbContext;
using BronyTV.DbContext.Entity;
using BronyTV.Repository;
using BronyTV.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var videosStorageRoot = builder.Configuration["VideoStorage:RootPath"]
    ?? Environment.GetEnvironmentVariable("BRONYTV_VIDEOS_ROOT")
    ?? "/app/media";
const string OpenCorsPolicy = "OpenCorsPolicy";

builder.Services.AddDbContext<DbBronyTV>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        x => x.MigrationsHistoryTable("__EFMigrationsHistory", "public")));

builder.Services.AddScoped<IVideoRepository, VideoRepository>();
builder.Services.AddScoped<ISeasonRepository, SeasonRepository>();
builder.Services.AddScoped<IAdminRepository, AdminRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ISeasonService, SeasonService>();
builder.Services.AddScoped<IVideoService, VideoService>();
builder.Services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
builder.Services.AddScoped<IUserAuthService, UserAuthService>();
builder.Services.AddControllers();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:8080" };

builder.Services.AddCors(options =>
{
    options.AddPolicy(OpenCorsPolicy, policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders("Content-Range", "Accept-Ranges");
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(
    options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue("bronytv_session", out var cookieToken)
                    && !string.IsNullOrWhiteSpace(cookieToken))
                {
                    context.Token = cookieToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 500 * 1024 * 1024;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 500 * 1024 * 1024;
});

var app = builder.Build();
var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var startupLogger = loggerFactory.CreateLogger("BronyTV.Startup");

await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider.GetRequiredService<DbBronyTV>();
    await context.Database.MigrateAsync();

    Directory.CreateDirectory(Path.Combine(app.Environment.WebRootPath, "content", "video"));
    var previewsDir = Path.Combine(app.Environment.WebRootPath, "content", "previews");
    Directory.CreateDirectory(previewsDir);

    const string defaultSeasonPoster = "default-season.jpg";
    var defaultPosterPath = Path.Combine(previewsDir, defaultSeasonPoster);
    if (!File.Exists(defaultPosterPath))
    {
        // Minimal valid JPEG placeholder when season posters are not deployed yet.
        var placeholderJpeg = Convert.FromBase64String(
            "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAARCAABAAEDASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAn/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/8QAFQEBAQAAAAAAAAAAAAAAAAAAAAX/xAAUEQEAAAAAAAAAAAAAAAAAAAAA/9oADAMBAAIRAxEAPwCwAA8A/9k=");
        await File.WriteAllBytesAsync(defaultPosterPath, placeholderJpeg);
        startupLogger.LogInformation("Создан placeholder превью сезона: {Path}", defaultPosterPath);
    }

    if (!await context.Admins.AnyAsync())
    {
        context.Admins.Add(new AdminEntity
        {
            Id = Guid.NewGuid(),
            Login = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"),
        });
        await context.SaveChangesAsync();
    }

    string BuildPosterPath(int seasonNumber)
    {
        var seasonFileName = $"s{seasonNumber}e1.jpg";
        var seasonFilePath = Path.Combine(previewsDir, seasonFileName);
        if (File.Exists(seasonFilePath))
        {
            return $"/content/previews/{seasonFileName}";
        }

        return $"/content/previews/{defaultSeasonPoster}";
    }

    if (!await context.Seasons.AnyAsync())
    {
        var seasons = new List<SeasonEntity>();
        for (var i = 1; i <= 9; i++)
        {
            seasons.Add(new SeasonEntity
            {
                Id = Guid.NewGuid(),
                Number = i,
                Title = $"Сезон {i}",
                Description = "Дружба - это чудо!",
                PosterPath = BuildPosterPath(i)
            });
        }
        context.Seasons.AddRange(seasons);
        startupLogger.LogInformation("Добавлены 9 сезонов в базу.");
    }
    else
    {
        var seasons = await context.Seasons.ToListAsync();
        foreach (var season in seasons)
        {
            if (string.IsNullOrWhiteSpace(season.PosterPath)
                || season.PosterPath == "placeholder"
                || season.PosterPath.Contains("placeholder", StringComparison.OrdinalIgnoreCase)
                || season.PosterPath.Contains("default_season", StringComparison.OrdinalIgnoreCase)
                || season.PosterPath.StartsWith("/api/content/", StringComparison.OrdinalIgnoreCase))
            {
                season.PosterPath = BuildPosterPath(season.Number);
            }
        }
    }

    if (context.ChangeTracker.HasChanges())
    {
        await context.SaveChangesAsync();
    }
}

app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(() =>
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbBronyTV>();
            SyncVideosFromDisk(context, videosStorageRoot, startupLogger);
        }
        catch (Exception ex)
        {
            startupLogger.LogError(ex, "Фоновая синхронизация видео с диска завершилась с ошибкой.");
        }
    });
});

// CORS оборачивает статику: ответы /videos и wwwroot получают заголовки для кросс-доменного плеера.
app.UseCors(OpenCorsPolicy);

// /videos/* отдаёт VideoStreamController (PhysicalFile + enableRangeProcessing) для Safari/iOS.

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

var indexHtmlPath = Path.Combine(app.Environment.WebRootPath, "index.html");
if (File.Exists(indexHtmlPath))
{
    app.MapFallbackToFile("index.html");
}

app.Run();

static void SyncVideosFromDisk(DbBronyTV context, string videosRoot, ILogger logger)
{
    if (string.IsNullOrWhiteSpace(videosRoot) || !Directory.Exists(videosRoot))
    {
        logger.LogWarning("Корневая директория видео не найдена или пуста: {Root}", videosRoot);
        return;
    }

    var numberRuns = new Regex(@"\d+", RegexOptions.CultureInvariant);
    var seasons = context.Seasons.ToList(); // Убираем AsNoTracking, так как будем обновлять связи

    // ОПТИМИЗАЦИЯ: Загружаем ВСЕ существующие видео из базы в память ОДИН раз
    var allExistingVideos = context.Videos.ToList();
    logger.LogInformation("Загружено {Count} существующих видео из базы для синхронизации.", allExistingVideos.Count);

    var hasChanges = false;

    foreach (var season in seasons)
    {
        var seasonDir = Path.Combine(videosRoot, $"сезон {season.Number}");
        if (!Directory.Exists(seasonDir))
        {
            continue;
        }

        foreach (var fullPath in Directory.EnumerateFiles(seasonDir, "*.mp4", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(fullPath);
            var numbers = numberRuns.Matches(name)
                .Cast<Match>()
                .Select(m => int.Parse(m.Value, CultureInfo.InvariantCulture))
                .ToList();

            if (numbers.Count == 0)
            {
                continue;
            }

            int episodeNumber = numbers.Count >= 2 ? numbers[1] : numbers[0];

            if (episodeNumber is < 1 or > 999)
            {
                continue;
            }

            // Ищем видео в памяти локально, вместо постоянных запросов к БД
            var existing = allExistingVideos.FirstOrDefault(v => v.SeasonId == season.Id && v.EpisodeNumber == episodeNumber);
            
            if (existing != null)
            {
                if (!string.Equals(existing.FilePath, name, StringComparison.Ordinal))
                {
                    existing.FilePath = name;
                    hasChanges = true;
                }
            }
            else
            {
                var newVideo = new VideoEntity
                {
                    Id = Guid.NewGuid(),
                    SeasonId = season.Id,
                    EpisodeNumber = episodeNumber,
                    Title = $"Серия {episodeNumber}",
                    Description = string.Empty,
                    FilePath = name,
                    PreviewImageUrl = null
                };
                
                context.Videos.Add(newVideo);
                allExistingVideos.Add(newVideo); // Добавляем в локальный список, чтобы не дублировать
                hasChanges = true;
            }
        }
    }

    if (hasChanges)
    {
        logger.LogInformation("Сохранение изменений синхронизации в базу данных...");
        context.SaveChanges();
        logger.LogInformation("Синхронизация успешно завершена!");
    }
    else
    {
        logger.LogInformation("Изменений на диске не обнаружено. Синхронизация не требуется.");
    }
}
