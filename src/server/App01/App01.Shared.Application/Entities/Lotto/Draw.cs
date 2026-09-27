using System.ComponentModel.DataAnnotations.Schema;


namespace App01.Shared.Application.Entities.Lotto;


public class Draw
{
    public required long Id { get; set; }


    public required long DrawSystemId { get; set; }

    public required DateTime DrawDate { get; set; }

    public required int DrawTypeId { get; set; }
    public virtual DrawType DrawType { get; set; } = null!;

    public long NumbersLow { get; set; } = default!;
    public long NumbersHigh { get; set; } = default!;
    public long SpecialsLow { get; set; } = default!;
    public long SpecialsHigh { get; set; } = default!;


    [NotMapped]
    public List<int> Numbers
    {
        get => FromMasks(NumbersLow, NumbersHigh);
        set
        {
            var (Low, High) = ToMasks(value);
            NumbersLow = Low;
            NumbersHigh = High;
        }
    }

    [NotMapped]
    public List<int> Specials
    {
        get => FromMasks(SpecialsLow, SpecialsHigh);
        set
        {
            var (Low, High) = ToMasks(value);
            SpecialsLow = Low;
            SpecialsHigh = High;
        }
    }

    private static (long Low, long High) ToMasks(IEnumerable<int> numbers)
    {
        long low = 0;
        long high = 0;

        if (numbers == null) return (0, 0);

        foreach (var num in numbers)
        {
            if (num < 1) continue; // Ignorujemy 0 i ujemne

            if (num <= 64)
            {
                // Przesuniêcie dla liczb 1-64 (Bit 0 to liczba 1)
                low |= (1L << (num - 1));
            }
            else if (num <= 128)
            {
                // Przesuniêcie dla liczb 65-128 (Bit 0 to liczba 65)
                high |= (1L << (num - 65));
            }
            // Powy¿ej 128 nie obs³ugujemy w 2 kolumnach (rzadki przypadek)
        }

        return (low, high);
    }

    private static List<int> FromMasks(long low, long high)
    {
        var numbers = new List<int>();

        // Dekodowanie LOW (1-64)
        for (int i = 0; i < 64; i++)
        {
            // Sprawdzamy czy bit 'i' jest ustawiony
            if ((low & (1L << i)) != 0)
            {
                numbers.Add(i + 1);
            }
        }

        // Dekodowanie HIGH (65-128) - tylko jeœli maska nie jest pusta
        if (high != 0)
        {
            for (int i = 0; i < 64; i++)
            {
                if ((high & (1L << i)) != 0)
                {
                    numbers.Add(i + 65);
                }
            }
        }

        return numbers;
    }
}