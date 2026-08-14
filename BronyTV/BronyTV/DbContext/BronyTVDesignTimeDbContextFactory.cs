using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BronyTV.DbContext;

/// <summary>
/// Design-time factory used by `dotnet ef migrations` so migrations can be
/// generated without a live database or app config (the migrations snapshots
/// are produced from the EF model, not from the database).
/// </summary>
public class BronyTVDesignTimeDbContextFactory : IDesignTimeDbContextFactory<DbBronyTV>
{
    public DbBronyTV CreateDbContext(string[] args)
    {
        const string designTimeConnectionString =
            "Host=localhost;Port=5432;Database=BronyTV_DesignTime;Username=postgres;Password=123456";

        var optionsBuilder = new DbContextOptionsBuilder<DbBronyTV>();
        optionsBuilder.UseNpgsql(
            designTimeConnectionString,
            x => x.MigrationsHistoryTable("__EFMigrationsHistory", "public"));

        return new DbBronyTV(optionsBuilder.Options);
    }
}
