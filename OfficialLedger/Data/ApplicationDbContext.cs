using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OfficialLedger.Models;

namespace OfficialLedger.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<League> Leagues => Set<League>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<SportType> SportTypes => Set<SportType>();
    public DbSet<Season> Seasons => Set<Season>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Game>().ToTable("Game");
        builder.Entity<League>().ToTable("League");
        builder.Entity<SportType>().ToTable("SportType");
        builder.Entity<Season>().ToTable("Season");
    }
}
