namespace VarthexComanda.Application.Atendimento;

public class ListarVendasPorData
{
    private readonly IVendaRepository _vendas;

    public ListarVendasPorData(IVendaRepository vendas)
    {
        _vendas = vendas;
    }

    public IReadOnlyList<VendaResumo> Executar(DateTime dataLocal)
    {
        var inicioLocal = dataLocal.Date;
        var fimLocal = inicioLocal.AddDays(1);
        var inicioUtc = FusoBrasilia.ParaUtc(inicioLocal);
        var fimUtc = FusoBrasilia.ParaUtc(fimLocal);
        return _vendas.ListarPorData(inicioUtc, fimUtc);
    }
}
