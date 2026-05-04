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
        public DbSet<GameHistory> GameHistories { get; set; }
        public DbSet<GameHistoryTeam> GameHistoryTeams { get; set; }
        public DbSet<GameHistoryTower> GameHistoryTowers { get; set; }
        public DbSet<GameHistorySnapshot> GameHistorySnapshots { get; set; }
        public DbSet<GameHistoryEvent> GameHistoryEvents { get; set; }

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
                Database.ExecuteSqlRaw(
                    "CREATE TABLE IF NOT EXISTS \"GameHistories\" (" +
                    "    \"Id\" INTEGER NOT NULL CONSTRAINT \"PK_GameHistories\" PRIMARY KEY AUTOINCREMENT," +
                    "    \"GameMode\" INTEGER NOT NULL," +
                    "    \"StartTime\" TEXT NOT NULL," +
                    "    \"EndTime\" TEXT NULL," +
                    "    \"Duration\" TEXT NOT NULL," +
                    "    \"Winner\" INTEGER NOT NULL," +
                    "    \"EndReason\" TEXT NOT NULL DEFAULT ''" +
                    ");");
                Database.ExecuteSqlRaw(
                    "CREATE TABLE IF NOT EXISTS \"GameHistoryTeams\" (" +
                    "    \"Id\" INTEGER NOT NULL CONSTRAINT \"PK_GameHistoryTeams\" PRIMARY KEY AUTOINCREMENT," +
                    "    \"GameHistoryId\" INTEGER NOT NULL," +
                    "    \"TeamColor\" INTEGER NOT NULL," +
                    "    \"TeamName\" TEXT NOT NULL DEFAULT ''," +
                    "    \"Side\" TEXT NOT NULL DEFAULT ''," +
                    "    \"FinalScore\" INTEGER NOT NULL DEFAULT 0," +
                    "    \"Deaths\" INTEGER NOT NULL DEFAULT 0," +
                    "    \"TowersControlled\" INTEGER NOT NULL DEFAULT 0," +
                    "    CONSTRAINT \"FK_GameHistoryTeams_GameHistories_GameHistoryId\" " +
                    "        FOREIGN KEY (\"GameHistoryId\") REFERENCES \"GameHistories\" (\"Id\") ON DELETE CASCADE" +
                    ");");
                Database.ExecuteSqlRaw(
                    "CREATE TABLE IF NOT EXISTS \"GameHistoryTowers\" (" +
                    "    \"Id\" INTEGER NOT NULL CONSTRAINT \"PK_GameHistoryTowers\" PRIMARY KEY AUTOINCREMENT," +
                    "    \"GameHistoryId\" INTEGER NOT NULL," +
                    "    \"MacAddress\" TEXT NOT NULL DEFAULT ''," +
                    "    \"DisplayLetter\" TEXT NOT NULL DEFAULT ''," +
                    "    \"FinalColor\" INTEGER NOT NULL DEFAULT -1," +
                    "    \"Captures\" INTEGER NOT NULL DEFAULT 0," +
                    "    CONSTRAINT \"FK_GameHistoryTowers_GameHistories_GameHistoryId\" " +
                    "        FOREIGN KEY (\"GameHistoryId\") REFERENCES \"GameHistories\" (\"Id\") ON DELETE CASCADE" +
                    ");");
                Database.ExecuteSqlRaw(
                    "CREATE TABLE IF NOT EXISTS \"GameHistorySnapshots\" (" +
                    "    \"Id\" INTEGER NOT NULL CONSTRAINT \"PK_GameHistorySnapshots\" PRIMARY KEY AUTOINCREMENT," +
                    "    \"GameHistoryId\" INTEGER NOT NULL," +
                    "    \"SnapshotJSON\" TEXT NOT NULL DEFAULT ''," +
                    "    CONSTRAINT \"FK_GameHistorySnapshots_GameHistories_GameHistoryId\" " +
                    "        FOREIGN KEY (\"GameHistoryId\") REFERENCES \"GameHistories\" (\"Id\") ON DELETE CASCADE" +
                    ");");
                Database.ExecuteSqlRaw(
                    "CREATE TABLE IF NOT EXISTS \"GameHistoryEvents\" (" +
                    "    \"Id\" INTEGER NOT NULL CONSTRAINT \"PK_GameHistoryEvents\" PRIMARY KEY AUTOINCREMENT," +
                    "    \"GameHistoryId\" INTEGER NOT NULL," +
                    "    \"Timestamp\" TEXT NOT NULL," +
                    "    \"EventType\" INTEGER NOT NULL," +
                    "    \"TeamColor\" INTEGER NOT NULL," +
                    "    \"Side\" TEXT NOT NULL DEFAULT ''," +
                    "    \"TowerLetter\" TEXT NULL," +
                    "    \"Value\" INTEGER NULL," +
                    "    CONSTRAINT \"FK_GameHistoryEvents_GameHistories_GameHistoryId\" " +
                    "        FOREIGN KEY (\"GameHistoryId\") REFERENCES \"GameHistories\" (\"Id\") ON DELETE CASCADE" +
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
            builder.Entity<GameHistory>(e =>
            {
                e.HasKey(gh => gh.Id);
                e.HasMany(gh => gh.Teams)
                 .WithOne(ght => ght.GameHistory)
                 .HasForeignKey(ght => ght.GameHistoryId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasMany(gh => gh.Towers)
                 .WithOne(ght => ght.GameHistory)
                 .HasForeignKey(ght => ght.GameHistoryId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(gh => gh.Snapshot)
                 .WithOne(gs => gs.GameHistory)
                 .HasForeignKey<GameHistorySnapshot>(gs => gs.GameHistoryId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
            builder.Entity<GameHistoryTeam>(e =>
            {
                e.HasKey(ght => ght.Id);
            });
            builder.Entity<GameHistoryTower>(e =>
            {
                e.HasKey(ght => ght.Id);
            });
            builder.Entity<GameHistorySnapshot>(e =>
            {
                e.HasKey(gs => gs.Id);
            });
            builder.Entity<GameHistoryEvent>(e =>
            {
                e.HasKey(ge => ge.Id);
                e.HasOne(ge => ge.GameHistory)
                 .WithMany()
                 .HasForeignKey(ge => ge.GameHistoryId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
