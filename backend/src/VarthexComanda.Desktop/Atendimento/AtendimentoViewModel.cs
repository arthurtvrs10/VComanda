using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Desktop.Atendimento;

public partial class AtendimentoViewModel : ObservableObject
{
    private readonly AbrirComanda _abrirComanda;
    private readonly AdicionarItem _adicionarItem;
    private readonly AlterarQuantidade _alterarQuantidade;
    private readonly RemoverItem _removerItem;
    private readonly CancelarComanda _cancelarComanda;
    private readonly IComandaRepository _comandas;
    private readonly ListarCategoriasAtivas _listarCategoriasAtivas;
    private readonly PesquisarProdutos _pesquisarProdutos;

    public AtendimentoViewModel(
        AbrirComanda abrirComanda,
        AdicionarItem adicionarItem,
        AlterarQuantidade alterarQuantidade,
        RemoverItem removerItem,
        CancelarComanda cancelarComanda,
        IComandaRepository comandas,
        ListarCategoriasAtivas listarCategoriasAtivas,
        PesquisarProdutos pesquisarProdutos)
    {
        _abrirComanda = abrirComanda;
        _adicionarItem = adicionarItem;
        _alterarQuantidade = alterarQuantidade;
        _removerItem = removerItem;
        _cancelarComanda = cancelarComanda;
        _comandas = comandas;
        _listarCategoriasAtivas = listarCategoriasAtivas;
        _pesquisarProdutos = pesquisarProdutos;

        ComandasAbertas = new ObservableCollection<Comanda>();
        Categorias = new ObservableCollection<Categoria>();
        Itens = new ObservableCollection<ItemComanda>();
        ProdutosCatalogo = new ObservableCollection<Produto>();

        AtualizarComandasAbertas();
        CarregarCategorias();
    }

    public ObservableCollection<Comanda> ComandasAbertas { get; }
    public ObservableCollection<Categoria> Categorias { get; }
    public ObservableCollection<ItemComanda> Itens { get; }
    public ObservableCollection<Produto> ProdutosCatalogo { get; }

    [ObservableProperty]
    private string novoNumero = string.Empty;

    [ObservableProperty]
    private Comanda? comandaAtual;

    [ObservableProperty]
    private Categoria? categoriaCatalogo;

    [ObservableProperty]
    private string textoBuscaCatalogo = string.Empty;

    [ObservableProperty]
    private string mensagem = string.Empty;

    partial void OnCategoriaCatalogoChanged(Categoria? value) => PesquisarCatalogo();

    partial void OnTextoBuscaCatalogoChanged(string value) => PesquisarCatalogo();

    [RelayCommand]
    private void PesquisarCatalogo()
    {
        var resultado = _pesquisarProdutos.Executar(CategoriaCatalogo?.Id, TextoBuscaCatalogo);
        ProdutosCatalogo.Clear();
        foreach (var produto in resultado.Where(p => p.Ativo))
        {
            ProdutosCatalogo.Add(produto);
        }
    }

    [RelayCommand]
    private void Abrir()
    {
        if (!int.TryParse(NovoNumero, out var numero))
        {
            Mensagem = "Informe um número de comanda válido.";
            return;
        }

        var resultado = _abrirComanda.Executar(numero);
        if (!resultado.Sucesso)
        {
            Mensagem = string.Join(" ", resultado.Erros);
            return;
        }

        NovoNumero = string.Empty;
        Mensagem = string.Empty;
        AtualizarComandasAbertas();
        AbrirParaEdicao(resultado.Valor!.Id);
    }

    [RelayCommand]
    private void SelecionarComanda(Comanda comanda) => AbrirParaEdicao(comanda.Id);

    private void AbrirParaEdicao(int comandaId)
    {
        var detalhe = _comandas.BuscarComItens(comandaId);
        if (detalhe is null)
        {
            Mensagem = "Comanda não encontrada.";
            return;
        }

        ComandaAtual = detalhe.Comanda;
        Itens.Clear();
        foreach (var item in detalhe.Itens)
        {
            Itens.Add(item);
        }
    }

    [RelayCommand]
    private void FecharEdicao()
    {
        ComandaAtual = null;
        Itens.Clear();
    }

    [RelayCommand]
    private void AdicionarProdutoAoItem(Produto produto)
    {
        if (ComandaAtual is null)
        {
            return;
        }

        AplicarResultado(_adicionarItem.Executar(ComandaAtual.Id, produto.Id, 1));
    }

    [RelayCommand]
    private void AumentarQuantidade(ItemComanda item) =>
        AplicarResultado(_alterarQuantidade.Executar(item.Id, item.Quantidade + 1));

    [RelayCommand]
    private void DiminuirQuantidade(ItemComanda item)
    {
        if (item.Quantidade <= 1)
        {
            AplicarResultado(_removerItem.Executar(item.Id));
            return;
        }

        AplicarResultado(_alterarQuantidade.Executar(item.Id, item.Quantidade - 1));
    }

    [RelayCommand]
    private void Remover(ItemComanda item) => AplicarResultado(_removerItem.Executar(item.Id));

    [RelayCommand]
    private void CancelarComandaAtual()
    {
        if (ComandaAtual is null)
        {
            return;
        }

        var resultado = _cancelarComanda.Executar(ComandaAtual.Id);
        if (!resultado.Sucesso)
        {
            Mensagem = string.Join(" ", resultado.Erros);
            return;
        }

        Mensagem = string.Empty;
        FecharEdicao();
        AtualizarComandasAbertas();
    }

    private void AplicarResultado(Resultado<ComandaComItens> resultado)
    {
        if (!resultado.Sucesso)
        {
            Mensagem = string.Join(" ", resultado.Erros);
            return;
        }

        Mensagem = string.Empty;
        ComandaAtual = resultado.Valor!.Comanda;
        Itens.Clear();
        foreach (var item in resultado.Valor.Itens)
        {
            Itens.Add(item);
        }
        AtualizarComandasAbertas();
    }

    private void AtualizarComandasAbertas()
    {
        ComandasAbertas.Clear();
        foreach (var comanda in _comandas.ListarAbertas())
        {
            ComandasAbertas.Add(comanda);
        }
    }

    private void CarregarCategorias()
    {
        Categorias.Clear();
        foreach (var categoria in _listarCategoriasAtivas.Executar())
        {
            Categorias.Add(categoria);
        }
        PesquisarCatalogo();
    }
}
