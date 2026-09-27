using System.Globalization;
using System.Reflection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Moq;


namespace App01.Api.Tests.Workers;


/// <summary>
/// Tests for CSV parsing logic in LottoWorker03, specifically for MultiMulti (type 9)
/// where special=0 means "no Plus" and should result in empty Specials list.
/// </summary>
public class LottoWorker03CsvParsingTests
{
    /// <summary>
    /// Test helper that simulates CSV line parsing logic from LottoWorker03.
    /// This is a copy of the parsing logic to test it in isolation.
    /// </summary>
    private static (List<int> Numbers, List<int> Specials) ParseCsvLineForMultiMulti(string line, int specialsCount)
    {
        const int drawTypeId = 9; // MultiMulti
        var separator = ',';
        var parts = line.Split(separator);

        var numbers = new List<int>();
        var specials = new List<int>();

        // Skip DrawSystemId (parts[0]) and DrawDate (parts[1])
        for (int i = 2; i < parts.Length; i++)
        {
            var value = parts[i].Trim();
            if (string.IsNullOrEmpty(value))
                continue;

            if (int.TryParse(value, out var num))
            {
                numbers.Add(num);
            }
        }

        // Move last N numbers to specials based on SpecialsCount (same logic as LottoWorker03)
        if (specialsCount > 0 && specials.Count == 0 && numbers.Count > specialsCount)
        {
            for (int i = 0; i < specialsCount; i++)
            {
                var specialValue = numbers[numbers.Count - specialsCount + i];
                // MultiMulti (DrawTypeId = 9): value 0 means "no Plus" - skip adding to specials
                if (drawTypeId == 9 && specialValue == 0)
                    continue;
                specials.Add(specialValue);
            }
            numbers.RemoveRange(numbers.Count - specialsCount, specialsCount);
        }

        return (numbers, specials);
    }

    [Fact]
    public void MultiMulti_CsvWithZeroSpecial_ShouldHaveEmptySpecials()
    {
        // Arrange - CSV line with 20 numbers + special = 0 (no Plus)
        // Old data from 1996 where Plus didn't exist
        var csvLine = "00001,18.03.1996 22:00,78,76,73,68,66,62,48,42,41,35,34,27,26,23,22,21,16,10,09,04,00";
        var specialsCount = 1; // MultiMulti has 1 special

        // Act
        var (numbers, specials) = ParseCsvLineForMultiMulti(csvLine, specialsCount);

        // Assert
        Assert.Equal(20, numbers.Count); // Should have exactly 20 numbers
        Assert.Empty(specials); // Should be EMPTY because special was 0 (no Plus)

        // Verify numbers are correct (sorted: 4, 9, 10, 16, 21, 22, 23, 26, 27, 34, 35, 41, 42, 48, 62, 66, 68, 73, 76, 78)
        Assert.Contains(78, numbers);
        Assert.Contains(4, numbers);
        Assert.DoesNotContain(0, numbers); // 0 should NOT be in numbers - it was the special marker
    }

    [Fact]
    public void MultiMulti_CsvWithNonZeroSpecial_ShouldHaveSpecialNumber()
    {
        // Arrange - CSV line with 20 numbers + special = 65 (Plus number)
        // New data from 2026 where Plus exists
        var csvLine = "16551,20.01.2026 22:00,62,80,12,58,54,34,68,74,72,67,14,46,37,20,75,26,50,16,59,65,65";
        var specialsCount = 1; // MultiMulti has 1 special

        // Act
        var (numbers, specials) = ParseCsvLineForMultiMulti(csvLine, specialsCount);

        // Assert
        Assert.Equal(20, numbers.Count); // Should have exactly 20 numbers
        Assert.Single(specials); // Should have exactly 1 special
        Assert.Equal(65, specials[0]); // Special should be 65 (Plus number)

        // Verify 65 appears in numbers (it's one of the drawn numbers that was also Plus)
        Assert.Contains(65, numbers);
    }

    [Fact]
    public void MultiMulti_CsvWithSpecial27_ShouldHaveCorrectSpecial()
    {
        // Arrange - another example with Plus = 27
        var csvLine = "16564,27.01.2026 14:00,31,71,52,51,59,76,12,48,73,57,23,07,37,49,02,36,19,68,24,27,27";
        var specialsCount = 1;

        // Act
        var (numbers, specials) = ParseCsvLineForMultiMulti(csvLine, specialsCount);

        // Assert
        Assert.Equal(20, numbers.Count);
        Assert.Single(specials);
        Assert.Equal(27, specials[0]); // Plus = 27
        Assert.Contains(27, numbers); // 27 is also in the drawn numbers
    }

    [Fact]
    public void MultiMulti_MultipleLines_MixedSpecials()
    {
        // Arrange - test both cases
        var lines = new[]
        {
            "00001,18.03.1996 22:00,78,76,73,68,66,62,48,42,41,35,34,27,26,23,22,21,16,10,09,04,00", // no Plus
            "16551,20.01.2026 22:00,62,80,12,58,54,34,68,74,72,67,14,46,37,20,75,26,50,16,59,65,65", // Plus = 65
            "00002,19.03.1996 22:00,77,75,71,69,67,64,63,62,59,49,48,44,39,35,33,28,19,15,12,06,00", // no Plus
        };
        var specialsCount = 1;

        // Act & Assert
        var (numbers1, specials1) = ParseCsvLineForMultiMulti(lines[0], specialsCount);
        Assert.Equal(20, numbers1.Count);
        Assert.Empty(specials1); // no Plus (special = 0)

        var (numbers2, specials2) = ParseCsvLineForMultiMulti(lines[1], specialsCount);
        Assert.Equal(20, numbers2.Count);
        Assert.Single(specials2);
        Assert.Equal(65, specials2[0]); // Plus = 65

        var (numbers3, specials3) = ParseCsvLineForMultiMulti(lines[2], specialsCount);
        Assert.Equal(20, numbers3.Count);
        Assert.Empty(specials3); // no Plus (special = 0)
    }
}