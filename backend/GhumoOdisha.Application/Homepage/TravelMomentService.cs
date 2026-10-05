using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Homepage;

public class TravelMomentService(IGhumoOdishaDbContext db, IImageStorage imageStorage) : ITravelMomentService
{
    public async Task<IReadOnlyList<TravelMomentDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.TravelMoments.AsNoTracking()
            .OrderBy(m => m.DisplayOrder).ThenBy(m => m.TravelMomentId)
            .Select(m => new TravelMomentDto(m.TravelMomentId, m.ImageUrl, m.Caption, m.DisplayOrder))
            .ToListAsync(cancellationToken);

    public async Task<TravelMomentDto> AddAsync(UploadedImage image, string? caption, CancellationToken cancellationToken = default)
    {
        UploadedImageRules.Validate(image);
        var cleanCaption = UploadedImageRules.CleanCaption(caption);

        var count = await db.TravelMoments.CountAsync(cancellationToken);
        if (count >= ITravelMomentService.MaxPhotos)
        {
            throw new ValidationAppException([$"Real Travel Moments holds up to {ITravelMomentService.MaxPhotos} photos. Delete one to add another."]);
        }

        var lastOrder = await db.TravelMoments.MaxAsync(m => (int?)m.DisplayOrder, cancellationToken) ?? -1;
        var url = await imageStorage.SaveAsync(image.Content, image.FileName, image.ContentType, "moments", cancellationToken);

        var moment = new TravelMoment
        {
            ImageUrl = url,
            Caption = cleanCaption,
            DisplayOrder = lastOrder + 1,
            CreatedAt = DateTime.UtcNow,
        };
        db.TravelMoments.Add(moment);
        await db.SaveChangesAsync(cancellationToken);

        return new TravelMomentDto(moment.TravelMomentId, moment.ImageUrl, moment.Caption, moment.DisplayOrder);
    }

    public async Task UpdateCaptionAsync(int travelMomentId, string? caption, CancellationToken cancellationToken = default)
    {
        var moment = await FindAsync(travelMomentId, cancellationToken);
        moment.Caption = UploadedImageRules.CleanCaption(caption);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MoveAsync(int travelMomentId, int direction, CancellationToken cancellationToken = default)
    {
        if (direction is not (-1 or 1))
        {
            throw new ValidationAppException(["Direction must be -1 or 1."]);
        }

        var all = await db.TravelMoments.OrderBy(m => m.DisplayOrder).ThenBy(m => m.TravelMomentId).ToListAsync(cancellationToken);
        var index = all.FindIndex(m => m.TravelMomentId == travelMomentId);
        if (index < 0)
        {
            throw new NotFoundException("Photo not found.");
        }

        var target = index + direction;
        if (target < 0 || target >= all.Count)
        {
            return;
        }

        (all[index], all[target]) = (all[target], all[index]);
        for (var i = 0; i < all.Count; i++)
        {
            all[i].DisplayOrder = i;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int travelMomentId, CancellationToken cancellationToken = default)
    {
        var moment = await FindAsync(travelMomentId, cancellationToken);
        db.TravelMoments.Remove(moment);
        await db.SaveChangesAsync(cancellationToken);
        imageStorage.Delete(moment.ImageUrl);
    }

    private async Task<TravelMoment> FindAsync(int travelMomentId, CancellationToken cancellationToken) =>
        await db.TravelMoments.FirstOrDefaultAsync(m => m.TravelMomentId == travelMomentId, cancellationToken)
            ?? throw new NotFoundException("Photo not found.");
}
