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
        Assert.True(primeira.TryAcquire());

        var segundaConseguiu = Task.Run(() =>
        {
            using var segunda = new SingleInstanceGuard(nomeMutex);
            return segunda.TryAcquire();
        }).Result;
        Assert.False(segundaConseguiu);

        primeira.Release();

        var terceiraConseguiu = Task.Run(() =>
        {
            using var terceira = new SingleInstanceGuard(nomeMutex);
            var conseguiu = terceira.TryAcquire();
            if (conseguiu) terceira.Release();
            return conseguiu;
        }).Result;
        Assert.True(terceiraConseguiu);
    }
}
