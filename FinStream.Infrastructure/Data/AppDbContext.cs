// INFRASTRUCTURE FILE: Entity Framework Core database context.
// This is the main "database connection manager" that EF Core uses to:
// 1. Define our tables (DbSet properties)
// 2. Configure how data is stored (OnModelCreating)
// 3. Seed initial data for testing/demo purposes

// EF Core packages - this is the ORM (Object-Relational Mapper) that lets us work with databases using C# objects
using Microsoft.EntityFrameworkCore;
using FinStream.Domain.Entities;

namespace FinStream.Infrastructure.Data;

/// <summary>
/// The main database context for FinStream.
/// Think of this as the "database connection object" that EF Core uses to:
/// - Know which tables exist (via DbSet properties)
/// - Configure column types, indexes, relationships
/// - Access and modify data
/// </summary>
public class AppDbContext : DbContext
{
    // Constructor: EF Core injects the DbContextOptions (connection string, provider, etc.)
    // This is how we know WHICH database to connect to (SQL Server, Postgres, InMemory, etc.)
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // DbSet properties: These define our "tables" in the database.
    // Each DbSet corresponds to one entity type and allows CRUD operations.
    // Think of these as "collections" that EF Core translates to SQL queries.

    /// <summary>All tracked financial instruments (stocks, ETFs, etc.)</summary>
    public DbSet<Instrument> Instruments => Set<Instrument>();

    /// <summary>All calculated metric snapshots (SMA, EMA, Volatility at each point in time)</summary>
    public DbSet<MetricSnapshot> Metrics => Set<MetricSnapshot>();

    /// <summary>All signal events (alerts that fired when rules triggered)</summary>
    public DbSet<SignalEvent> Signals => Set<SignalEvent>();

    /// <summary>All signal rules (trader-defined conditions for triggering alerts)</summary>
    public DbSet<SignalRule> Rules => Set<SignalRule>();

    /// <summary>
    /// Configures the database schema: column types, indexes, relationships, constraints.
    /// This runs once when the database is created or updated.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // IMPORTANT: Always call base implementation first - it does important internal setup
        base.OnModelCreating(modelBuilder);

        // Configure Instrument entity:
        // - Id is the primary key
        // - Symbol has a UNIQUE index (can't have two instruments with the same ticker)
        // - Symbol is required and max 10 chars (tickers are short like "AAPL", "MSFT")
        // - Name is optional and max 100 chars
        modelBuilder.Entity<Instrument>(entity =>
        {
            entity.HasKey(e => e.Id);  // Primary key
            entity.HasIndex(e => e.Symbol).IsUnique();  // Unique index on Symbol
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(10);  // Required, max 10 chars
            entity.Property(e => e.Name).HasMaxLength(100);  // Optional, max 100 chars
        });

        // Configure MetricSnapshot entity:
        // - Id is the primary key
        // - Composite index on (InstrumentId, Timestamp) for fast history queries
        // - All decimal properties have 4 decimal places (precise enough for financial data)
        // - Each metric belongs to one instrument (one-to-many relationship)
        modelBuilder.Entity<MetricSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.InstrumentId, e.Timestamp });  // Fast lookup by instrument + time
            entity.Property(e => e.Price).HasPrecision(18, 4);  // Store up to 18 digits, 4 after decimal
            entity.Property(e => e.Sma).HasPrecision(18, 4);
            entity.Property(e => e.Ema).HasPrecision(18, 4);
            entity.Property(e => e.Volatility).HasPrecision(18, 4);
            entity.Property(e => e.PriceChangePct).HasPrecision(18, 4);
            entity.HasOne(e => e.Instrument)  // Each metric has one instrument
                  .WithMany(i => i.Metrics)  // Each instrument has many metrics
                  .HasForeignKey(e => e.InstrumentId)  // Foreign key column
                  .OnDelete(DeleteBehavior.Cascade);  // Delete metrics when instrument is deleted
        });

        // Configure SignalEvent entity:
        // - Id is the primary key
        // - Composite index on (InstrumentId, TriggeredAt) for fast signal history queries
        // - RuleName is the name of the rule that triggered (e.g., "SPIKE", "DIP")
        // - Each signal belongs to one instrument (one-to-many relationship)
        modelBuilder.Entity<SignalEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.InstrumentId, e.TriggeredAt });  // Fast lookup by instrument + time
            entity.Property(e => e.Price).HasPrecision(18, 4);  // Price when signal fired
            entity.Property(e => e.RuleName).HasMaxLength(50);  // Rule name like "SPIKE", "DIP"
            entity.HasOne(e => e.Instrument)  // Each signal has one instrument
                  .WithMany(i => i.Signals)  // Each instrument has many signals
                  .HasForeignKey(e => e.InstrumentId)
                  .OnDelete(DeleteBehavior.Cascade);  // Delete signals when instrument is deleted
        });

        // Configure SignalRule entity:
        // - Id is the primary key
        // - Name has a UNIQUE index (can't have two rules with the same name)
        // - ConditionType stores the condition type (e.g., "PRICECHANGE_PCT_GT")
        // - Threshold is the value to compare against (e.g., 5.0 means "5%")
        modelBuilder.Entity<SignalRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();  // Unique index on name
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.ConditionType).HasMaxLength(50);
            entity.Property(e => e.Threshold).HasPrecision(18, 4);
        });

        // Seed initial data: Insert sample data when the database is first created
        // This is useful for testing, demos, and having data to show immediately
        SeedData(modelBuilder);
    }

    /// <summary>
    /// Seeds the database with sample data for demo/testing purposes.
    /// This runs once when the database is first created (like a SQL INSERT script).
    /// </summary>
    private static void SeedData(ModelBuilder modelBuilder)
    {
        // Create sample instruments with fixed IDs so they're consistent across runs
        var aaplId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var googId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var msftId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        // Seed 3 popular stocks: Apple, Google, Microsoft
        modelBuilder.Entity<Instrument>().HasData(
            new Instrument { Id = aaplId, Symbol = "AAPL", Name = "Apple Inc.", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Instrument { Id = googId, Symbol = "GOOG", Name = "Alphabet Inc.", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Instrument { Id = msftId, Symbol = "MSFT", Name = "Microsoft Corporation", IsActive = true, CreatedAt = DateTime.UtcNow }
        );

        // Seed 3 default signal rules that traders commonly want:
        // - SPIKE: Alert when price increases by more than 5%
        // - DIP: Alert when price decreases by more than 5%
        // - VOLATILE: Alert when volatility exceeds 3%
        modelBuilder.Entity<SignalRule>().HasData(
            new SignalRule { Id = Guid.NewGuid(), Name = "SPIKE", ConditionType = "PRICECHANGE_PCT_GT", Threshold = 5.0m, IsActive = true, CreatedAt = DateTime.UtcNow },
            new SignalRule { Id = Guid.NewGuid(), Name = "DIP", ConditionType = "PRICECHANGE_PCT_LT", Threshold = -5.0m, IsActive = true, CreatedAt = DateTime.UtcNow },
            new SignalRule { Id = Guid.NewGuid(), Name = "VOLATILE", ConditionType = "VOLATILITY_GT", Threshold = 3.0m, IsActive = true, CreatedAt = DateTime.UtcNow }
        );
    }
}