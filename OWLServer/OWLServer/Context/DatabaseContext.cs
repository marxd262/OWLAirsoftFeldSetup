using Microsoft.EntityFrameworkCore;
using OWLServer.Models;

namespace OWLServer.Context
{
    public class DatabaseContext : DbContext
    {
        // TODO Datenbank hierhin
        // Tabllen:
        // Tower
        // Teams
        // Spielmodi-Config

        DbSet<Tower> Towers { get; set; }
        DbSet<Tower> Teams { get; set; }

        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options)
        {
            Database.EnsureCreated();
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Tower>(e =>
            {
                e.HasKey(t => t.Id);
                e.HasOne(t => t.Location);
            });
            builder.Entity<ElementLocation>(e =>
            {
                e.HasKey(el => el.Id);
            });
            builder.Entity<TeamBase>(e =>
            {
                e.HasKey(el => el.Id);
            });
        }
    }
}
