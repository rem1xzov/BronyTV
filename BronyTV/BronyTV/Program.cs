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

    string BuildPosterPath(int seasonNumber)
    {
        var fileName = $"s{seasonNumber}e1.jpg";
        var fileOnDisk = Path.Combine(app.Environment.WebRootPath, "content", "previews", fileName);
        return File.Exists(fileOnDisk) ? $"/content/previews/{fileName}" : "placeholder";
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
            if (string.IsNullOrWhiteSpace(season.PosterPath) || season.PosterPath == "placeholder")
            {
                season.PosterPath = BuildPosterPath(season.Number);
            }
        }
    }

    if (context.ChangeTracker.HasChanges())
    {
        context.SaveChanges();
    }

    // Файлы на диске: /root/сезон N/cN cM.mp4 (WinSCP: c1 c1.mp4, c1 c2.mp4, …)
    SyncVideosFromDisk(context, videosStorageRoot);
}

static void SyncVideosFromDisk(DbBronyTV context, string videosRoot)
{
    if (string.IsNullOrWhiteSpace(videosRoot) || !Directory.Exists(videosRoot))
    {
        return;
    }

    var fileNameRegex = new Regex(@"^c(\d+)\s+c(\d+)\.mp4$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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
            var match = fileNameRegex.Match(name);
            if (!match.Success)
            {
                continue;
            }

            var fileSeason = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var episodeNumber = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            if (fileSeason != season.Number)
            {
                continue;
            }

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