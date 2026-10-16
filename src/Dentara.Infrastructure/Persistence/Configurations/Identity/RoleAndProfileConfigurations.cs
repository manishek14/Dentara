namespace Dentara.Infrastructure.Persistence.Configurations.Identity;

using Dentara.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        // Role is an Enumeration, not a full entity. We seed it as a reference table.
        builder.HasKey(r => r.Value);

        builder.Property(r => r.Value).ValueGeneratedNever();
        builder.Property(r => r.Name).HasMaxLength(50).IsRequired();

        // Seed the four known roles
        builder.HasData(
            Role.Patient,
            Role.Dentist,
            Role.Receptionist,
            Role.Admin);
    }
}

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");
        builder.HasKey(ur => ur.Id);

        builder.Property(ur => ur.UserId).IsRequired();
        builder.Property(ur => ur.RoleValue).IsRequired();
        builder.Property(ur => ur.ExpiresAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(ur => ur.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(ur => ur.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(ur => ur.RowVersion).IsRowVersion();

        // UNIQUE(UserId, RoleValue) per architecture spec
        builder.HasIndex(ur => new { ur.UserId, ur.RoleValue })
            .IsUnique()
            .HasDatabaseName("IX_UserRoles_UserId_RoleValue");

        // CHECK constraint: RoleValue must be one of 1,2,3,4
        builder.ToTable(ur => ur.HasCheckConstraint(
            "CK_UserRoles_RoleValue",
            "RoleValue IN (1, 2, 3, 4)"));
    }
}

public sealed class PatientProfileConfiguration : IEntityTypeConfiguration<PatientProfile>
{
    public void Configure(EntityTypeBuilder<PatientProfile> builder)
    {
        builder.ToTable("PatientProfiles");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.UserId).IsRequired();
        builder.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.LastName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.NationalCode).HasMaxLength(10);
        builder.Property(p => p.Notes).HasMaxLength(2000);
        builder.Property(p => p.DateOfBirth).HasColumnType("datetimeoffset(0)");
        builder.Property(p => p.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(p => p.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.HasIndex(p => p.UserId).HasDatabaseName("IX_PatientProfiles_UserId");
        builder.HasIndex(p => new { p.FirstName, p.LastName }).HasDatabaseName("IX_PatientProfiles_Name");
    }
}

public sealed class DentistProfileConfiguration : IEntityTypeConfiguration<DentistProfile>
{
    public void Configure(EntityTypeBuilder<DentistProfile> builder)
    {
        builder.ToTable("DentistProfiles");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.UserId).IsRequired();
        builder.Property(d => d.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(d => d.Specialization).HasMaxLength(100);
        builder.Property(d => d.LicenseNumber).HasMaxLength(50);
        builder.Property(d => d.Bio).HasMaxLength(2000);
        builder.Property(d => d.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(d => d.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(d => d.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(d => d.RowVersion).IsRowVersion();

        builder.HasIndex(d => d.UserId).HasDatabaseName("IX_DentistProfiles_UserId");
    }
}
