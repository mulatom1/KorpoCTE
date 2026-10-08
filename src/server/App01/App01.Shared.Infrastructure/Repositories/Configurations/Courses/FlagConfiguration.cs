using App01.Shared.Application.Entities.Courses;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace App01.Shared.Application.Repositories.Configurations.Courses;


public class FlagConfiguration : IEntityTypeConfiguration<Flag>
{
    public void Configure(EntityTypeBuilder<Flag> builder)
    {
        builder.ToTable("Flags", "Courses");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnType("int")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.CourseId)
            .IsRequired()
            .HasColumnType("int");

        builder.Property(e => e.Code)
            .IsRequired()
            .HasColumnType("varchar(50)")
            .HasMaxLength(50);

        builder.Property(e => e.Title)
            .IsRequired()
            .HasColumnType("nvarchar(200)")
            .HasMaxLength(200);

        builder.Property(e => e.Criteria)
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(e => e.Code)
            .IsUnique();

        builder.HasOne(e => e.Course)
            .WithMany()
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}