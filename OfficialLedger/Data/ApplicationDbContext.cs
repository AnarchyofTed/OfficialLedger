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
    public DbSet<Expense> Expenses => Set<Expense>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Expense>().ToTable("Expense");
        builder.Entity<Expense>().Property(x => x.ExpenseDate).HasColumnType("date");
        builder.Entity<Expense>().Property(x => x.Amount).HasPrecision(18, 2);
        builder.Entity<Expense>().HasIndex(x => new { x.UserId, x.ExpenseDate });

        builder.Entity<Game>().ToTable("Game");
        builder.Entity<League>().ToTable("League");
        builder.Entity<SportType>().ToTable("SportType");
        builder.Entity<Season>().ToTable("Season");
        builder.Entity<Season>().Property(x => x.UserId).HasMaxLength(450);
        builder.Entity<Season>().Property(x => x.SportImage).HasMaxLength(32);
        builder.Entity<Season>().HasIndex(x => x.UserId);
        builder.Entity<Game>().HasOne(x => x.Season).WithMany()
            .HasForeignKey(x => x.SeasonId).OnDelete(DeleteBehavior.Restrict);
    }
}

