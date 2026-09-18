using VarthexComanda.Application.Atendimento;
using Xunit;

namespace VarthexComanda.Application.Tests.Atendimento;

public class FusoBrasiliaTests
{
    [Fact]
    public void ParaUtc_MeiaNoiteLocal_RetornaTresHorasUtc()
    {
        var meiaNoiteLocal = new DateTime(2026, 9, 18, 0, 0, 0);

        var utc = FusoBrasilia.ParaUtc(meiaNoiteLocal);

        Assert.Equal(new DateTime(2026, 9, 18, 3, 0, 0), utc);
    }

    [Fact]
    public void ParaLocal_TresHorasUtc_RetornaMeiaNoiteLocal()
    {
        var tresHorasUtc = new DateTime(2026, 9, 18, 3, 0, 0);

        var local = FusoBrasilia.ParaLocal(tresHorasUtc);

        Assert.Equal(new DateTime(2026, 9, 18, 0, 0, 0), local);
    }
}
