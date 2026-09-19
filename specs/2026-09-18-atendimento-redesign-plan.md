# Redesign da Tela de Atendimento Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Substituir a tela de Atendimento atual (grade dinâmica só com
comandas abertas + edição por dropdown) pela tela redesenhada e aprovada
(grade fixa de N slots numerados, tela de comanda em duas colunas
menu+carrinho em tabela), reaproveitando toda a lógica de negócio já
existente e testada.

**Architecture:** Quase toda a lógica já existe (`AbrirComanda`,
`AdicionarItem`, `AlterarQuantidade`, `RemoverItem`, `CancelarComanda`,
`EncerrarComanda` via `IEncerramentoDialog`) — esta fatia adiciona só uma
peça nova no ViewModel (a grade de slots, derivada em memória de
`ComandasAbertas` + o tamanho configurado) e reescreve o XAML da tela.

**Tech Stack:** C#/.NET 10, WPF (CommunityToolkit.Mvvm), xUnit.

**Spec:** [specs/2026-09-18-atendimento-redesign-design.md](../specs/2026-09-18-atendimento-redesign-design.md)

## Global Constraints

- Nenhuma mudança de assinatura nos use cases já existentes
  (`AdicionarItem`, `AlterarQuantidade`, `RemoverItem`, `CancelarComanda`,
  `EncerrarComanda`, `AbrirComanda`) — todos reaproveitados como estão.
- `ComandaSlotItem` é um tipo novo só na camada Desktop
  (`VarthexComanda.Desktop.Atendimento`) — não é entidade de domínio, não
  tem tabela, é recalculado em memória a partir de `ComandasAbertas` +
  `ObterConfiguracao().QuantidadeMaximaComandas ?? 20`.
- O card compacto de cada slot mostra só o total (`Comanda.TotalCentavos`,
  já existe) — **não** mostra quantidade de itens (exigiria consulta
  extra por comanda, fora de escopo).
- O "tempo" do slot é **"aberta há X"**, calculado como
  `_relogio.UtcNow - comanda.AbertaEm` (diferença de UTC direta — **não**
  precisa de `FusoBrasilia`, já que é uma duração/diferença entre dois
  instantes UTC, não uma exibição de horário absoluto).
- Fotos de produto: ícone placeholder genérico para todo produto — sem
  mudança de schema em `Produto` nesta fatia.
- F4 é o único atalho de teclado desta fatia (liga a
  `VerTotalCommand`, que já existe e já abre o diálogo de Encerramento
  existente) — não implementa navegação por teclado ampla (RNF18 continua
  fatia futura).
- **Decisão documentada:** a busca textual por produto
  (`TextoBuscaCatalogo`/`PesquisarCatalogoCommand`/`LimparFiltroCommand`)
  não aparece na tela redesenhada — o mockup aprovado navega só por
  categoria (abas), sem campo de busca. Esses três membros continuam
  existindo em `AtendimentoViewModel` (não são removidos, evitando uma
  mudança de escopo maior nesta fatia), só deixam de ser referenciados
  pelo XAML novo. Isso é intencional, não uma omissão — sinalizar
  explicitamente pra um revisor não tratar como código morto a remover.
- Prefixar todo comando `dotnet` com o PATH do SDK: `export
  PATH="$PATH:/c/Program Files/dotnet" && dotnet ...` (Git Bash).

---

## Task 1: `ComandaSlotItem` e grade de slots em `AtendimentoViewModel`

**Files:**
- Create: `backend/src/VarthexComanda.Desktop/Atendimento/ComandaSlotItem.cs`
- Modify: `backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoViewModel.cs`
- Modify: `backend/tests/VarthexComanda.Desktop.Tests/Atendimento/AtendimentoViewModelTests.cs`

**Interfaces:**
- Consumes: `ObterConfiguracao`/`ConfiguracaoEstabelecimento.QuantidadeMaximaComandas`
  (`VarthexComanda.Application.Configuracao`, já existe), `IClock`
  (`VarthexComanda.Application.Abstractions`, já existe),
  `CentavosParaMoedaConverter.Formatar(long)` (já existe), `Comanda`
  (`Numero`, `AbertaEm`, `TotalCentavos`, já existem).
- Produces: `ComandaSlotItem { int Numero; bool Aberta; int? ComandaId;
  string TotalFormatado; string TempoFormatado; }`,
  `AtendimentoViewModel.Slots` (`ObservableCollection<ComandaSlotItem>`),
  `AtendimentoViewModel.AbrirOuSelecionarSlotCommand` (recebe um
  `ComandaSlotItem`), `AtendimentoViewModel.AtualizarComandasAbertas()`
  (agora público) — consumidos pela Task 2 (XAML) e pela Task 2's edição
  de `MainWindow.xaml.cs`.

