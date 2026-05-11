using BronyTV.DbContext;
using BronyTV.Repository;
using BronyTV.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using BronyTV.DbContext.Entity;
using Microsoft.Extensions.FileProviders;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
var videosStorageRoot = builder.Configuration["VideoStorage:RootPath"]
    ?? Environment.GetEnvironmentVariable("BRONYTV_VIDEOS_ROOT")
    ?? "/root";
const string FrontendCorsPolicy = "FrontendCorsPolicy";

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
var corsOrigins = new[]
{
    "http://localhost:3000",
    "https://bronytv.ru",
    "http://bronytv.ru",
    "https://www.bronytv.ru",
    "http://www.bronytv.ru",
};
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy
            .WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
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
    options.Limits.MaxRequestBodySize = 500 * 1024 * 1024;//500мб
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 500 * 1024 * 1024;
});

var app = builder.Build();


using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<DbBronyTV>();
    context.Database.Migrate();

    Directory.CreateDirectory(Path.Combine(app.Environment.WebRootPath, "content", "video"));
    Directory.CreateDirectory(Path.Combine(app.Environment.WebRootPath, "content", "previews"));

    if (!context.Admins.Any())
    {
        context.Admins.Add(new AdminEntity
        {
            Id = Guid.NewGuid(),
            Login = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"),
        });
        context.SaveChanges();
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

    if (!context.Seasons.Any())
    {
        var seasons = new List<SeasonEntity>();
        for (int i = 1; i <= 9; i++)
        {
            seasons.Add(new SeasonEntity
            {
                Id = Guid.NewGuid(),
                Number = i,
                Title = $"Сезон {i}",
                Description = "Дружба - это чудо!", // Заменим на реальное описание сезона
                PosterPath = BuildPosterPath(i)
            });
        }
        context.Seasons.AddRange(seasons);
        Console.WriteLine("9 сезонов успешно добавлены в базу");
    }
    else
    {
        var seasons = context.Seasons.ToList();
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
        context.SaveChanges();
    }

    // Видео: все .mp4 в /root/сезон N/; номер сезона и серии из групп цифр в имени файла (игнорируем буквы/кодировки).
    SyncVideosFromDisk(context, videosStorageRoot);
}

static void SyncVideosFromDisk(DbBronyTV context, string videosRoot)
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
                Console.WriteLine($"Пропуск (нет цифр в имени): {name}");
                continue;
            }

            int episodeNumber;
            if (numbers.Count >= 2)
            {
                var seasonFromName = numbers[0];
                episodeNumber = numbers[1];
                if (seasonFromName != season.Number)
                {
                    Console.WriteLine(
                        $"Внимание: в имени первое число={seasonFromName} (ожид. сезон {season.Number}), берём серию из второго числа={episodeNumber}: {name}");
                }
            }
            else
            {
                episodeNumber = numbers[0];
            }

            if (episodeNumber < 1 || episodeNumber > 999)
            {
                Console.WriteLine($"Пропуск (некорректный номер серии {episodeNumber}): {name}");
                continue;
            }

            Console.WriteLine($"Нашел файл: {name}, привязал к Сезону {season.Number}, Серии {episodeNumber}");

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

if (!string.Equals(
        Environment.GetEnvironmentVariable("DISABLE_HTTPS_REDIRECT"),
        "true",
        StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}
if (Directory.Exists(videosStorageRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(videosStorageRoot),
        RequestPath = "/videos"
    });
}

app.UseStaticFiles();
app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();