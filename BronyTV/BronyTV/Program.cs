using BronyTV.DbContext;
using BronyTV.Repository;
using BronyTV.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using BronyTV.DbContext.Entity;

var builder = WebApplication.CreateBuilder(args);
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
}

if (!string.Equals(
        Environment.GetEnvironmentVariable("DISABLE_HTTPS_REDIRECT"),
        "true",
        StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();