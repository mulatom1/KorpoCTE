using App01.Shared.Application.Entities.Courses;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace App01.Shared.Application.Repositories.Configurations.Courses;


public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Courses", "Courses");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnType("int")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Slug)
            .IsRequired()
            .HasColumnType("varchar(100)")
            .HasMaxLength(100);

        builder.Property(e => e.PublishDate)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasIndex(e => e.Slug)
            .IsUnique();
    }
}