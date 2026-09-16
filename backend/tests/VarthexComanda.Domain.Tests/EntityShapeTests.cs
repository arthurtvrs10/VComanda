using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Domain.Tests;

public class EntityShapeTests
{
    [Fact]
    public void Comanda_AbertaSemFechadaEm_ExpoeCamposObrigatorios()
    {
        var comanda = new Comanda
        {
            Id = 1,
            Numero = 42,
            Status = StatusComanda.Aberta,
            AbertaEm = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc),
            FechadaEm = null,
            TotalCentavos = 0,
            Observacao = null
        };

        Assert.Equal(42, comanda.Numero);
        Assert.Equal(StatusComanda.Aberta, comanda.Status);
        Assert.Null(comanda.FechadaEm);
    }

    [Fact]
    public void ItemComanda_ExpoeSnapshotDeNomeEPreco()
    {
        var item = new ItemComanda
        {
            Id = 1,
            ComandaId = 1,
            ProdutoId = 7,
            NomeProduto = "Refrigerante",
            PrecoUnitarioCentavos = 500,
            Quantidade = 2,
            SubtotalCentavos = 1000,
            Observacao = null,
            CriadoEm = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow
        };

        Assert.Equal("Refrigerante", item.NomeProduto);
        Assert.Equal(1000, item.SubtotalCentavos);
    }

    [Fact]
    public void Venda_VinculaComandaENumeroUnico()
    {
        var venda = new Venda
        {
            Id = 1,
            ComandaId = 5,
            Numero = 5,
            TotalCentavos = 5400,
            FinalizadaEm = DateTime.UtcNow,
            Status = StatusVenda.Concluida
        };

        Assert.Equal(StatusVenda.Concluida, venda.Status);
        Assert.Equal(5400, venda.TotalCentavos);
    }

    [Fact]
    public void BackupRegistro_RegistraSucessoOuFalha()
    {
        var registro = new BackupRegistro
        {
            Id = 1,
            Arquivo = "varthex-comanda-2026-09-16.db",
            Destino = "D:\\backups",
            CriadoEm = DateTime.UtcNow,
            Status = StatusBackup.Sucesso,
            Checksum = "abc123",
            Mensagem = null
        };

        Assert.Equal(StatusBackup.Sucesso, registro.Status);
    }
}
