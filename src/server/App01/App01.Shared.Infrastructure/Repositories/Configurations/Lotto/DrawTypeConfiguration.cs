using App01.Shared.Application.Entities.Lotto;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App01.Shared.Application.Repositories.Configurations.Lotto;

public class DrawTypeConfiguration : IEntityTypeConfiguration<DrawType>
{
    public void Configure(EntityTypeBuilder<DrawType> builder)
    {
        builder.ToTable("DrawTypes", "Lotto");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnType("int")
            .ValueGeneratedNever();

        builder.Property(e => e.Name)
            .IsRequired()
            .HasColumnType("varchar(50)")
            .HasMaxLength(50);

        builder.Property(e => e.Description)
            .IsRequired()
            .HasColumnType("varchar(1000)")
            .HasMaxLength(1000);

        builder.Property(e => e.TicketPrize)
            .IsRequired()
            .HasColumnType("decimal(18,2)");
        
        builder.Property(e => e.NumbersCount)
            .IsRequired()
            .HasColumnType("int");
        builder.Property(e => e.NumbersMaxValue)
            .IsRequired()
            .HasColumnType("int");
        
        builder.Property(e => e.SpecialsCount)
            .IsRequired()
            .HasColumnType("int");

        builder.Property(e => e.SpecialsMaxValue)
            .IsRequired()
            .HasColumnType("int");


        builder.HasMany(e => e.Draws)
            .WithOne(e => e.DrawType)
            .HasForeignKey(e => e.DrawTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
