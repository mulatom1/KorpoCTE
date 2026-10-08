using App01.Shared.Application.Entities.Courses;
using App01.Shared.Application.Entities.Portal;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace App01.Shared.Application.Repositories.Configurations.Courses;


public class UserFlagConfiguration : IEntityTypeConfiguration<UserFlag>
{
    public void Configure(EntityTypeBuilder<UserFlag> builder)
    {
        builder.ToTable("UserFlags", "Courses");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnType("int")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.UserId)
            .IsRequired()
            .HasColumnType("bigint");

        builder.Property(e => e.FlagId)
            .IsRequired()
            .HasColumnType("int");

        builder.Property(e => e.EarnedAt)
            .IsRequired()
            .HasColumnType("datetime2");

        // Flaga zdobyta najwyżej raz na użytkownika - wymuszone w bazie, nie tylko w handlerze
        builder.HasIndex(e => new { e.UserId, e.FlagId })
            .IsUnique();

        builder.HasOne(e => e.Flag)
            .WithMany()
            .HasForeignKey(e => e.FlagId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}