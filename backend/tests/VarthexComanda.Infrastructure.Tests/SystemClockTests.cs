using VarthexComanda.Infrastructure.Time;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests;

public class SystemClockTests
{
    [Fact]
    public void UtcNow_RetornaHorarioUtcProximoDoAtual()
    {
        var clock = new SystemClock();

        var antes = DateTime.UtcNow;
        var agora = clock.UtcNow;
        var depois = DateTime.UtcNow;

        Assert.Equal(DateTimeKind.Utc, agora.Kind);
        Assert.InRange(agora, antes.AddSeconds(-1), depois.AddSeconds(1));
    }
}
