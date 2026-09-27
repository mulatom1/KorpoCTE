using App01.Shared.Application.Entities.Portal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace App01.Shared.Application.Repositories.Configurations.Portal;


public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", "Portal");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Email)
                .IsRequired()
                .HasColumnType("varchar(255)")
                .HasMaxLength(255);

        builder.Property(e => e.PasswordHash)
            .IsRequired()
            .HasColumnType("varchar(255)")  
            .HasMaxLength(255);

        builder.Property(e => e.IsAdmin)
            .IsRequired()
            .HasColumnType("bit")
            .HasDefaultValue(false);

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasColumnType("datetime2")
            .HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.Email)
            .IsUnique();


        builder.HasMany(e => e.LottoTickets)
           .WithOne(e => e.User)
           .HasForeignKey(e => e.UserId)
           .OnDelete(DeleteBehavior.Restrict);
    }
}