- [ ] **Step 1: Criar `ComandaSlotItem`**

Criar `backend/src/VarthexComanda.Desktop/Atendimento/ComandaSlotItem.cs`:

```csharp
namespace VarthexComanda.Desktop.Atendimento;

public class ComandaSlotItem
{
    public required int Numero { get; init; }
    public required bool Aberta { get; init; }
    public int? ComandaId { get; init; }
    public required string TotalFormatado { get; init; }
    public required string TempoFormatado { get; init; }
}
```

- [ ] **Step 2: Atualizar o call site existente em `AtendimentoViewModelTests.cs` (vai quebrar a compilação — o construtor ainda não mudou)**

Em `backend/tests/VarthexComanda.Desktop.Tests/Atendimento/AtendimentoViewModelTests.cs`,
substituir a chamada do construtor dentro de `CriarViewModel`:

```csharp
        var viewModel = new AtendimentoViewModel(
            new AbrirComanda(comandas, relogio, new ObterConfiguracao(new FakeConfiguracaoRepository())),
            new AdicionarItem(comandas, produtos, relogio),
            new AlterarQuantidade(comandas, relogio),
            new RemoverItem(comandas, relogio),
            new CancelarComanda(comandas, relogio),
            comandas,
            new ListarCategoriasAtivas(categorias),
            new PesquisarProdutos(produtos),
            confirmador,
            encerramentoDialog);
```

por:

```csharp
        var viewModel = new AtendimentoViewModel(
            new AbrirComanda(comandas, relogio, new ObterConfiguracao(new FakeConfiguracaoRepository())),
            new AdicionarItem(comandas, produtos, relogio),
            new AlterarQuantidade(comandas, relogio),
            new RemoverItem(comandas, relogio),
            new CancelarComanda(comandas, relogio),
            comandas,
            new ListarCategoriasAtivas(categorias),
            new PesquisarProdutos(produtos),
            confirmador,
            encerramentoDialog,
            new ObterConfiguracao(new FakeConfiguracaoRepository()),
            relogio);
```

- [ ] **Step 3: Escrever os 5 novos testes de slots (vão falhar — `Slots`/`AbrirOuSelecionarSlotCommand` ainda não existem)**

No mesmo arquivo `AtendimentoViewModelTests.cs`, adicionar estes 5 testes
ao final da classe (antes do `}` de fechamento). Eles NÃO usam o helper
`CriarViewModel` (precisam de acesso direto a `relogio`/controle fino do
repositório de configuração antes de construir o ViewModel):

