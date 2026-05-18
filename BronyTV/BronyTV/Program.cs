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
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ISeasonService, SeasonService>();
builder.Services.AddScoped<IVideoService, VideoService>();
builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy(OpenCorsPolicy, policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
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
    Directory.CreateDirectory(Path.Combine(app.Environment.WebRootPath, "content", "previews"));

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

    const string defaultSeasonPoster = "default-season.jpg";

    string BuildPosterPath(int seasonNumber)
    {
        var previewsDir = Path.Combine(app.Environment.WebRootPath, "content", "previews");
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
                || season.PosterPath.Contains("placeholder", StringComparison.OrdinalIgnoreCase))
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
        return;
    }

    var numberRuns = new Regex(@"\d+", RegexOptions.CultureInvariant);
    var seasons = context.Seasons.AsNoTracking().ToList();

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
                logger.LogInformation("Пропуск (нет цифр в имени): {Name}", name);
                continue;
            }

            int episodeNumber;
            if (numbers.Count >= 2)
            {
                var seasonFromName = numbers[0];
                episodeNumber = numbers[1];
                if (seasonFromName != season.Number)
                {
                    logger.LogInformation(
                        "В имени первое число={SeasonFromName} (ожид. сезон {ExpectedSeason}), серия из второго числа={EpisodeNumber}: {Name}",
                        seasonFromName, season.Number, episodeNumber, name);
                }
            }
            else
            {
                episodeNumber = numbers[0];
            }

            if (episodeNumber is < 1 or > 999)
            {
                logger.LogInformation("Пропуск (некорректный номер серии {EpisodeNumber}): {Name}", episodeNumber, name);
                continue;
            }

            logger.LogInformation("Найден файл: {Name}, сезон {SeasonNumber}, серия {EpisodeNumber}", name, season.Number, episodeNumber);

            var existing = context.Videos.FirstOrDefault(v => v.SeasonId == season.Id && v.EpisodeNumber == episodeNumber);
            if (existing != null)
            {
                if (!string.Equals(existing.FilePath, name, StringComparison.Ordinal))
                {
                    existing.FilePath = name;
                }
            }
            else
            {
                context.Videos.Add(new VideoEntity
                {
                    Id = Guid.NewGuid(),
                    SeasonId = season.Id,
                    EpisodeNumber = episodeNumber,
                    Title = $"Серия {episodeNumber}",
                    Description = string.Empty,
                    FilePath = name,
                    PreviewImageUrl = null
                });
            }
        }
    }

    context.SaveChanges();
}
