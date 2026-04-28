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
        public DbSet<TowerPositionLayout> TowerPositionLayouts { get; set; }
        public DbSet<TowerPosition> TowerPositions { get; set; }

        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options)
        {
            if (!Database.EnsureCreated())
            {
                Database.ExecuteSqlRaw(
                    "CREATE TABLE IF NOT EXISTS \"TowerControlLayouts\" (" +
                    "    \"Id\" INTEGER NOT NULL CONSTRAINT \"PK_TowerControlLayouts\" PRIMARY KEY AUTOINCREMENT," +
                    "    \"Name\" TEXT NOT NULL" +
                    ");");
                Database.ExecuteSqlRaw(
                    "CREATE TABLE IF NOT EXISTS \"TowerControlLinks\" (" +
                    "    \"Id\" INTEGER NOT NULL CONSTRAINT \"PK_TowerControlLinks\" PRIMARY KEY AUTOINCREMENT," +
                    "    \"TowerControlLayoutId\" INTEGER NOT NULL," +
                    "    \"ControllerTowerMacAddress\" TEXT NOT NULL," +
                    "    \"ControlledTowerMacAddress\" TEXT NOT NULL," +
                    "    CONSTRAINT \"FK_TowerControlLinks_TowerControlLayouts_TowerControlLayoutId\" " +
                    "        FOREIGN KEY (\"TowerControlLayoutId\") REFERENCES \"TowerControlLayouts\" (\"Id\") ON DELETE CASCADE" +
                    ");");
            }
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
            builder.Entity<TowerPositionLayout>(e =>
            {
                e.HasKey(tpl => tpl.Id);
                e.HasMany(tpl => tpl.Positions)
                 .WithOne()
                 .HasForeignKey(tp => tp.TowerPositionLayoutId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
            builder.Entity<TowerPosition>(e =>
            {
                e.HasKey(tp => tp.Id);
            });
        }
    }
}
