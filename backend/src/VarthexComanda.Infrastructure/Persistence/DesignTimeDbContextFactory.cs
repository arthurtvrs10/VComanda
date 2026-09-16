using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VarthexComanda.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VarthexComandaDbContext>
{
    public VarthexComandaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<VarthexComandaDbContext>()
            .UseSqlite("Data Source=varthex-comanda.design.db")
            .Options;
        return new VarthexComandaDbContext(options);
    }
}
