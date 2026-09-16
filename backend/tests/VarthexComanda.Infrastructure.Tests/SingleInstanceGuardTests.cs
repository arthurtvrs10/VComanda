using VarthexComanda.Infrastructure.Concurrency;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests;

public class SingleInstanceGuardTests
{
    [Fact]
    public void TryAcquire_SegundaGuardaComMesmoNome_FalhaAteAPrimeiraLiberar()
    {
        var nomeMutex = "VarthexComandaTests_" + Guid.NewGuid();
        using var primeira = new SingleInstanceGuard(nomeMutex);
        using var segunda = new SingleInstanceGuard(nomeMutex);

        Assert.True(primeira.TryAcquire());
        Assert.False(segunda.TryAcquire());

        primeira.Release();

        using var terceira = new SingleInstanceGuard(nomeMutex);
        Assert.True(terceira.TryAcquire());
        terceira.Release();
    }
}
