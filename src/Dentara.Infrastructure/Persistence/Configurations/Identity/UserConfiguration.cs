namespace Dentara.Infrastructure.Persistence.Configurations.Identity;

using Dentara.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(u => u.PhoneNumberNormalized).HasMaxLength(20).IsRequired();

        // Unique index on PhoneNumberNormalized per architecture spec
        builder.HasIndex(u => u.PhoneNumberNormalized)
            .IsUnique()
            .HasDatabaseName("IX_Users_PhoneNumberNormalized");

        builder.Property(u => u.PasswordHash).HasMaxLength(500);
        builder.Property(u => u.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(u => u.IsPhoneVerified).IsRequired().HasDefaultValue(false);

        builder.Property(u => u.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(u => u.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(u => u.LastLoginAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(u => u.PhoneVerifiedAtUtc).HasColumnType("datetimeoffset(0)");

        // RowVersion for optimistic concurrency
        builder.Property(u => u.RowVersion).IsRowVersion();

        builder.HasMany(u => u.Roles)
            .WithOne()
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade); // role assignments can cascade

        builder.HasMany(u => u.PatientProfiles)
            .WithOne()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.DentistProfiles)
            .WithOne()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(u => u.Roles);
        builder.Ignore(u => u.PatientProfiles);
        builder.Ignore(u => u.DentistProfiles);
        // These navigations are ignored because EF Core does not support
        // private setter collections without explicit configuration. The
        // relationships are still tracked via the dependent side (UserRole,
        // PatientProfile, DentistProfile).
    }
}
