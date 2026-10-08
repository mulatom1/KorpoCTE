using App01.Shared.Application.Entities.Courses;
using App01.Shared.Application.Entities.Lotto;
using App01.Shared.Application.Entities.Portal;

using Microsoft.EntityFrameworkCore;


namespace App01.Shared.Infrastructure.Repositories;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Portal
    public DbSet<User> Users { get; set; }
    public DbSet<Mail> Mails { get; set; }


    // Lotto
    public DbSet<DrawType> DrawTypes { get; set; } = null!;

    public DbSet<DrawTypeWinTier> DrawTypeWinTiers { get; set; } = null!;

    public DbSet<Draw> Draws { get; set; } = null!;

    public DbSet<Ticket> Tickets { get; set; } = null!;


    // Courses
    public DbSet<Course> Courses { get; set; } = null!;

    public DbSet<Flag> Flags { get; set; } = null!;

    public DbSet<UserFlag> UserFlags { get; set; } = null!;


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}