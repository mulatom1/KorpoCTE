using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using App01.Shared.Application.Entities.Portal;


namespace App01.Shared.Application.Repositories.Configurations.Portal;


public class MailConfiguration : IEntityTypeConfiguration<Mail>
{
    public void Configure(EntityTypeBuilder<Mail> builder)
    {
        builder.ToTable("Mails", "Portal");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Email)
            .IsRequired()
            .HasColumnType("varchar(255)")
            .HasMaxLength(255);

        builder.Property(e => e.Topic)
            .IsRequired()
            .HasColumnType("varchar(500)")
            .HasMaxLength(500);

        builder.Property(e => e.Body)
            .HasColumnType("varchar(4000)")
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnType("datetime2")
            .IsRequired();
    }
}
