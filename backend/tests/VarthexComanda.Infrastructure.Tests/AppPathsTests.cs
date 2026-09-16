using VarthexComanda.Infrastructure.Storage;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests;

public class AppPathsTests
{
    [Fact]
    public void EnsureCreated_CriaAsTresPastasSobOTerritorioIndicado()
    {
        var root = Path.Combine(Path.GetTempPath(), "VarthexComandaTests_" + Guid.NewGuid());
        try
        {
            var paths = new AppPaths(root);

            paths.EnsureCreated();

            Assert.True(Directory.Exists(paths.DataDirectory));
            Assert.True(Directory.Exists(paths.LogsDirectory));
            Assert.True(Directory.Exists(paths.BackupsDirectory));
            Assert.Equal(Path.Combine(paths.DataDirectory, "varthex-comanda.db"), paths.DatabasePath);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
