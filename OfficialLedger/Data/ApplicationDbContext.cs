using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OfficialLedger.Models;

namespace OfficialLedger.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<League> Leagues => Set<League>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Season> Seasons => Set<Season>();
}