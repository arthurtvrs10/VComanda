using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Desktop.Catalogo;

public partial class ProdutosViewModel : ObservableObject
{
    private static readonly CultureInfo CulturaMoeda = CultureInfo.GetCultureInfo("pt-BR");

    private readonly ListarCategoriasAtivas _listarCategoriasAtivas;
    private readonly CadastrarCategoria _cadastrarCategoria;
    private readonly PesquisarProdutos _pesquisarProdutos;
    private readonly CadastrarProduto _cadastrarProduto;
    private readonly AlterarProduto _alterarProduto;
    private readonly DesativarProduto _desativarProduto;

    public ProdutosViewModel(
        ListarCategoriasAtivas listarCategoriasAtivas,
        CadastrarCategoria cadastrarCategoria,
        PesquisarProdutos pesquisarProdutos,
        CadastrarProduto cadastrarProduto,
        AlterarProduto alterarProduto,
        DesativarProduto desativarProduto)
    {
        _listarCategoriasAtivas = listarCategoriasAtivas;
        _cadastrarCategoria = cadastrarCategoria;
        _pesquisarProdutos = pesquisarProdutos;
        _cadastrarProduto = cadastrarProduto;
        _alterarProduto = alterarProduto;
        _desativarProduto = desativarProduto;

        Categorias = new ObservableCollection<Categoria>();
        Produtos = new ObservableCollection<Produto>();

        CarregarCategorias();
        Pesquisar();
    }

    public ObservableCollection<Categoria> Categorias { get; }
    public ObservableCollection<Produto> Produtos { get; }

    [ObservableProperty]
    private Categoria? categoriaFiltro;

    [ObservableProperty]
    private string textoBusca = string.Empty;

    [ObservableProperty]
    private Produto? produtoSelecionado;

    [ObservableProperty]
    private string nomeProduto = string.Empty;

    [ObservableProperty]
    private Categoria? categoriaProduto;

    [ObservableProperty]
    private string precoProdutoReais = string.Empty;

    [ObservableProperty]
    private bool produtoAtivo = true;

    [ObservableProperty]
    private string novaCategoriaNome = string.Empty;

    [ObservableProperty]
    private string mensagem = string.Empty;

    partial void OnCategoriaFiltroChanged(Categoria? value) => Pesquisar();

    partial void OnTextoBuscaChanged(string value) => Pesquisar();

    partial void OnProdutoSelecionadoChanged(Produto? value)
    {
        if (value is null)
        {
            NomeProduto = string.Empty;
            CategoriaProduto = null;
            PrecoProdutoReais = string.Empty;
            ProdutoAtivo = true;
            Mensagem = string.Empty;
            return;
        }

        NomeProduto = value.Nome;
        CategoriaProduto = Categorias.FirstOrDefault(c => c.Id == value.CategoriaId);
        PrecoProdutoReais = (value.PrecoCentavos / 100m).ToString("0.00", CulturaMoeda);
        ProdutoAtivo = value.Ativo;
        Mensagem = string.Empty;
    }

    [RelayCommand]
    private void Pesquisar()
    {
        var resultado = _pesquisarProdutos.Executar(CategoriaFiltro?.Id, TextoBusca);
        Produtos.Clear();
        foreach (var produto in resultado)
        {
            Produtos.Add(produto);
        }
    }

    [RelayCommand]
    private void LimparFiltro()
    {
        CategoriaFiltro = null;
    }

    [RelayCommand]
    private void Novo()
    {
        ProdutoSelecionado = null;
        NomeProduto = string.Empty;
        CategoriaProduto = null;
        PrecoProdutoReais = string.Empty;
        ProdutoAtivo = true;
        Mensagem = string.Empty;
    }

    [RelayCommand]
    private void Salvar()
    {
        if (CategoriaProduto is null)
        {
            Mensagem = "Selecione uma categoria.";
            return;
        }
        if (!TentarConverterPreco(PrecoProdutoReais, out var precoCentavos))
        {
            Mensagem = "Informe um preço válido.";
            return;
        }

        try
        {
            var resultado = ProdutoSelecionado is null
                ? _cadastrarProduto.Executar(NomeProduto, CategoriaProduto.Id, precoCentavos)
                : _alterarProduto.Executar(ProdutoSelecionado.Id, NomeProduto, CategoriaProduto.Id, precoCentavos, ProdutoAtivo);

            if (!resultado.Sucesso)
            {
                Mensagem = string.Join(" ", resultado.Erros);
                return;
            }

            Novo();
            Pesquisar();
        }
        catch (Exception)
        {
            Mensagem = "Não foi possível salvar o produto. Tente novamente.";
        }
    }

    [RelayCommand]
    private void Desativar()
    {
        if (ProdutoSelecionado is null)
        {
            Mensagem = "Selecione um produto para desativar.";
            return;
        }

        try
        {
            var resultado = _desativarProduto.Executar(ProdutoSelecionado.Id);
            if (!resultado.Sucesso)
            {
                Mensagem = string.Join(" ", resultado.Erros);
                return;
            }

            Novo();
            Pesquisar();
        }
        catch (Exception)
        {
            Mensagem = "Não foi possível desativar o produto. Tente novamente.";
        }
    }

    [RelayCommand]
    private void AdicionarCategoria()
    {
        try
        {
            var resultado = _cadastrarCategoria.Executar(NovaCategoriaNome);
            if (!resultado.Sucesso)
            {
                Mensagem = string.Join(" ", resultado.Erros);
                return;
            }

            NovaCategoriaNome = string.Empty;
            Mensagem = string.Empty;
            CarregarCategorias();
        }
        catch (Exception)
        {
            Mensagem = "Não foi possível adicionar a categoria. Tente novamente.";
        }
    }

    private void CarregarCategorias()
    {
        var categoriaSelecionadaId = CategoriaProduto?.Id;
        Categorias.Clear();
        foreach (var categoria in _listarCategoriasAtivas.Executar())
        {
            Categorias.Add(categoria);
        }
        if (categoriaSelecionadaId is not null)
        {
            CategoriaProduto = Categorias.FirstOrDefault(c => c.Id == categoriaSelecionadaId);
        }
    }

    private static bool TentarConverterPreco(string texto, out long precoCentavos)
    {
        precoCentavos = 0;
        const NumberStyles estilo = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;
        if (!decimal.TryParse(texto, estilo, CulturaMoeda, out var valor))
        {
            return false;
        }
        if (valor <= 0)
        {
            return false;
        }
        precoCentavos = (long)Math.Round(valor * 100m, MidpointRounding.AwayFromZero);
        return true;
    }
}
