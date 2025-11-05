using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using JobSearch.Services;

namespace JobSearch.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<JobOffer> JobOffer { get; set; } = default!;
        public DbSet<AuditLog> AuditLogs { get; set; } = default!;
        public DbSet<JobApplication> JobApplications { get; set; } = default!;
        public DbSet<UserProfileCV> UserProfileCVs { get; set; } = default!;
        public DbSet<CVSkill> CVSkills { get; set; } = default!;
        public DbSet<CVLanguage> CVLanguages { get; set; } = default!;

        // --- DODAJ TĘ LINIĘ ---
        public DbSet<Notification> Notifications { get; set; } = default!;


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Relacje dla Ofert i Aplikacji (bez zmian)
            builder.Entity<ApplicationUser>()
        .HasMany(u => u.CreatedJobOffers)
        .WithOne(o => o.CreatedByUser)
        .HasForeignKey(o => o.CreatedByUserId)
        .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ApplicationUser>()
              .HasMany(u => u.Applications)
              .WithOne(a => a.Applicant)
              .HasForeignKey(a => a.ApplicantId)
              .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<JobOffer>()
              .HasMany(o => o.Applications)
              .WithOne(a => a.JobOffer)
              .HasForeignKey(a => a.JobOfferId)
              .OnDelete(DeleteBehavior.Cascade);

            // Relacje dla CV (bez zmian)
            builder.Entity<ApplicationUser>()
        .HasOne(u => u.UserProfileCV)
        .WithOne(cv => cv.User)
        .HasForeignKey<UserProfileCV>(cv => cv.UserId);

            builder.Entity<UserProfileCV>()
              .HasMany(cv => cv.Languages)
              .WithOne(l => l.UserProfileCV)
              .HasForeignKey(l => l.UserProfileCVId)
              .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserProfileCV>()
              .HasMany(cv => cv.Skills)
              .WithOne(s => s.UserProfileCV)
              .HasForeignKey(s => s.UserProfileCVId)
              .OnDelete(DeleteBehavior.Cascade);

            // --- DODAJ TĘ KONFIGURACJĘ ---
            // Relacja Użytkownik -> Powiadomienia (jeden-do-wielu)
            builder.Entity<ApplicationUser>()
                .HasMany(u => u.Notifications)
                .WithOne(n => n.User)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade); // Usuń powiadomienia, gdy użytkownik jest usuwany
        }
    }
}