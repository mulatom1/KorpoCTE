using App01.Shared.Application.Entities.Lotto;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace App01.Shared.Application.Repositories.Configurations.Lotto;


public class DrawConfiguration : IEntityTypeConfiguration<Draw>
{
    public void Configure(EntityTypeBuilder<Draw> builder)
    {
        builder.ToTable("Draws", "Lotto");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.DrawSystemId)
                .IsRequired()
                .HasColumnType("bigint");


        builder.Property(e => e.DrawDate)
                .IsRequired()
                .HasColumnType("datetime2");

        builder.Property(e => e.DrawTypeId)
                .IsRequired()
                .HasColumnType("int");

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


        // Indeks na DrawDate dla sortowania i filtrowania po dacie
        builder.HasIndex(e => e.DrawDate)
            .HasDatabaseName("IX_Draws_DrawDate");

        // Indeks na DrawSystemId dla szybkiego wyszukiwania po ID systemu
        builder.HasIndex(e => e.DrawSystemId)
            .HasDatabaseName("IX_Draws_DrawSystemId");

        // **KLUCZOWY INDEKS KOMPOZYTOWY** dla zapytania w LottoWorker04
        // Zapytanie: WHERE DrawTypeId = @drawTypeId AND DrawSystemId = @drawSystemId
        builder.HasIndex(e => new { e.DrawTypeId, e.DrawSystemId, e.DrawDate })
            .HasDatabaseName("IX_Draws_DrawTypeId_DrawSystemId_DrawDate")
            .IsUnique(); // Unique constraint - zapobiega duplikatom w bazie

        // Indeks dla zapyta� filtruj�cych po DrawTypeId i DrawDate (u�ywane w DrawsGetList)
        builder.HasIndex(e => new { e.DrawTypeId, e.DrawDate })
            .HasDatabaseName("IX_Draws_DrawTypeId_DrawDate");


        builder.HasOne(e => e.DrawType)
                .WithMany(e => e.Draws)
                .HasForeignKey(e => e.DrawTypeId)
                .OnDelete(DeleteBehavior.Restrict);
    }
}