using GhumoOdisha.Application.Common;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Cars;

/// <summary>Who did something in the Cars module — "Customer", "Driver", "Admin" or "System" plus their id.</summary>
public record CarActor(string Role, int? Id)
{
    public static CarActor Admin(int adminId) => new("Admin", adminId);
    public static CarActor Driver(int driverId) => new("Driver", driverId);
    public static CarActor Customer(int customerId) => new("Customer", customerId);
    public static readonly CarActor System = new("System", null);
}

/// <summary>Adds a <see cref="CarAuditEvent"/> to the context; saved with the caller's own SaveChanges, so the change and its history commit together.</summary>
public static class CarAudit
{
    public static void Record(
        IGhumoOdishaDbContext db,
        CarAuditEntity entityType,
        int entityId,
        string action,
        string title,
        CarActor actor,
        string? oldValue = null,
        string? newValue = null,
        string? note = null,
        int? carBookingId = null,
        bool visibleToCustomer = false)
    {
        db.CarAuditEvents.Add(new CarAuditEvent
        {
            EntityType = entityType,
            EntityId = entityId,
            CarBookingId = carBookingId,
            Action = action,
            Title = Truncate(title, 160)!,
            OldValue = Truncate(oldValue, 2000),
            NewValue = Truncate(newValue, 2000),
            Note = Truncate(note, 500),
            ActorRole = actor.Role,
            ActorId = actor.Id,
            IsVisibleToCustomer = visibleToCustomer,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