```csharp
    [Fact]
    public void AtualizarSlots_SemConfiguracao_Cria20SlotsComOsAbertosCorretos()
    {
        var categorias = new FakeCategoriaRepository();
        var relogio = new FakeClock();
        var comandas = new FakeComandaRepository();
        var confirmador = new FakeConfirmador { ProximaResposta = true };
        var encerramentoDialog = new FakeEncerramentoDialog(comandas, relogio);

        var viewModel = new AtendimentoViewModel(
            new AbrirComanda(comandas, relogio, new ObterConfiguracao(new FakeConfiguracaoRepository())),
            new AdicionarItem(comandas, new FakeProdutoRepository(), relogio),
            new AlterarQuantidade(comandas, relogio),
            new RemoverItem(comandas, relogio),
            new CancelarComanda(comandas, relogio),
            comandas,
            new ListarCategoriasAtivas(categorias),
            new PesquisarProdutos(new FakeProdutoRepository()),
            confirmador,
            encerramentoDialog,
            new ObterConfiguracao(new FakeConfiguracaoRepository()),
            relogio);

        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);

        Assert.Equal(20, viewModel.Slots.Count);
        var slotAberto = viewModel.Slots.Single(s => s.Numero == 10);
        Assert.True(slotAberto.Aberta);
        Assert.NotNull(slotAberto.ComandaId);
        var slotLivre = viewModel.Slots.Single(s => s.Numero == 1);
        Assert.False(slotLivre.Aberta);
        Assert.Null(slotLivre.ComandaId);
    }

    [Fact]
    public void AtualizarSlots_ComConfiguracao_UsaTamanhoConfigurado()
    {
        var categorias = new FakeCategoriaRepository();
        var relogio = new FakeClock();
        var comandas = new FakeComandaRepository();
        var confirmador = new FakeConfirmador { ProximaResposta = true };
        var encerramentoDialog = new FakeEncerramentoDialog(comandas, relogio);
        var configuracoes = new FakeConfiguracaoRepository();
        configuracoes.Definir("comandas.quantidade_maxima", "5", DateTime.UtcNow);

        var viewModel = new AtendimentoViewModel(
            new AbrirComanda(comandas, relogio, new ObterConfiguracao(new FakeConfiguracaoRepository())),
            new AdicionarItem(comandas, new FakeProdutoRepository(), relogio),
            new AlterarQuantidade(comandas, relogio),
            new RemoverItem(comandas, relogio),
            new CancelarComanda(comandas, relogio),
            comandas,
            new ListarCategoriasAtivas(categorias),
            new PesquisarProdutos(new FakeProdutoRepository()),
            confirmador,
            encerramentoDialog,
            new ObterConfiguracao(configuracoes),
            relogio);

        Assert.Equal(5, viewModel.Slots.Count);
    }

    [Fact]
    public void AbrirOuSelecionarSlot_SlotLivre_AbreComandaNesseNumero()
    {
        var (viewModel, _, _, _) = CriarViewModel();

        var slotLivre = viewModel.Slots.Single(s => s.Numero == 7);
        viewModel.AbrirOuSelecionarSlotCommand.Execute(slotLivre);

        Assert.NotNull(viewModel.ComandaAtual);
        Assert.Equal(7, viewModel.ComandaAtual!.Numero);
    }

    [Fact]
    public void AbrirOuSelecionarSlot_SlotAberto_EntraNaEdicao()
    {
        var (viewModel, _, _, _) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);
        viewModel.FecharEdicaoCommand.Execute(null);

        var slotAberto = viewModel.Slots.Single(s => s.Numero == 10);
        viewModel.AbrirOuSelecionarSlotCommand.Execute(slotAberto);

        Assert.NotNull(viewModel.ComandaAtual);
        Assert.Equal(10, viewModel.ComandaAtual!.Numero);
    }

    [Fact]
    public void AtualizarSlots_ComandaAbertaMostraTotalETempoFormatados()
    {
        var categorias = new FakeCategoriaRepository();
        var relogio = new FakeClock();
        var categoria = new CadastrarCategoria(categorias, relogio).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        new CadastrarProduto(produtos, categorias, relogio).Executar("Refrigerante", categoria.Id, 500);
        var comandas = new FakeComandaRepository();
        var confirmador = new FakeConfirmador { ProximaResposta = true };
        var encerramentoDialog = new FakeEncerramentoDialog(comandas, relogio);

        var viewModel = new AtendimentoViewModel(
            new AbrirComanda(comandas, relogio, new ObterConfiguracao(new FakeConfiguracaoRepository())),
            new AdicionarItem(comandas, produtos, relogio),
            new AlterarQuantidade(comandas, relogio),
            new RemoverItem(comandas, relogio),
            new CancelarComanda(comandas, relogio),
            comandas,
            new ListarCategoriasAtivas(categorias),
            new PesquisarProdutos(produtos),
            confirmador,
            encerramentoDialog,
            new ObterConfiguracao(new FakeConfiguracaoRepository()),
            relogio);

        viewModel.NovoNumero = "7";
        viewModel.AbrirCommand.Execute(null);

        relogio.UtcNow = relogio.UtcNow.AddMinutes(5);
        var produto = produtos.Pesquisar(null, null)[0];
        viewModel.AdicionarProdutoAoItemCommand.Execute(produto);

        var slot = viewModel.Slots.Single(s => s.Numero == 7);
        Assert.True(slot.Aberta);
        Assert.Equal("R$ 5,00", slot.TotalFormatado);
        Assert.Equal("há 5 min", slot.TempoFormatado);
    }
```

Adicionar `using System.Linq;` se ainda não existir no arquivo (necessário
para `.Single(...)` — como o projeto usa `ImplicitUsings`, isso já deve
estar disponível automaticamente; não adicionar `using` manual se o
arquivo já compila com LINQ em outros pontos).

- [ ] **Step 4: Rodar os testes e confirmar que falham (erro de compilação)**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
```

Esperado: falha de build — `AtendimentoViewModel` não tem 12 parâmetros
no construtor, `Slots`/`AbrirOuSelecionarSlotCommand` não existem.

- [ ] **Step 5: Atualizar `AtendimentoViewModel`**

Em `backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoViewModel.cs`,
adicionar aos `using`s existentes:

```csharp
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Configuracao;
```

Adicionar dois novos campos privados (junto dos existentes):

```csharp
    private readonly ObterConfiguracao _obterConfiguracao;
    private readonly IClock _relogio;
