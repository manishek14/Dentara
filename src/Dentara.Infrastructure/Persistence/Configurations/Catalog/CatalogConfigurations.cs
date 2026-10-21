namespace Dentara.Infrastructure.Persistence.Configurations.Catalog;

using Dentara.Domain.Catalog;
using Dentara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Services");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(2000);
        builder.Property(s => s.DurationMinutes).IsRequired();
        builder.Property(s => s.PriceAmount).HasColumnType("decimal(18, 2)").IsRequired();
        builder.Property(s => s.Currency).HasMaxLength(3).IsRequired();
        builder.Property(s => s.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(s => s.Version).IsRequired();
        builder.Property(s => s.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(s => s.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(s => s.RowVersion).ConfigureRowVersion();

        // CHECK constraints per architecture spec
        builder.ToTable(s => s.HasCheckConstraint(
            "CK_Services_DurationPositive",
            "DurationMinutes > 0 AND DurationMinutes <= 1440"));
        builder.ToTable(s => s.HasCheckConstraint(
            "CK_Services_PriceNonNegative",
            "PriceAmount >= 0"));
        builder.ToTable(s => s.HasCheckConstraint(
            "CK_Services_CurrencyLength",
            "LEN(Currency) = 3"));

        builder.HasIndex(s => s.IsActive).HasDatabaseName("IX_Services_IsActive");
    }
}

public sealed class DentistServiceConfiguration : IEntityTypeConfiguration<DentistService>
{
    public void Configure(EntityTypeBuilder<DentistService> builder)
    {
        builder.ToTable("DentistServices");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.DentistId).IsRequired();
        builder.Property(d => d.ServiceId).IsRequired();
        builder.Property(d => d.DurationMinutesOverride);
        builder.Property(d => d.PriceAmountOverride).HasColumnType("decimal(18, 2)");
        builder.Property(d => d.CurrencyOverride).HasMaxLength(3);
        builder.Property(d => d.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(d => d.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(d => d.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(d => d.RowVersion).ConfigureRowVersion();

        // UNIQUE(DentistId, ServiceId)
        builder.HasIndex(d => new { d.DentistId, d.ServiceId })
            .IsUnique()
            .HasDatabaseName("IX_DentistServices_DentistId_ServiceId");

        // CHECK: if PriceAmountOverride is set, CurrencyOverride must be set
        builder.ToTable(d => d.HasCheckConstraint(
            "CK_DentistServices_OverrideConsistency",
            "(PriceAmountOverride IS NULL AND CurrencyOverride IS NULL) OR " +
            "(PriceAmountOverride IS NOT NULL AND CurrencyOverride IS NOT NULL)"));
    }
}

public sealed class ClinicResourceConfiguration : IEntityTypeConfiguration<ClinicResource>
{
    public void Configure(EntityTypeBuilder<ClinicResource> builder)
    {
        builder.ToTable("ClinicResources");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(2000);
        builder.Property(r => r.TypeValue).IsRequired();
        builder.Property(r => r.Capacity).IsRequired();
        builder.Property(r => r.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(r => r.OwnerDentistId);
        builder.Property(r => r.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(r => r.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(r => r.RowVersion).ConfigureRowVersion();

        // CHECK: TypeValue must be one of 1,2,3,4
        builder.ToTable(r => r.HasCheckConstraint(
            "CK_ClinicResources_TypeValue",
            "TypeValue IN (1, 2, 3, 4)"));
        builder.ToTable(r => r.HasCheckConstraint(
            "CK_ClinicResources_CapacityPositive",
            "Capacity >= 1"));

        builder.HasIndex(r => r.TypeValue).HasDatabaseName("IX_ClinicResources_TypeValue");
        builder.HasIndex(r => r.IsActive).HasDatabaseName("IX_ClinicResources_IsActive");
    }
}

public sealed class ResourceServiceRequirementConfiguration : IEntityTypeConfiguration<ResourceServiceRequirement>
{
    public void Configure(EntityTypeBuilder<ResourceServiceRequirement> builder)
    {
        builder.ToTable("ResourceServiceRequirements");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ServiceId).IsRequired();
        builder.Property(r => r.TypeValue).IsRequired();
        builder.Property(r => r.QuantityRequired).IsRequired();
        builder.Property(r => r.RequireDentistOwned).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.SpecificResourceId);
        builder.Property(r => r.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(r => r.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(r => r.RowVersion).ConfigureRowVersion();

        builder.ToTable(r => r.HasCheckConstraint(
            "CK_ResourceServiceRequirements_TypeValue",
            "TypeValue IN (1, 2, 3, 4)"));
        builder.ToTable(r => r.HasCheckConstraint(
            "CK_ResourceServiceRequirements_QuantityPositive",
            "QuantityRequired >= 1"));
        // Mutual exclusivity: RequireDentistOwned and SpecificResourceId cannot both be set
        builder.ToTable(r => r.HasCheckConstraint(
            "CK_ResourceServiceRequirements_MutualExclusivity",
            "NOT (RequireDentistOwned = 1 AND SpecificResourceId IS NOT NULL)"));

        builder.HasIndex(r => r.ServiceId).HasDatabaseName("IX_ResourceServiceRequirements_ServiceId");
    }
}
