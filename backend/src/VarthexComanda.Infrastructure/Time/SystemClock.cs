using VarthexComanda.Application.Abstractions;

namespace VarthexComanda.Infrastructure.Time;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
