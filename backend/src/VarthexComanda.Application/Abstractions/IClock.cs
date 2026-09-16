namespace VarthexComanda.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
