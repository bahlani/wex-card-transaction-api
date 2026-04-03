using CardTransactionApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CardTransactionApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Card>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.CreditLimit)
                .HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Amount)
                .HasColumnType("decimal(18,2)");
            entity.Property(t => t.Description)
                .HasMaxLength(500)
                .IsRequired();
            entity.HasOne(t => t.Card)
                .WithMany(c => c.Transactions)
                .HasForeignKey(t => t.CardId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
