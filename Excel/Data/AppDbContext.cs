using Microsoft.EntityFrameworkCore;
using SqlDummySeeder.Excel.Models;

namespace SqlDummySeeder.Excel.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Template> Templates => Set<Template>();
    public DbSet<ColumnDefinition> Columns => Set<ColumnDefinition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Template>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<ColumnDefinition>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Mode).HasConversion<string>().IsRequired();
            e.Property(x => x.ListItemsRaw).HasColumnType("TEXT");
            e.Property(x => x.FormatString).HasMaxLength(4000);
            e.HasOne(x => x.Template)
                .WithMany(t => t.Columns)
                .HasForeignKey(x => x.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TemplateId, x.Order }).IsUnique();
        });
    }
}