```

Atualizar o construtor (assinatura completa, com os 2 novos parâmetros no
final):

```csharp
    public AtendimentoViewModel(
        AbrirComanda abrirComanda,
        AdicionarItem adicionarItem,
        AlterarQuantidade alterarQuantidade,
        RemoverItem removerItem,
        CancelarComanda cancelarComanda,
        IComandaRepository comandas,
        ListarCategoriasAtivas listarCategoriasAtivas,
        PesquisarProdutos pesquisarProdutos,
        IConfirmador confirmador,
        IEncerramentoDialog encerramentoDialog,
        ObterConfiguracao obterConfiguracao,
        IClock relogio)
    {
        _abrirComanda = abrirComanda;
        _adicionarItem = adicionarItem;
        _alterarQuantidade = alterarQuantidade;
        _removerItem = removerItem;
        _cancelarComanda = cancelarComanda;
        _comandas = comandas;
        _listarCategoriasAtivas = listarCategoriasAtivas;
        _pesquisarProdutos = pesquisarProdutos;
        _confirmador = confirmador;
        _encerramentoDialog = encerramentoDialog;
        _obterConfiguracao = obterConfiguracao;
        _relogio = relogio;

        ComandasAbertas = new ObservableCollection<Comanda>();
        Categorias = new ObservableCollection<Categoria>();
        Itens = new ObservableCollection<ItemComanda>();
        ProdutosCatalogo = new ObservableCollection<Produto>();
        Slots = new ObservableCollection<ComandaSlotItem>();

        AtualizarComandasAbertas();
        CarregarCategorias();
    }
```

Adicionar a nova propriedade pública (junto das outras `ObservableCollection`
já expostas):

```csharp
    public ObservableCollection<ComandaSlotItem> Slots { get; }
```

Substituir o método privado `AtualizarComandasAbertas()` (torna-se
público e passa a recalcular os slots) e adicionar os dois novos métodos
privados de apoio:

```csharp
    public void AtualizarComandasAbertas()
    {
        ComandasAbertas.Clear();
        foreach (var comanda in _comandas.ListarAbertas())
        {
            ComandasAbertas.Add(comanda);
        }
        AtualizarSlots();
    }

    private void AtualizarSlots()
    {
        var configuracao = _obterConfiguracao.Executar();
        var tamanho = configuracao.QuantidadeMaximaComandas ?? 20;
        var agora = _relogio.UtcNow;

        Slots.Clear();
        for (var numero = 1; numero <= tamanho; numero++)
        {
            var comanda = ComandasAbertas.FirstOrDefault(c => c.Numero == numero);
            if (comanda is not null)
            {
                Slots.Add(new ComandaSlotItem
                {
                    Numero = numero,
                    Aberta = true,
                    ComandaId = comanda.Id,
                    TotalFormatado = CentavosParaMoedaConverter.Formatar(comanda.TotalCentavos),
                    TempoFormatado = FormatarTempoAberta(comanda.AbertaEm, agora)
                });
            }
            else
            {
                Slots.Add(new ComandaSlotItem
                {
                    Numero = numero,
                    Aberta = false,
                    ComandaId = null,
                    TotalFormatado = string.Empty,
                    TempoFormatado = string.Empty
                });
            }
        }
    }

    private static string FormatarTempoAberta(DateTime abertaEmUtc, DateTime agoraUtc)
    {
        var decorrido = agoraUtc - abertaEmUtc;
        if (decorrido.TotalMinutes < 1)
        {
            return "agora";
        }
        if (decorrido.TotalMinutes < 60)
        {
            return $"há {(int)decorrido.TotalMinutes} min";
        }
        return $"há {(int)decorrido.TotalHours} h";
    }
```

Nota: mantenha todo o resto do arquivo (todos os outros comandos e
métodos já existentes) exatamente como está — esta é uma edição
aditiva, não uma reescrita.

Por fim, adicionar o novo command `AbrirOuSelecionarSlot`, logo depois do
método privado `SelecionarComanda`/`AbrirParaEdicao` já existentes:

```csharp
    [RelayCommand]
    private void AbrirOuSelecionarSlot(ComandaSlotItem slot)
    {
        if (slot.Aberta)
        {
            var comanda = ComandasAbertas.FirstOrDefault(c => c.Numero == slot.Numero);
            if (comanda is not null)
            {
                AbrirParaEdicao(comanda.Id);
            }
            return;
        }

        NovoNumero = slot.Numero.ToString();
        Abrir();
    }
```

- [ ] **Step 6: Rodar os testes e confirmar que passam**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~AtendimentoViewModelTests"
```

