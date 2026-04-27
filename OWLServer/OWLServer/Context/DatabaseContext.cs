using Microsoft.EntityFrameworkCore;
using OWLServer.Models;

namespace OWLServer.Context
{
    public class DatabaseContext : DbContext
    {
        public DbSet<Tower> Towers { get; set; }
        public DbSet<TeamBase> Teams { get; set; }
        public DbSet<ChainLayout> ChainLayouts { get; set; }
        public DbSet<ChainLink> ChainLinks { get; set; }
        public DbSet<TowerControlLayout> TowerControlLayouts { get; set; }
        public DbSet<TowerControlLink> TowerControlLinks { get; set; }

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
            builder.Entity<ChainLayout>(e =>
            {
                e.HasKey(cl => cl.Id);
                e.HasMany(cl => cl.Links)
                 .WithOne()
                 .HasForeignKey(lnk => lnk.ChainLayoutId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
            builder.Entity<ChainLink>(e =>
            {
                e.HasKey(lnk => lnk.Id);
            });
            builder.Entity<TowerControlLayout>(e =>
            {
                e.HasKey(tcl => tcl.Id);
                e.HasMany(tcl => tcl.Links)
                 .WithOne()
                 .HasForeignKey(lnk => lnk.TowerControlLayoutId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
            builder.Entity<TowerControlLink>(e =>
            {
                e.HasKey(lnk => lnk.Id);
            });
        }
    }
}
