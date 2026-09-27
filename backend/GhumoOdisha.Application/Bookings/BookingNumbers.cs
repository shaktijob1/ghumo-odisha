using System.Security.Cryptography;
using GhumoOdisha.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Bookings;

/// <summary>
/// Customer-facing booking numbers ("GO-985676"): random, unique, 6 digits. Once every 6-digit
/// number is taken (or free ones have become too rare to hit at random) new bookings move to 7
/// digits, then 8, and so on. The unique index on Bookings.BookingNumber is the final guarantee.
/// </summary>
public static class BookingNumbers
{
    private const int StartDigits = 6;
    private const int MaxDigits = 9; // stays inside int
    private const int AttemptsPerLength = 25;

    public static async Task<int> NextAsync(IGhumoOdishaDbContext db, CancellationToken cancellationToken = default)
    {
        for (var digits = StartDigits; digits <= MaxDigits; digits++)
        {
            var min = (int)Math.Pow(10, digits - 1);
            var max = (int)Math.Pow(10, digits) - 1;

            var used = await db.Bookings.CountAsync(b => b.BookingNumber >= min && b.BookingNumber <= max, cancellationToken);
            if (used >= max - min + 1)
            {
                continue; // every number of this length is taken
            }

            for (var attempt = 0; attempt < AttemptsPerLength; attempt++)
            {
                var candidate = RandomNumberGenerator.GetInt32(min, max + 1);
                if (!await db.Bookings.AnyAsync(b => b.BookingNumber == candidate, cancellationToken))
                {
                    return candidate;
                }
            }
            // Almost full: free numbers are too rare to find at random — move to the next length.
        }

        throw new InvalidOperationException("No booking numbers left.");
    }
}