Esperado: PASS em todos (11 testes já existentes + 5 novos = 16).

- [ ] **Step 7: Rodar a suíte inteira e commitar**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

Esperado: todos os projetos PASS. Total esperado: Domain 4 + Application
73 + Infrastructure 63 + Desktop 44 (39 + 5 novos) = 184.

```bash
git add backend/src/VarthexComanda.Desktop/Atendimento/ComandaSlotItem.cs backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoViewModel.cs backend/tests/VarthexComanda.Desktop.Tests/Atendimento/AtendimentoViewModelTests.cs
git commit -m "feat: adiciona grade de slots configuravel ao AtendimentoViewModel"
```

---

## Task 2: Reescrita da `AtendimentoView` (XAML) e wiring

**Files:**
- Modify: `backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoView.xaml`
- Modify: `backend/src/VarthexComanda.Desktop/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `AtendimentoViewModel.Slots`, `.AbrirOuSelecionarSlotCommand`,
  `.Categorias`, `.CategoriaCatalogo`, `.ProdutosCatalogo`,
  `.AdicionarProdutoAoItemCommand`, `.Itens`, `.AumentarQuantidadeCommand`,
  `.DiminuirQuantidadeCommand`, `.RemoverCommand`, `.ComandaAtual`,
  `.FecharEdicaoCommand`, `.CancelarComandaAtualCommand`,
  `.VerTotalCommand`, `.Mensagem` (todos já existem, da Task 1 e de
  antes), `NuloParaVisibilidadeConverter`/`NuloParaVisibilidadeInversoConverter`/
  `CentavosParaMoedaConverter` (já existem, mesmo arquivo de recursos).
- Produces: nada consumido por tasks futuras — esta é a última task
  funcional da fatia.

- [ ] **Step 1: Substituir o conteúdo de `AtendimentoView.xaml`**

Este UserControl não tem testes automatizados (é XAML puro) — a
validação é `dotnet build` limpo + o roteiro manual do Step 3. Substituir
o conteúdo inteiro de
`backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoView.xaml` por:

```xml
<UserControl x:Class="VarthexComanda.Desktop.Atendimento.AtendimentoView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:local="clr-namespace:VarthexComanda.Desktop.Atendimento"
             mc:Ignorable="d"
             Background="#F2F2F0">
    <UserControl.Resources>
        <local:NuloParaVisibilidadeConverter x:Key="VisivelSeComandaAberta" />
        <local:NuloParaVisibilidadeInversoConverter x:Key="VisivelSeGrade" />
        <local:CentavosParaMoedaConverter x:Key="Moeda" />
    </UserControl.Resources>
    <UserControl.InputBindings>
        <KeyBinding Key="F4" Command="{Binding VerTotalCommand}" />
    </UserControl.InputBindings>
    <Grid Margin="16">

        <StackPanel Visibility="{Binding ComandaAtual, Converter={StaticResource VisivelSeGrade}}">
            <TextBlock Text="COMANDAS" FontWeight="Bold" FontSize="12" Foreground="#4A4A47" Margin="0,0,0,10" />

            <ItemsControl ItemsSource="{Binding Slots}">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <UniformGrid Columns="5" />
                    </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Button Margin="4" Height="104"
                                Command="{Binding DataContext.AbrirOuSelecionarSlotCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                CommandParameter="{Binding}">
                            <Button.Style>
                                <Style TargetType="Button">
                                    <Setter Property="Background" Value="White" />
                                    <Setter Property="BorderBrush" Value="#C4C4C4" />
                                    <Setter Property="BorderThickness" Value="1" />
                                    <Setter Property="Foreground" Value="#23252A" />
                                    <Style.Triggers>
                                        <DataTrigger Binding="{Binding Aberta}" Value="True">
                                            <Setter Property="Background" Value="#D9822B" />
                                            <Setter Property="BorderBrush" Value="#BF701F" />
                                            <Setter Property="Foreground" Value="White" />
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </Button.Style>
                            <Grid Margin="10">
                                <Grid.RowDefinitions>
                                    <RowDefinition Height="Auto" />
                                    <RowDefinition Height="*" />
                                    <RowDefinition Height="Auto" />
                                </Grid.RowDefinitions>
                                <DockPanel Grid.Row="0">
                                    <TextBlock Text="COMANDA" FontSize="10" FontWeight="Bold" />
                                    <TextBlock Text="{Binding TempoFormatado}" FontSize="10" DockPanel.Dock="Right" />
                                </DockPanel>
                                <TextBlock Grid.Row="1" Text="{Binding Numero, StringFormat='{}{0:00}'}"
                                           FontSize="28" FontWeight="Bold"
                                           HorizontalAlignment="Center" VerticalAlignment="Center" />
                                <TextBlock Grid.Row="2" HorizontalAlignment="Center" FontSize="11">
                                    <TextBlock.Style>
                                        <Style TargetType="TextBlock">
                                            <Setter Property="Text" Value="{Binding TotalFormatado}" />
                                            <Style.Triggers>
                                                <DataTrigger Binding="{Binding Aberta}" Value="False">
                                                    <Setter Property="Text" Value="LIVRE" />
                                                </DataTrigger>
                                            </Style.Triggers>
                                        </Style>
                                    </TextBlock.Style>
                                </TextBlock>
                            </Grid>
                        </Button>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>

            <TextBlock Text="{Binding Mensagem}" Foreground="#B23B32" Margin="0,12,0,0" />
        </StackPanel>

        <Grid Visibility="{Binding ComandaAtual, Converter={StaticResource VisivelSeComandaAberta}}">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="1*" />
                <ColumnDefinition Width="1" />
                <ColumnDefinition Width="1*" />
            </Grid.ColumnDefinitions>

            <Border Grid.Column="1" Background="#C4C4C4" />

            <Grid Grid.Column="0" Margin="0,0,14,0">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="*" />
                    <RowDefinition Height="Auto" />
                </Grid.RowDefinitions>

                <TextBlock Grid.Row="0" Text="MENU" FontWeight="Bold" FontSize="12" Foreground="#4A4A47" Margin="0,0,0,8" />

                <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto">
                    <ItemsControl ItemsSource="{Binding ProdutosCatalogo}">
                        <ItemsControl.ItemsPanel>
                            <ItemsPanelTemplate>
                                <WrapPanel />
                            </ItemsPanelTemplate>
                        </ItemsControl.ItemsPanel>
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Button Width="128" Height="128" Margin="0,0,8,8" Padding="0"
                                        Background="White" BorderBrush="#C4C4C4" BorderThickness="1"
                                        Command="{Binding DataContext.AdicionarProdutoAoItemCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                        CommandParameter="{Binding}">
                                    <Grid>
                                        <Grid.RowDefinitions>
                                            <RowDefinition Height="*" />
                                            <RowDefinition Height="Auto" />
                                        </Grid.RowDefinitions>
                                        <Border Grid.Row="0" Background="#EDEDEA" />
                                        <Border Grid.Row="1" BorderBrush="#C4C4C4" BorderThickness="0,1,0,0" Padding="8,6">
                                            <StackPanel>
                                                <TextBlock Text="{Binding Nome}" FontSize="12" FontWeight="SemiBold"
                                                           TextTrimming="CharacterEllipsis" />
                                                <TextBlock Text="{Binding PrecoCentavos, Converter={StaticResource Moeda}}"
                                                           FontSize="10.5" Foreground="#6B6B67" />
                                            </StackPanel>
                                        </Border>
                                    </Grid>
                                </Button>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </ScrollViewer>

                <ItemsControl Grid.Row="2" ItemsSource="{Binding Categorias}" Margin="0,8,0,0">
                    <ItemsControl.ItemsPanel>
                        <ItemsPanelTemplate>
                            <UniformGrid Rows="1" />
                        </ItemsPanelTemplate>
                    </ItemsControl.ItemsPanel>
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Button Height="40" Margin="0,0,6,0" Padding="4"
                                    Command="{Binding DataContext.SelecionarCategoriaCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                    CommandParameter="{Binding}">
                                <Button.Style>
                                    <Style TargetType="Button">
                                        <Setter Property="Background" Value="White" />
                                        <Setter Property="BorderBrush" Value="#C4C4C4" />
                                        <Setter Property="BorderThickness" Value="1" />
                                        <Setter Property="Content" Value="{Binding Nome}" />
                                        <Setter Property="FontSize" Value="11.5" />
                                    </Style>
                                </Button.Style>
                            </Button>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </Grid>

            <Grid Grid.Column="2" Margin="14,0,0,0">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="*" />
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="Auto" />
                </Grid.RowDefinitions>

                <TextBlock Grid.Row="0" FontWeight="Bold" FontSize="12" Foreground="#4A4A47" Margin="0,0,0,8"
                           Text="{Binding ComandaAtual.Numero, StringFormat='COMANDA #{0}'}" />

                <Border Grid.Row="1" BorderBrush="#C4C4C4" BorderThickness="1">
                    <ListView ItemsSource="{Binding Itens}" BorderThickness="0">
                        <ListView.View>
                            <GridView>
                                <GridViewColumn Header="DESCRIÇÃO" DisplayMemberBinding="{Binding NomeProduto}" Width="180" />
                                <GridViewColumn Header="QTD." DisplayMemberBinding="{Binding Quantidade}" Width="50" />
                                <GridViewColumn Header="PREÇO" DisplayMemberBinding="{Binding PrecoUnitarioCentavos, Converter={StaticResource Moeda}}" Width="80" />
                                <GridViewColumn Header="TOTAL" DisplayMemberBinding="{Binding SubtotalCentavos, Converter={StaticResource Moeda}}" Width="80" />
                                <GridViewColumn Width="130">
                                    <GridViewColumn.CellTemplate>
                                        <DataTemplate>
                                            <StackPanel Orientation="Horizontal">
                                                <Button Content="−" Width="24"
                                                        Command="{Binding DataContext.DiminuirQuantidadeCommand, RelativeSource={RelativeSource AncestorType=ListView}}"
                                                        CommandParameter="{Binding}" Margin="0,0,4,0" />
                                                <Button Content="+" Width="24"
                                                        Command="{Binding DataContext.AumentarQuantidadeCommand, RelativeSource={RelativeSource AncestorType=ListView}}"
                                                        CommandParameter="{Binding}" Margin="0,0,4,0" />
                                                <Button Content="Excluir" Foreground="#B23B32"
                                                        Command="{Binding DataContext.RemoverCommand, RelativeSource={RelativeSource AncestorType=ListView}}"
                                                        CommandParameter="{Binding}" />
                                            </StackPanel>
                                        </DataTemplate>
                                    </GridViewColumn.CellTemplate>
                                </GridViewColumn>
                            </GridView>
                        </ListView.View>
                    </ListView>
                </Border>

                <DockPanel Grid.Row="2" Margin="2,10,2,10">
                    <TextBlock Text="TOTAL DA COMANDA" FontWeight="Bold" FontSize="12" Foreground="#4A4A47" VerticalAlignment="Bottom" />
                    <TextBlock Text="{Binding ComandaAtual.TotalCentavos, Converter={StaticResource Moeda}}"
                               FontWeight="Bold" FontSize="22" DockPanel.Dock="Right" HorizontalAlignment="Right" />
                </DockPanel>

                <StackPanel Grid.Row="3" Orientation="Horizontal">
                    <Button Content="Voltar" Padding="16,0" Height="52"
                            Command="{Binding FecharEdicaoCommand}" Margin="0,0,8,0" />
                    <Button Content="Cancelar comanda" Padding="16,0" Height="52" Foreground="#B23B32"
                            Command="{Binding CancelarComandaAtualCommand}" />
                    <Grid Width="1" />
                    <Button Content="Finalizar comanda (F4)" Padding="20,0" Height="52"
                            Background="#3F8F5F" Foreground="White" FontWeight="Bold"
                            Command="{Binding VerTotalCommand}" HorizontalAlignment="Right" />
                </StackPanel>

                <TextBlock Grid.Row="3" Text="{Binding Mensagem}" Foreground="#B23B32" TextWrapping="Wrap" Margin="0,60,0,0" />
            </Grid>
        </Grid>
    </Grid>
