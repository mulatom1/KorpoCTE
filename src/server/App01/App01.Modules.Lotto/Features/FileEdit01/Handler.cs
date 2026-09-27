using App01.Shared.Infrastructure.Repositories;

using FluentValidation;

using MediatR;

using Microsoft.Extensions.Logging;


namespace App01.Modules.Lotto.Features.FileEdit01;


public class FileEdit01Handler : IRequestHandler<Contracts.Request, Contracts.Response>
{
    private readonly ILogger<FileEdit01Handler> _logger;
    private readonly IValidator<Contracts.Request> _validator;
    private readonly AppDbContext _dbContext;


    public FileEdit01Handler(
        ILogger<FileEdit01Handler> logger,
        IValidator<Contracts.Request> validator,
        AppDbContext dbContext)
    {
        _logger = logger;
        _validator = validator;
        _dbContext = dbContext;
    }

    public async Task<Contracts.Response> Handle(Contracts.Request request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        try
        {
            // 2. Asynchroniczny odczyt wszystkich linii
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TMP", "LOTTO", "EDIT");
            var pathWithFile = Path.Combine(path, request.FileName);

            string[] lines = await File.ReadAllLinesAsync(pathWithFile);
            var modifiedLines = new List<string>(lines.Length);
            bool anyChangeMade = false;

            // 3. Przetwarzanie linii
            foreach (var line in lines)
            {
                // Sprawdzamy czy linia jest wystarczaj¹co d³uga
                if (line.Length > request.Position)
                {
                    char[] chars = line.ToCharArray();

                    // Sprawdzenie czy znak jest inny, ¿eby nie nadpisywaæ bez potrzeby (opcjonalne)
                    if (chars[request.Position] != request.NewChar)
                    {
                        chars[request.Position] = request.NewChar;
                        anyChangeMade = true;
                    }

                    modifiedLines.Add(new string(chars));
                }
                else
                {
                    // Linia za krótka - przepisujemy bez zmian
                    modifiedLines.Add(line);
                }
            }

            // 4. Asynchroniczny zapis (tylko jeœli coœ siê zmieni³o lub chcemy wymusiæ zapis)
            await File.WriteAllLinesAsync(pathWithFile, modifiedLines);

            return new Contracts.Response(anyChangeMade, "OK");
        }
        catch (Exception ex)
        {
            // Obs³uga b³êdów np. brak uprawnieñ do pliku
            return new Contracts.Response(false, $"{ex.Message} {ex.InnerException?.Message}");
        }
    }
}