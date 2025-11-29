using Microsoft.EntityFrameworkCore;
using webProject.Models;

namespace webProject.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<CalculationHistory> CalculationHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                // Indexes for fast lookups
                entity.HasIndex(e => e.Email).IsUnique();
                
                // Required properties with constraints
                entity.Property(e => e.Email)
                    .IsRequired()
                    .HasMaxLength(255);
                    
                entity.Property(e => e.PasswordHash)
                    .IsRequired()
                    .HasMaxLength(255);
                    
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
            });

            modelBuilder.Entity<CalculationHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                // Indexes for optimized queries
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => new { e.UserId, e.Success }); // Composite index for filtering user's successful/failed calculations
                
                // Required properties with constraints
                entity.Property(e => e.Size)
                    .IsRequired();
                    
                entity.Property(e => e.MatrixData)
                    .IsRequired();
                    
                entity.Property(e => e.Solution)
                    .IsRequired();
                    
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                
                // Check constraint for positive matrix size
                entity.ToTable(t => t.HasCheckConstraint("CK_CalculationHistory_MatrixSize", "\"Size\" > 0"));
                
                // Foreign key relationship with cascade delete
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}