using Microsoft.EntityFrameworkCore;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence;

public class VarthexComandaDbContext : DbContext
{
    public VarthexComandaDbContext(DbContextOptions<VarthexComandaDbContext> options)
        : base(options)
    {
    }

    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Comanda> Comandas => Set<Comanda>();
    public DbSet<ItemComanda> ItensComanda => Set<ItemComanda>();
    public DbSet<Venda> Vendas => Set<Venda>();
    public DbSet<Configuracao> Configuracoes => Set<Configuracao>();
    public DbSet<BackupRegistro> BackupRegistros => Set<BackupRegistro>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VarthexComandaDbContext).Assembly);
    }
}
