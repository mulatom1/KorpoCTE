using MediatR;


namespace App01.Modules.Lotto.Features.TransformNumbers;

public class TransformNumbersHandler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    public async Task<Contracts.Response> Handle(Contracts.Request request, CancellationToken cancellationToken)
    {
        var sortedNewNumbers = request.Numbers.OrderBy(n => n).ToList();
        var sortedNewSpecials = request.Specials.OrderBy(n => n).ToList();

        var (Low1, High1) = ToMasks(request.Numbers);
        var (Low2, High2) = ToMasks(request.Specials);

        var existingNumbers = FromMasks(request.NumbersLow, request.NumbersHigh);
        var existingSpecials = FromMasks(request.SpecialsLow, request.SpecialsHigh); 


        return await Task.FromResult(new Contracts.Response(
            Low1,
            High1,
            Low2,
            High2,
            sortedNewNumbers,
            sortedNewSpecials
        ));
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
