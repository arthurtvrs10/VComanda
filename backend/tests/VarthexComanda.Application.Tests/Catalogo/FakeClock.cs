using VarthexComanda.Application.Abstractions;

namespace VarthexComanda.Application.Tests.Catalogo;

public class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
}
