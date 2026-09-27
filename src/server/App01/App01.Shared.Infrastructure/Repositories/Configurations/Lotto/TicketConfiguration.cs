using App01.Shared.Application.Entities.Lotto;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace App01.Shared.Application.Repositories.Configurations.Lotto;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets", "Lotto");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.UserId)
            .IsRequired()
            .HasColumnType("bigint");

        builder.Property(e => e.DrawTypeId)
            .IsRequired()
            .HasColumnType("int");

        builder.Property(e => e.GroupName)
            .HasColumnType("varchar(100)");

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasColumnType("datetime2");


        builder.Property(e => e.NumbersLow)
            .IsRequired()
            .HasColumnType("bigint");

        builder.Property(e => e.NumbersHigh)
            .IsRequired()
            .HasColumnType("bigint");

        builder.Property(e => e.SpecialsLow)
            .IsRequired()
            .HasColumnType("bigint");

        builder.Property(e => e.SpecialsHigh)
            .IsRequired()
            .HasColumnType("bigint");


        builder.HasIndex(e => e.UserId);

        builder.HasIndex(e => e.CreatedAt);

        builder.HasIndex(e => e.DrawTypeId);


        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.DrawType)
            .WithMany()
            .HasForeignKey(e => e.DrawTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}