</UserControl>
```

Nota importante: o botão de categoria acima referencia
`SelecionarCategoriaCommand`, que **não existe ainda** —
`AtendimentoViewModel` hoje seleciona categoria via `SelectedItem`
bindado a `CategoriaCatalogo` num `ComboBox` (two-way binding
automático), não via command. Como esta tela nova usa botões (não um
ComboBox), adicionar um command simples equivalente. Voltar ao
`AtendimentoViewModel.cs` (mesmo arquivo da Task 1, edição adicional
aqui) e adicionar, logo abaixo de `LimparFiltro()`:

```csharp
    [RelayCommand]
    private void SelecionarCategoria(Categoria categoria) => CategoriaCatalogo = categoria;
```

(`CategoriaCatalogo` já é `[ObservableProperty]` e já dispara
`PesquisarCatalogo()` via `OnCategoriaCatalogoChanged` — nenhuma outra
mudança necessária.)

- [ ] **Step 2: Atualizar `MainWindow.xaml.cs` pra recarregar a grade de comandas ao trocar de aba**

Em `backend/src/VarthexComanda.Desktop/MainWindow.xaml.cs`, o handler
`MostrarAtendimento_Click` hoje é:

```csharp
    private void MostrarAtendimento_Click(object sender, RoutedEventArgs e)
    {
        _atendimentoView.ViewModel.AtualizarCategorias();
        ConteudoPrincipal.Content = _atendimentoView;
    }
