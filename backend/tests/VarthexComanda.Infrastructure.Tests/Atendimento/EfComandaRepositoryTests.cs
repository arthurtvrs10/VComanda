using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Persistence.Atendimento;
using VarthexComanda.Infrastructure.Persistence.Catalogo;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Atendimento;

public class EfComandaRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ServiceProvider _provedor;
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabrica;

    public EfComandaRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"varthex-comanda-tests-{Guid.NewGuid()}.db");

        var servicos = new ServiceCollection();
        servicos.AddDbContextFactory<VarthexComandaDbContext>(options =>
            options.UseSqlite($"Data Source={_dbPath};Foreign Keys=True"));
        _provedor = servicos.BuildServiceProvider();
        _fabrica = _provedor.GetRequiredService<IDbContextFactory<VarthexComandaDbContext>>();

        using var contexto = _fabrica.CreateDbContext();
        contexto.Database.Migrate();
    }

    public void Dispose()
    {
        _provedor.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public void AbrirComanda_NumeroLivre_CriaComandaAberta()
    {
        var repositorio = new EfComandaRepository(_fabrica);

        var comanda = repositorio.AbrirComanda(10, DateTime.UtcNow);

        Assert.True(comanda.Id > 0);
        Assert.Equal(StatusComanda.Aberta, comanda.Status);
        Assert.Equal(0, comanda.TotalCentavos);
    }

    [Fact]
    public void AbrirComanda_MesmoNumeroDuasVezes_SegundaLancaExcecao()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        repositorio.AbrirComanda(10, DateTime.UtcNow);

        Assert.Throws<VarthexComanda.Application.Atendimento.NumeroComandaOcupadoException>(
            () => repositorio.AbrirComanda(10, DateTime.UtcNow));
    }

    [Fact]
    public void ListarAbertas_RetornaSomenteAbertasOrdenadasPorNumero()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        repositorio.AbrirComanda(20, DateTime.UtcNow);
        repositorio.AbrirComanda(10, DateTime.UtcNow);

        var abertas = repositorio.ListarAbertas();

        Assert.Equal(new[] { 10, 20 }, abertas.Select(c => c.Numero).ToArray());
    }

    [Fact]
    public void BuscarComItens_ComandaSemItens_RetornaListaVazia()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var comanda = repositorio.AbrirComanda(10, DateTime.UtcNow);

        var detalhe = repositorio.BuscarComItens(comanda.Id);

        Assert.NotNull(detalhe);
        Assert.Empty(detalhe!.Itens);
    }

    [Fact]
    public void BuscarComItens_ComandaInexistente_RetornaNull()
    {
        var repositorio = new EfComandaRepository(_fabrica);

        var detalhe = repositorio.BuscarComItens(999);

        Assert.Null(detalhe);
    }

    private (Produto produto, int comandaId) PrepararComandaEProduto(EfComandaRepository comandas)
    {
        var categoriaRepositorio = new EfCategoriaRepository(_fabrica);
        var agora = DateTime.UtcNow;
        var categoria = categoriaRepositorio.Salvar(new Categoria { Id = 0, Nome = "Bebidas", Ativo = true, CriadoEm = agora, AtualizadoEm = agora });
        var produtoRepositorio = new EfProdutoRepository(_fabrica);
        var produto = produtoRepositorio.Salvar(new Produto { Id = 0, CategoriaId = categoria.Id, Nome = "Refrigerante", PrecoCentavos = 500, Ativo = true, CriadoEm = agora, AtualizadoEm = agora });
        var comanda = comandas.AbrirComanda(10, agora);
        return (produto, comanda.Id);
    }

    [Fact]
    public void AdicionarItem_ProdutoNovo_CriaItemERecalculaTotal()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var (produto, comandaId) = PrepararComandaEProduto(repositorio);

        var detalhe = repositorio.AdicionarItem(comandaId, produto, 2, DateTime.UtcNow);

        Assert.Single(detalhe.Itens);
        Assert.Equal(2, detalhe.Itens[0].Quantidade);
        Assert.Equal(1000, detalhe.Itens[0].SubtotalCentavos);
        Assert.Equal(1000, detalhe.Comanda.TotalCentavos);
    }

    [Fact]
    public void AdicionarItem_MesmoProdutoMesmoPreco_IncrementaQuantidadeEmVezDeDuplicar()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var (produto, comandaId) = PrepararComandaEProduto(repositorio);
        repositorio.AdicionarItem(comandaId, produto, 1, DateTime.UtcNow);

        var detalhe = repositorio.AdicionarItem(comandaId, produto, 1, DateTime.UtcNow);

        Assert.Single(detalhe.Itens);
        Assert.Equal(2, detalhe.Itens[0].Quantidade);
        Assert.Equal(1000, detalhe.Comanda.TotalCentavos);
    }

    [Fact]
    public void AdicionarItem_MesmoProdutoPrecoDiferente_CriaLinhaNova()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var (produto, comandaId) = PrepararComandaEProduto(repositorio);
        repositorio.AdicionarItem(comandaId, produto, 1, DateTime.UtcNow);

        var produtoComNovoPreco = new Produto
        {
            Id = produto.Id, CategoriaId = produto.CategoriaId, Nome = produto.Nome,
            PrecoCentavos = 700, Ativo = true, CriadoEm = produto.CriadoEm, AtualizadoEm = DateTime.UtcNow
        };
        var detalhe = repositorio.AdicionarItem(comandaId, produtoComNovoPreco, 1, DateTime.UtcNow);

        Assert.Equal(2, detalhe.Itens.Count);
        Assert.Equal(500 + 700, detalhe.Comanda.TotalCentavos);
    }

    [Fact]
    public void AlterarQuantidade_ItemExistente_RecalculaSubtotalETotal()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var (produto, comandaId) = PrepararComandaEProduto(repositorio);
        var item = repositorio.AdicionarItem(comandaId, produto, 3, DateTime.UtcNow).Itens[0];

        var detalhe = repositorio.AlterarQuantidade(item.Id, 1, DateTime.UtcNow);

        Assert.Equal(1, detalhe.Itens[0].Quantidade);
        Assert.Equal(500, detalhe.Itens[0].SubtotalCentavos);
        Assert.Equal(500, detalhe.Comanda.TotalCentavos);
    }

    [Fact]
    public void RemoverItem_ItemExistente_RemoveERecalculaTotal()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var (produto, comandaId) = PrepararComandaEProduto(repositorio);
        var item = repositorio.AdicionarItem(comandaId, produto, 1, DateTime.UtcNow).Itens[0];

        var detalhe = repositorio.RemoverItem(item.Id, DateTime.UtcNow);

        Assert.Empty(detalhe.Itens);
        Assert.Equal(0, detalhe.Comanda.TotalCentavos);
    }

    [Fact]
    public void CancelarComanda_ComandaAberta_MarcaCanceladaEDefineFechadaEm()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var comanda = repositorio.AbrirComanda(20, DateTime.UtcNow);

        var cancelada = repositorio.CancelarComanda(comanda.Id, DateTime.UtcNow);

        Assert.Equal(StatusComanda.Cancelada, cancelada.Status);
        Assert.NotNull(cancelada.FechadaEm);
    }

    [Fact]
    public void AdicionarItem_ComandaCancelada_LancaExcecao()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var (produto, comandaId) = PrepararComandaEProduto(repositorio);
        repositorio.CancelarComanda(comandaId, DateTime.UtcNow);

        Assert.Throws<VarthexComanda.Application.Atendimento.ComandaNaoAbertaException>(
            () => repositorio.AdicionarItem(comandaId, produto, 1, DateTime.UtcNow));
    }

    [Fact]
    public void AlterarQuantidade_ComandaCancelada_LancaExcecao()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var (produto, comandaId) = PrepararComandaEProduto(repositorio);
        var item = repositorio.AdicionarItem(comandaId, produto, 1, DateTime.UtcNow).Itens[0];
        repositorio.CancelarComanda(comandaId, DateTime.UtcNow);

        Assert.Throws<VarthexComanda.Application.Atendimento.ComandaNaoAbertaException>(
            () => repositorio.AlterarQuantidade(item.Id, 2, DateTime.UtcNow));
    }

    [Fact]
    public void RemoverItem_ComandaCancelada_LancaExcecao()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var (produto, comandaId) = PrepararComandaEProduto(repositorio);
        var item = repositorio.AdicionarItem(comandaId, produto, 1, DateTime.UtcNow).Itens[0];
        repositorio.CancelarComanda(comandaId, DateTime.UtcNow);

        Assert.Throws<VarthexComanda.Application.Atendimento.ComandaNaoAbertaException>(
            () => repositorio.RemoverItem(item.Id, DateTime.UtcNow));
    }

    [Fact]
    public void CancelarComanda_ComandaJaCancelada_LancaExcecao()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var comanda = repositorio.AbrirComanda(20, DateTime.UtcNow);
        repositorio.CancelarComanda(comanda.Id, DateTime.UtcNow);

        Assert.Throws<VarthexComanda.Application.Atendimento.ComandaNaoAbertaException>(
            () => repositorio.CancelarComanda(comanda.Id, DateTime.UtcNow));
    }
}
