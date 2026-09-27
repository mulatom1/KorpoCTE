using App01.Shared.Application.Entities.Lotto;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace App01.Shared.Application.Repositories.Configurations.Lotto;

public class DrawTypeWinTierConfiguration : IEntityTypeConfiguration<DrawTypeWinTier>
{
    public void Configure(EntityTypeBuilder<DrawTypeWinTier> builder)
    {
        builder.ToTable("DrawTypeWinTiers", "Lotto");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnType("int")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.DrawTypeId)
            .HasColumnType("int")
            .IsRequired();

        builder.Property(e => e.WinTier)
            .HasColumnType("int")
            .IsRequired();

        builder.Property(e => e.NumbersMatchCount)
            .HasColumnType("int")
            .IsRequired();

        builder.Property(e => e.SpecialsMatchCount)
            .HasColumnType("int")
            .IsRequired();

        builder.Property(e => e.PotentialWinPrize)
              .IsRequired()
              .HasPrecision(18, 2)
              .HasColumnType("numeric(18, 2)");

        builder.HasIndex(e => e.DrawTypeId);


        builder.HasOne(e => e.DrawType)
            .WithMany(e => e.DrawTypeWinTiers)
            .HasForeignKey(e => e.DrawTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