```

Substituir por (agora também atualiza a grade de comandas/slots, já que
`AtualizarComandasAbertas()` passou a ser público na Task 1 — reflete
uma mudança de configuração de "Quantidade de comandas" feita enquanto o
operador estava em outra aba):

```csharp
    private void MostrarAtendimento_Click(object sender, RoutedEventArgs e)
    {
        _atendimentoView.ViewModel.AtualizarCategorias();
        _atendimentoView.ViewModel.AtualizarComandasAbertas();
        ConteudoPrincipal.Content = _atendimentoView;
    }
```

- [ ] **Step 3: Build e suíte completa (sem regressão esperada)**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
```

Esperado: build limpo, sem erros.

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

Esperado: 184/184 (mesmo total da Task 1 — XAML não adiciona testes).

- [ ] **Step 4: Commitar**

```bash
git add backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoView.xaml backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoViewModel.cs backend/src/VarthexComanda.Desktop/MainWindow.xaml.cs
git commit -m "feat: reescreve a tela de Atendimento com grade de slots e layout menu+carrinho"
```

- [ ] **Step 5: Roteiro de verificação manual**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet run --project backend/src/VarthexComanda.Desktop
```

1. Abrir o app na aba "Atendimento" — deve aparecer a grade de 20 slots
   (ou o número configurado em Configurações, se algo tiver sido salvo lá
   antes). Slots sem comanda aberta mostram "LIVRE"; nenhum slot aberto
   ainda nesta primeira execução.
2. Clicar num slot livre (ex. "03") — deve abrir a comanda nesse número e
   levar direto pra tela de menu+carrinho.
3. Clicar em 2-3 produtos do menu — cada clique deve aparecer/incrementar
   uma linha na tabela da direita, com total atualizando.
4. Usar os botões "−"/"+" na linha de um item — quantidade e subtotal
   devem atualizar; diminuir até 1 e clicar "−" de novo deve pedir
   confirmação antes de remover a linha.
5. Clicar "Voltar" — deve voltar pra grade, e o slot da comanda que
   ficou com itens deve aparecer com fundo âmbar, mostrando "aberta há
   0 min" (ou similar) e o total correto.
6. Reabrir a mesma comanda (clicar no slot ocupado) — os itens
   lançados anteriormente devem continuar lá.
7. Pressionar **F4** dentro da tela da comanda (com foco em algum
   elemento da tela, não numa caixa de texto) — deve abrir a tela de
   Encerramento existente (mesma tela da Etapa 4, com total e
   confirmação de cobrança).
8. Confirmar o encerramento — a comanda deve sumir da grade (voltar a
   "LIVRE").
9. Abrir outra comanda, adicionar um item, clicar "Cancelar comanda" —
   deve pedir confirmação (mesma mensagem de hoje) e, ao confirmar,
   voltar a comanda pra "LIVRE" na grade.
10. Ir em Configurações, definir "Quantidade de comandas" = 5, salvar,
    voltar pra Atendimento — a grade deve passar a mostrar só 5 slots.

---

## Task 3: Verificação final e changelog

**Files:**
- Modify: `docs/CHANGELOG.md`

- [ ] **Step 1: Rodar a suíte completa uma última vez**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

Esperado: 184/184 (Domain 4 + Application 73 + Infrastructure 63 +
Desktop 44).

- [ ] **Step 2: Atualizar o changelog**

Em `docs/CHANGELOG.md`, adicionar uma nova entrada `1.11`:

```markdown
## 1.11 - 2026-09-18 - Redesign da tela de Atendimento

- Grade de comandas passa a mostrar um número fixo de slots numerados
  (configurável em Configurações, 20 por padrão), cada um marcado como
  "aberta" (com total e tempo desde a abertura) ou "livre" — em vez da
  lista dinâmica anterior só com comandas já abertas.
- Tela de edição de uma comanda passa a mostrar o menu de produtos como
  uma grade de cards (com placeholder de foto) filtrável por categoria,
  e o carrinho como uma tabela com colunas Descrição/Qtd./Preço/Total.
- Botão "Finalizar comanda" ganha o atalho de teclado F4; continua
  abrindo a mesma tela de Encerramento já existente desde a Etapa 4.
- Toda a lógica de negócio (abrir, adicionar/alterar/remover item,
  cancelar, encerrar) foi reaproveitada sem alteração — esta fatia é
  só a reconstrução visual da tela.
```

- [ ] **Step 3: Commitar**

```bash
git add docs/CHANGELOG.md
git commit -m "docs: registra o redesign da tela de atendimento no changelog"
```
