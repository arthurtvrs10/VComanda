using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Domain;

namespace VarthexComanda.Desktop.Atendimento;

public partial class HistoricoViewModel : ObservableObject
{
    private readonly ListarVendasPorData _listarVendasPorData;
    private readonly BuscarItensDaVenda _buscarItensDaVenda;

    private IReadOnlyList<VendaResumo> _vendasCarregadas = Array.Empty<VendaResumo>();

    public HistoricoViewModel(ListarVendasPorData listarVendasPorData, BuscarItensDaVenda buscarItensDaVenda, IClock relogio)
    {
        _listarVendasPorData = listarVendasPorData;
        _buscarItensDaVenda = buscarItensDaVenda;

        Vendas = new ObservableCollection<VendaResumo>();
        ItensDaVendaSelecionada = new ObservableCollection<ItemComanda>();

        DataSelecionada = FusoBrasilia.ParaLocal(relogio.UtcNow).Date;
    }

    public ObservableCollection<VendaResumo> Vendas { get; }
    public ObservableCollection<ItemComanda> ItensDaVendaSelecionada { get; }

    [ObservableProperty]
    private DateTime dataSelecionada;

    [ObservableProperty]
    private string textoBuscaNumero = string.Empty;

    [ObservableProperty]
    private VendaResumo? vendaSelecionada;

    [ObservableProperty]
    private int quantidadeVendas;

    [ObservableProperty]
    private long totalDiaCentavos;

    [ObservableProperty]
    private long ticketMedioCentavos;

    [ObservableProperty]
    private string mensagem = string.Empty;

    partial void OnDataSelecionadaChanged(DateTime value) => AtualizarVendas();

    partial void OnTextoBuscaNumeroChanged(string value) => AplicarFiltro();

    partial void OnVendaSelecionadaChanged(VendaResumo? value)
    {
        ItensDaVendaSelecionada.Clear();
        if (value is null)
        {
            return;
        }

        try
        {
            var itens = _buscarItensDaVenda.Executar(value.Venda.Id);
            if (itens is null)
            {
                return;
            }

            Mensagem = string.Empty;
            foreach (var item in itens)
            {
                ItensDaVendaSelecionada.Add(item);
            }
        }
        catch (Exception)
        {
            Mensagem = "Não foi possível carregar os itens da venda. Tente novamente.";
        }
    }

    public void AtualizarVendas()
    {
        try
        {
            _vendasCarregadas = _listarVendasPorData.Executar(DataSelecionada);
            Mensagem = string.Empty;
        }
        catch (Exception)
        {
            _vendasCarregadas = Array.Empty<VendaResumo>();
            Mensagem = "Não foi possível carregar as vendas. Tente novamente.";
        }
        AplicarFiltro();
    }

    private void AplicarFiltro()
    {
        var filtro = TextoBuscaNumero.Trim();
        var filtradas = string.IsNullOrEmpty(filtro)
            ? _vendasCarregadas
            : _vendasCarregadas.Where(vr => vr.NumeroComanda.ToString().Contains(filtro)).ToList();

        Vendas.Clear();
        foreach (var venda in filtradas)
        {
            Vendas.Add(venda);
        }

        QuantidadeVendas = filtradas.Count;
        TotalDiaCentavos = filtradas.Sum(vr => vr.Venda.TotalCentavos);
        TicketMedioCentavos = QuantidadeVendas == 0 ? 0 : TotalDiaCentavos / QuantidadeVendas;
    }
}
