# Encerramento e Venda (Etapa 4) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement RF14-17 (revalidar total, exibir tela de encerramento,
confirmar cobrança externa, gravar venda + fechar comanda em uma única
transação), reaproveitando as entidades `Venda`/`StatusVenda` já mapeadas
desde a Etapa 0+1.

**Architecture:** Um oitavo método verbo `EncerrarComanda` em
`IComandaRepository` (um `DbContext`, um `SaveChanges`, mesmo padrão das
Etapas 2-3). Novo caso de uso `EncerrarComanda` na Application revalida RN09
(comanda vazia) antes de delegar. Nova `Window` modal `EncerramentoView` +
`EncerramentoViewModel` dedicada e testável (sem tipos WPF), aberta via uma
interface `IEncerramentoDialog` a partir de um novo comando `VerTotal` em
`AtendimentoViewModel` — mesma técnica de indireção já usada para
`IConfirmador` na Etapa 3, para não reintroduzir WPF na ViewModel existente.

**Tech Stack:** C#/.NET 10, WPF (CommunityToolkit.Mvvm), EF Core/SQLite,
xUnit.

**Spec:** [specs/2026-09-18-encerramento-design.md](../specs/2026-09-18-encerramento-design.md)

## Global Constraints

- `IComandaRepository`: cada método verbo abre **um único** `DbContext` (via
  `IDbContextFactory`) e chama `SaveChanges()` **uma única vez** — atomicidade
  por construção, sem Unit of Work.
- `venda.numero` é calculado como `MAX(numero) + 1` (ou `1` se não houver
  vendas) **dentro do mesmo `DbContext`** de `EncerrarComanda` — sem tabela de
  contador dedicada.
- Datas continuam em `IClock.UtcNow` — **não** alterar `IClock` nem qualquer
  data já gravada por `comanda`/`item_comanda`.
- RN10 (comanda fechada é imutável): `EncerrarComanda` verifica
  `comanda.Status == StatusComanda.Aberta` antes de mutar, senão lança
  `ComandaNaoAbertaException` (já existe em
  `VarthexComanda.Application.Atendimento`) — mesmo padrão dos outros 5
  métodos mutantes do repositório.
- RN09 (comanda vazia não gera venda): validado no **caso de uso**
  `EncerrarComanda` (Application), não no repositório — mesma divisão de
  responsabilidade que RN06 (quantidade > 0) já usa em `AdicionarItem`.
- `EncerramentoViewModel` e `AtendimentoViewModel` **não podem** ter nenhum
  `using System.Windows` nem referenciar `MessageBox`/`Window` diretamente —
  qualquer necessidade de UI nativa passa por uma interface pequena
  implementada em Desktop (mesmo padrão de `IConfirmador`/`MessageBoxConfirmador`
  da Etapa 3).
- Reaproveitar `CentavosParaMoedaConverter` (já existe em
  `backend/src/VarthexComanda.Desktop/Atendimento/CentavosParaMoedaConverter.cs`)
  para toda exibição de valores em `EncerramentoView.xaml` — não criar um
  segundo formatador de moeda.
- Sem tela de forma de pagamento, sem cálculo de troco, sem chamada a
  adquirente, sem armazenar token/NSU/cartão — proibições explícitas do guia
  de implementação (Etapa 4).
- Prefixar todo comando `dotnet` com o PATH do SDK: PowerShell
  `$env:Path += ';C:\Program Files\dotnet'; ` / Bash
  `export PATH="$PATH:/c/Program Files/dotnet" && `.

---

## Task 1: Repositório — `EncerrarComanda` (Application interface + Infrastructure + Fake)

**Files:**
- Modify: `backend/src/VarthexComanda.Application/Atendimento/IComandaRepository.cs`
- Modify: `backend/src/VarthexComanda.Infrastructure/Persistence/Atendimento/EfComandaRepository.cs`
- Modify: `backend/tests/VarthexComanda.Application.Tests/Atendimento/FakeComandaRepository.cs`
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/Atendimento/EfComandaRepositoryTests.cs`

**Interfaces:**
- Consumes: `VarthexComanda.Domain.Venda` (`{ int Id; int ComandaId; int Numero; long TotalCentavos; DateTime FinalizadaEm; StatusVenda Status; }`, já existe), `VarthexComanda.Domain.StatusVenda.Concluida` (já existe), `contexto.Vendas` (`DbSet<Venda>`, já existe em `VarthexComandaDbContext`), `VarthexComanda.Application.Atendimento.ComandaNaoAbertaException` (já existe).
- Produces: `IComandaRepository.EncerrarComanda(int comandaId, DateTime agora) → Venda` — assinatura que a Task 2 (caso de uso Application) e a Task 3 (fake em Desktop.Tests, reaproveitado) vão consumir.

- [ ] **Step 1: Adicionar o método à interface**

Em `backend/src/VarthexComanda.Application/Atendimento/IComandaRepository.cs`,
adicionar ao final da interface:

```csharp
    Comanda CancelarComanda(int comandaId, DateTime agora);
    Venda EncerrarComanda(int comandaId, DateTime agora);
}
```

- [ ] **Step 2: Implementar no fake (Application.Tests) para manter a solução compilando**

Em `backend/tests/VarthexComanda.Application.Tests/Atendimento/FakeComandaRepository.cs`,
adicionar um campo de vendas e o método, ao final da classe (antes da chave de
fechamento), espelhando exatamente a lógica que o `EfComandaRepository` terá:

```csharp
    private readonly List<Venda> _vendas = new();
    private int _proximoVendaId = 1;

    public Venda EncerrarComanda(int comandaId, DateTime agora)
    {
        var comanda = _comandas.FirstOrDefault(c => c.Id == comandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");
        if (comanda.Status != StatusComanda.Aberta)
        {
            throw new ComandaNaoAbertaException();
        }

        var proximoNumero = (_vendas.Count == 0 ? 0 : _vendas.Max(v => v.Numero)) + 1;
        var venda = new Venda
        {
            Id = _proximoVendaId++,
            ComandaId = comanda.Id,
            Numero = proximoNumero,
            TotalCentavos = comanda.TotalCentavos,
            FinalizadaEm = agora,
            Status = StatusVenda.Concluida
        };
        _vendas.Add(venda);

        comanda.Status = StatusComanda.Fechada;
        comanda.FechadaEm = agora;
        return venda;
    }
```

Nada muda no restante do arquivo. `Venda`/`StatusVenda` já são resolvidos
pelo `using VarthexComanda.Domain;` existente no topo do arquivo.

- [ ] **Step 3: Escrever os testes de infraestrutura (vão falhar por enquanto)**

Em `backend/tests/VarthexComanda.Infrastructure.Tests/Atendimento/EfComandaRepositoryTests.cs`,
adicionar, logo antes da última chave de fechamento da classe:

```csharp
    [Fact]
    public void EncerrarComanda_ComandaAberta_GravaVendaEFechaComanda()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var (produto, comandaId) = PrepararComandaEProduto(repositorio);
        repositorio.AdicionarItem(comandaId, produto, 2, DateTime.UtcNow);

        var venda = repositorio.EncerrarComanda(comandaId, DateTime.UtcNow);

        Assert.Equal(comandaId, venda.ComandaId);
        Assert.Equal(1000, venda.TotalCentavos);
        Assert.Equal(StatusVenda.Concluida, venda.Status);

        var detalhe = repositorio.BuscarComItens(comandaId);
        Assert.Equal(StatusComanda.Fechada, detalhe!.Comanda.Status);
        Assert.NotNull(detalhe.Comanda.FechadaEm);
    }

    [Fact]
    public void EncerrarComanda_DuasVendasSeguidas_NumeroSequencial()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var (produtoA, comandaIdA) = PrepararComandaEProduto(repositorio);
        repositorio.AdicionarItem(comandaIdA, produtoA, 1, DateTime.UtcNow);
        var comandaB = repositorio.AbrirComanda(30, DateTime.UtcNow);
        repositorio.AdicionarItem(comandaB.Id, produtoA, 1, DateTime.UtcNow);

        var vendaA = repositorio.EncerrarComanda(comandaIdA, DateTime.UtcNow);
        var vendaB = repositorio.EncerrarComanda(comandaB.Id, DateTime.UtcNow);

        Assert.Equal(1, vendaA.Numero);
        Assert.Equal(2, vendaB.Numero);
    }

    [Fact]
    public void EncerrarComanda_LiberaNumeroDaComanda()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var (produto, comandaId) = PrepararComandaEProduto(repositorio);
        repositorio.AdicionarItem(comandaId, produto, 1, DateTime.UtcNow);

        repositorio.EncerrarComanda(comandaId, DateTime.UtcNow);

        Assert.Empty(repositorio.ListarAbertas());
        var reaberta = repositorio.AbrirComanda(10, DateTime.UtcNow);
        Assert.True(reaberta.Id > 0);
    }

    [Fact]
    public void EncerrarComanda_ComandaJaFechada_LancaExcecao()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var (produto, comandaId) = PrepararComandaEProduto(repositorio);
        repositorio.AdicionarItem(comandaId, produto, 1, DateTime.UtcNow);
        repositorio.EncerrarComanda(comandaId, DateTime.UtcNow);

        Assert.Throws<VarthexComanda.Application.Atendimento.ComandaNaoAbertaException>(
            () => repositorio.EncerrarComanda(comandaId, DateTime.UtcNow));
    }
```

Note: `PrepararComandaEProduto` abre a comanda com número `10` — o teste
`EncerrarComanda_LiberaNumeroDaComanda` reabre o mesmo número `10` para provar
que ele ficou livre; `EncerrarComanda_DuasVendasSeguidas_NumeroSequencial`
usa uma segunda comanda com número `30` para não colidir.

- [ ] **Step 4: Rodar os testes e confirmar que falham (erro de compilação)**

Rodar (prefixando o PATH do dotnet):

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
```

Esperado: falha de build — `VarthexComanda.Infrastructure.EfComandaRepository`
não implementa `IComandaRepository.EncerrarComanda(int, DateTime)`.

- [ ] **Step 5: Implementar `EncerrarComanda` em `EfComandaRepository`**

Em `backend/src/VarthexComanda.Infrastructure/Persistence/Atendimento/EfComandaRepository.cs`,
adicionar, logo após o método `CancelarComanda` e antes da chave de fechamento
da classe:

```csharp
    public Venda EncerrarComanda(int comandaId, DateTime agora)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var comanda = contexto.Comandas.SingleOrDefault(c => c.Id == comandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");
        if (comanda.Status != StatusComanda.Aberta)
        {
            throw new ComandaNaoAbertaException();
        }

        var proximoNumero = (contexto.Vendas.Max(v => (int?)v.Numero) ?? 0) + 1;
        var venda = new Venda
        {
            Id = 0,
            ComandaId = comanda.Id,
            Numero = proximoNumero,
            TotalCentavos = comanda.TotalCentavos,
            FinalizadaEm = agora,
            Status = StatusVenda.Concluida
        };
        contexto.Vendas.Add(venda);

        comanda.Status = StatusComanda.Fechada;
        comanda.FechadaEm = agora;

        contexto.SaveChanges();
        return venda;
    }
```

`Venda`/`StatusVenda` já são resolvidos pelo `using VarthexComanda.Domain;`
existente no topo do arquivo.

- [ ] **Step 6: Rodar os testes e confirmar que passam**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~EfComandaRepositoryTests"
```

Esperado: PASS em todos os testes de `EfComandaRepositoryTests`, incluindo os
4 novos.

- [ ] **Step 7: Rodar a suíte inteira e commitar**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

Esperado: todos os projetos PASS (nenhum caso de uso ou ViewModel consome o
método ainda — nada quebra).

```bash
git add backend/src/VarthexComanda.Application/Atendimento/IComandaRepository.cs backend/src/VarthexComanda.Infrastructure/Persistence/Atendimento/EfComandaRepository.cs backend/tests/VarthexComanda.Application.Tests/Atendimento/FakeComandaRepository.cs backend/tests/VarthexComanda.Infrastructure.Tests/Atendimento/EfComandaRepositoryTests.cs
git commit -m "feat: adiciona EncerrarComanda ao repositorio de comandas"
```

---

## Task 2: Caso de uso `EncerrarComanda` (Application)

**Files:**
- Create: `backend/src/VarthexComanda.Application/Atendimento/EncerrarComanda.cs`
- Test: `backend/tests/VarthexComanda.Application.Tests/Atendimento/EncerrarComandaTests.cs`

**Interfaces:**
- Consumes: `IComandaRepository.BuscarComItens(int) → ComandaComItens?`, `IComandaRepository.EncerrarComanda(int, DateTime) → Venda` (Task 1), `IClock.UtcNow` (`VarthexComanda.Application.Abstractions`, já existe), `Resultado<T>` (`VarthexComanda.Application.Catalogo`, já existe), `ComandaNaoAbertaException` (já existe).
- Produces: `EncerrarComanda.Executar(int comandaId) → Resultado<Venda>` — consumido pela Task 3 (`EncerramentoViewModel`).

- [ ] **Step 1: Escrever os testes (vão falhar — a classe não existe)**

Criar `backend/tests/VarthexComanda.Application.Tests/Atendimento/EncerrarComandaTests.cs`:

```csharp
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Application.Tests.Atendimento;

public class EncerrarComandaTests
{
    [Fact]
    public void Executar_ComandaComItens_RetornaVendaEFechaComanda()
    {
        var comandas = new FakeComandaRepository();
        var relogio = new FakeClock();
        var comanda = comandas.AbrirComanda(10, relogio.UtcNow);
        var produto = new Produto { Id = 1, CategoriaId = 1, Nome = "Refrigerante", PrecoCentavos = 500, Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow };
        comandas.AdicionarItem(comanda.Id, produto, 2, relogio.UtcNow);
        var caso = new EncerrarComanda(comandas, relogio);

        var resultado = caso.Executar(comanda.Id);

        Assert.True(resultado.Sucesso);
        Assert.Equal(1000, resultado.Valor!.TotalCentavos);
        Assert.Empty(comandas.ListarAbertas());
    }

    [Fact]
    public void Executar_ComandaVazia_Falha()
    {
        var comandas = new FakeComandaRepository();
        var relogio = new FakeClock();
        var comanda = comandas.AbrirComanda(10, relogio.UtcNow);
        var caso = new EncerrarComanda(comandas, relogio);

        var resultado = caso.Executar(comanda.Id);

        Assert.False(resultado.Sucesso);
        Assert.Equal("Adicione um item antes de encerrar.", resultado.Erros[0]);
        Assert.Single(comandas.ListarAbertas());
    }

    [Fact]
    public void Executar_ComandaInexistente_Falha()
    {
        var comandas = new FakeComandaRepository();
        var relogio = new FakeClock();
        var caso = new EncerrarComanda(comandas, relogio);

        var resultado = caso.Executar(999);

        Assert.False(resultado.Sucesso);
        Assert.Equal("Comanda não encontrada.", resultado.Erros[0]);
    }

    [Fact]
    public void Executar_ComandaJaFechada_Falha()
    {
        var comandas = new FakeComandaRepository();
        var relogio = new FakeClock();
        var comanda = comandas.AbrirComanda(10, relogio.UtcNow);
        var produto = new Produto { Id = 1, CategoriaId = 1, Nome = "Refrigerante", PrecoCentavos = 500, Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow };
        comandas.AdicionarItem(comanda.Id, produto, 1, relogio.UtcNow);
        comandas.EncerrarComanda(comanda.Id, relogio.UtcNow);
        var caso = new EncerrarComanda(comandas, relogio);

        var resultado = caso.Executar(comanda.Id);

        Assert.False(resultado.Sucesso);
        Assert.Equal("A comanda não está aberta.", resultado.Erros[0]);
    }
}
```

- [ ] **Step 2: Rodar os testes e confirmar que falham**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~EncerrarComandaTests"
```

Esperado: falha de build — o tipo `EncerrarComanda` (caso de uso) não existe.

- [ ] **Step 3: Implementar o caso de uso**

Criar `backend/src/VarthexComanda.Application/Atendimento/EncerrarComanda.cs`:

```csharp
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Atendimento;

public class EncerrarComanda
{
    private readonly IComandaRepository _comandas;
    private readonly IClock _relogio;

    public EncerrarComanda(IComandaRepository comandas, IClock relogio)
    {
        _comandas = comandas;
        _relogio = relogio;
    }

    public Resultado<Venda> Executar(int comandaId)
    {
        var detalhe = _comandas.BuscarComItens(comandaId);
        if (detalhe is null)
        {
            return Resultado<Venda>.Falha("Comanda não encontrada.");
        }
        if (detalhe.Itens.Count == 0)
        {
            return Resultado<Venda>.Falha("Adicione um item antes de encerrar.");
        }

        try
        {
            return Resultado<Venda>.Ok(_comandas.EncerrarComanda(comandaId, _relogio.UtcNow));
        }
        catch (ComandaNaoAbertaException ex)
        {
            return Resultado<Venda>.Falha(ex.Message);
        }
    }
}
```

- [ ] **Step 4: Rodar os testes e confirmar que passam**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~EncerrarComandaTests"
```

Esperado: PASS nos 4 testes.

- [ ] **Step 5: Rodar a suíte inteira e commitar**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

```bash
git add backend/src/VarthexComanda.Application/Atendimento/EncerrarComanda.cs backend/tests/VarthexComanda.Application.Tests/Atendimento/EncerrarComandaTests.cs
git commit -m "feat: adiciona caso de uso EncerrarComanda"
```

---

## Task 3: `EncerramentoViewModel` + `IEncerramentoDialog` (Desktop, sem WPF/XAML ainda)

**Files:**
- Create: `backend/src/VarthexComanda.Desktop/Atendimento/IEncerramentoDialog.cs`
- Create: `backend/src/VarthexComanda.Desktop/Atendimento/EncerramentoViewModel.cs`
- Test: `backend/tests/VarthexComanda.Desktop.Tests/Atendimento/EncerramentoViewModelTests.cs`

**Interfaces:**
- Consumes: `EncerrarComanda.Executar(int) → Resultado<Venda>` (Task 2), `IComandaRepository.BuscarComItens(int) → ComandaComItens?` (já existe), `ComandaComItens { Comanda; Itens }` (já existe).
- Produces: `EncerramentoViewModel(EncerrarComanda, IComandaRepository)`, método público `Carregar(int comandaId)`, propriedades `NumeroComanda`/`TotalCentavos`/`Itens`/`CobrancaAprovada`/`Mensagem`, comandos `ConfirmarEncerrarCommand`/`VoltarCommand`, evento `event EventHandler<bool>? Concluido` — todos consumidos pela Task 4 (`EncerramentoView`/`EncerramentoDialog` e o novo comando `VerTotal` em `AtendimentoViewModel`).

- [ ] **Step 1: Escrever a interface do diálogo**

Criar `backend/src/VarthexComanda.Desktop/Atendimento/IEncerramentoDialog.cs`:

```csharp
namespace VarthexComanda.Desktop.Atendimento;

public interface IEncerramentoDialog
{
    bool Abrir(int comandaId);
}
```

- [ ] **Step 2: Escrever os testes da ViewModel (vão falhar — a classe não existe)**

Criar `backend/tests/VarthexComanda.Desktop.Tests/Atendimento/EncerramentoViewModelTests.cs`:

```csharp
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Tests.Atendimento;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Atendimento;

public class EncerramentoViewModelTests
{
    private static (EncerramentoViewModel viewModel, FakeComandaRepository comandas, int comandaId) CriarViewModel(bool comItem = true)
    {
        var relogio = new FakeClock();
        var comandas = new FakeComandaRepository();
        var comanda = comandas.AbrirComanda(10, relogio.UtcNow);
        if (comItem)
        {
            var produto = new Produto { Id = 1, CategoriaId = 1, Nome = "Refrigerante", PrecoCentavos = 500, Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow };
            comandas.AdicionarItem(comanda.Id, produto, 2, relogio.UtcNow);
        }

        var viewModel = new EncerramentoViewModel(new EncerrarComanda(comandas, relogio), comandas);
        viewModel.Carregar(comanda.Id);
        return (viewModel, comandas, comanda.Id);
    }

    [Fact]
    public void Carregar_ComandaComItens_PreencheNumeroTotalEItens()
    {
        var (viewModel, _, _) = CriarViewModel();

        Assert.Equal(10, viewModel.NumeroComanda);
        Assert.Equal(1000, viewModel.TotalCentavos);
        Assert.Single(viewModel.Itens);
    }

    [Fact]
    public void ConfirmarEncerrar_SemCobrancaAprovada_ComandoDesabilitado()
    {
        var (viewModel, _, _) = CriarViewModel();

        Assert.False(viewModel.ConfirmarEncerrarCommand.CanExecute(null));
    }

    [Fact]
    public void ConfirmarEncerrar_CobrancaAprovada_FechaComandaEDisparaConcluidoTrue()
    {
        var (viewModel, comandas, comandaId) = CriarViewModel();
        bool? resultadoEvento = null;
        viewModel.Concluido += (_, sucesso) => resultadoEvento = sucesso;
        viewModel.CobrancaAprovada = true;

        viewModel.ConfirmarEncerrarCommand.Execute(null);

        Assert.True(resultadoEvento);
        Assert.Empty(comandas.ListarAbertas());
    }

    [Fact]
    public void ConfirmarEncerrar_ComandaVazia_MostraMensagemENaoDisparaConcluido()
    {
        var (viewModel, comandas, comandaId) = CriarViewModel(comItem: false);
        bool eventoDisparado = false;
        viewModel.Concluido += (_, __) => eventoDisparado = true;
        viewModel.CobrancaAprovada = true;

        viewModel.ConfirmarEncerrarCommand.Execute(null);

        Assert.False(eventoDisparado);
        Assert.Equal("Adicione um item antes de encerrar.", viewModel.Mensagem);
        Assert.Single(comandas.ListarAbertas());
    }

    [Fact]
    public void Voltar_DisparaConcluidoFalseSemAlterarComanda()
    {
        var (viewModel, comandas, comandaId) = CriarViewModel();
        bool? resultadoEvento = null;
        viewModel.Concluido += (_, sucesso) => resultadoEvento = sucesso;

        viewModel.VoltarCommand.Execute(null);

        Assert.False(resultadoEvento);
        Assert.Single(comandas.ListarAbertas());
    }
}
```

- [ ] **Step 3: Rodar os testes e confirmar que falham**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~EncerramentoViewModelTests"
```

Esperado: falha de build — o tipo `EncerramentoViewModel` não existe.

- [ ] **Step 4: Implementar `EncerramentoViewModel`**

Criar `backend/src/VarthexComanda.Desktop/Atendimento/EncerramentoViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Domain;

namespace VarthexComanda.Desktop.Atendimento;

public partial class EncerramentoViewModel : ObservableObject
{
    private readonly EncerrarComanda _encerrarComanda;
    private readonly IComandaRepository _comandas;
    private int _comandaId;

    public EncerramentoViewModel(EncerrarComanda encerrarComanda, IComandaRepository comandas)
    {
        _encerrarComanda = encerrarComanda;
        _comandas = comandas;
        Itens = new ObservableCollection<ItemComanda>();
    }

    public event EventHandler<bool>? Concluido;

    public ObservableCollection<ItemComanda> Itens { get; }

    [ObservableProperty]
    private int numeroComanda;

    [ObservableProperty]
    private long totalCentavos;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmarEncerrarCommand))]
    private bool cobrancaAprovada;

    [ObservableProperty]
    private string mensagem = string.Empty;

    public void Carregar(int comandaId)
    {
        _comandaId = comandaId;
        var detalhe = _comandas.BuscarComItens(comandaId);
        if (detalhe is null)
        {
            Mensagem = "Comanda não encontrada.";
            return;
        }

        NumeroComanda = detalhe.Comanda.Numero;
        TotalCentavos = detalhe.Comanda.TotalCentavos;
        Itens.Clear();
        foreach (var item in detalhe.Itens)
        {
            Itens.Add(item);
        }
    }

    [RelayCommand(CanExecute = nameof(PodeConfirmarEncerrar))]
    private void ConfirmarEncerrar()
    {
        if (!CobrancaAprovada)
        {
            return;
        }

        try
        {
            var resultado = _encerrarComanda.Executar(_comandaId);
            if (!resultado.Sucesso)
            {
                Mensagem = string.Join(" ", resultado.Erros);
                return;
            }

            Mensagem = string.Empty;
            Concluido?.Invoke(this, true);
        }
        catch (Exception)
        {
            Mensagem = "A venda não foi registrada e a comanda continua aberta.";
        }
    }

    private bool PodeConfirmarEncerrar() => CobrancaAprovada;

    [RelayCommand]
    private void Voltar() => Concluido?.Invoke(this, false);
}
```

- [ ] **Step 5: Rodar os testes e confirmar que passam**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~EncerramentoViewModelTests"
```

Esperado: PASS nos 5 testes.

- [ ] **Step 6: Rodar a suíte inteira e commitar**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

```bash
git add backend/src/VarthexComanda.Desktop/Atendimento/IEncerramentoDialog.cs backend/src/VarthexComanda.Desktop/Atendimento/EncerramentoViewModel.cs backend/tests/VarthexComanda.Desktop.Tests/Atendimento/EncerramentoViewModelTests.cs
git commit -m "feat: adiciona EncerramentoViewModel"
```

---

## Task 4: `EncerramentoView` + `EncerramentoDialog` + comando `VerTotal` + DI + verificação manual

**Files:**
- Create: `backend/src/VarthexComanda.Desktop/Atendimento/EncerramentoView.xaml`
- Create: `backend/src/VarthexComanda.Desktop/Atendimento/EncerramentoView.xaml.cs`
- Create: `backend/src/VarthexComanda.Desktop/Atendimento/EncerramentoDialog.cs`
- Create: `backend/tests/VarthexComanda.Desktop.Tests/Atendimento/FakeEncerramentoDialog.cs`
- Modify: `backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoViewModel.cs`
- Modify: `backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoView.xaml`
- Modify: `backend/src/VarthexComanda.Desktop/App.xaml.cs`
- Modify: `backend/tests/VarthexComanda.Desktop.Tests/Atendimento/AtendimentoViewModelTests.cs`

**Interfaces:**
- Consumes: `EncerramentoViewModel` (Task 3), `IEncerramentoDialog` (Task 3), `CentavosParaMoedaConverter` (já existe).
- Produces: `AtendimentoViewModel.VerTotalCommand` — nada consome depois (última peça da fatia).

- [ ] **Step 1: Escrever `FakeEncerramentoDialog` (double de teste)**

Criar `backend/tests/VarthexComanda.Desktop.Tests/Atendimento/FakeEncerramentoDialog.cs`:

```csharp
using VarthexComanda.Desktop.Atendimento;

namespace VarthexComanda.Desktop.Tests.Atendimento;

public class FakeEncerramentoDialog : IEncerramentoDialog
{
    public bool ProximaResposta { get; set; } = true;
    public int? ComandaIdRecebida { get; private set; }

    public bool Abrir(int comandaId)
    {
        ComandaIdRecebida = comandaId;
        return ProximaResposta;
    }
}
```

- [ ] **Step 2: Escrever os testes de `VerTotal` em `AtendimentoViewModelTests` (vão falhar — comando não existe)**

Em `backend/tests/VarthexComanda.Desktop.Tests/Atendimento/AtendimentoViewModelTests.cs`,
atualizar a assinatura e o corpo de `CriarViewModel` para injetar o novo
diálogo (o construtor de `AtendimentoViewModel` vai ganhar um parâmetro a
mais no Step 4):

```csharp
    private static (AtendimentoViewModel viewModel, FakeProdutoRepository produtos, FakeConfirmador confirmador, FakeEncerramentoDialog encerramentoDialog) CriarViewModel(bool confirmar = true)
    {
        var categorias = new FakeCategoriaRepository();
        var relogio = new FakeClock();
        var categoria = new CadastrarCategoria(categorias, relogio).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        new CadastrarProduto(produtos, categorias, relogio).Executar("Refrigerante", categoria.Id, 500);
        var comandas = new FakeComandaRepository();
        var confirmador = new FakeConfirmador { ProximaResposta = confirmar };
        var encerramentoDialog = new FakeEncerramentoDialog();

        var viewModel = new AtendimentoViewModel(
            new AbrirComanda(comandas, relogio),
            new AdicionarItem(comandas, produtos, relogio),
            new AlterarQuantidade(comandas, relogio),
            new RemoverItem(comandas, relogio),
            new CancelarComanda(comandas, relogio),
            comandas,
            new ListarCategoriasAtivas(categorias),
            new PesquisarProdutos(produtos),
            confirmador,
            encerramentoDialog);

        return (viewModel, produtos, confirmador, encerramentoDialog);
    }
```

Os 8 testes existentes que já chamam `CriarViewModel()` precisam desconstruir
a nova tupla de 4 elementos. Fazer uma substituição **replace_all** por
padrão (cada padrão aparece em mais de um teste — substituir todas as
ocorrências, não só a primeira):

- `var (viewModel, _, _) = CriarViewModel();` → `var (viewModel, _, _, _) = CriarViewModel();`
  (aparece 3 vezes: `Abrir_NumeroValido_CriaComandaEMostraNaGrade`,
  `Abrir_MesmoNumeroDuasVezes_SegundaFalhaSemDuplicarNaGrade`,
  `CancelarComandaAtual_LiberaNumeroNaGrade`).
- `var (viewModel, produtos, _) = CriarViewModel();` → `var (viewModel, produtos, _, _) = CriarViewModel();`
  (aparece 1 vez: `AdicionarProdutoDuasVezes_IncrementaQuantidadeEmVezDeDuplicar`).
- `var (viewModel, produtos, confirmador) = CriarViewModel();` → `var (viewModel, produtos, confirmador, _) = CriarViewModel();`
  (aparece 4 vezes: `DiminuirQuantidadeAteZero_ConfirmadorAceita_RemoveItem`,
  `DiminuirQuantidadeAteZero_ConfirmadorRecusa_ItemPermanece`,
  `Remover_ConfirmadorRecusa_ItemPermanece`,
  `CancelarComandaAtual_ConfirmadorRecusa_ComandaPermaneceAberta`).

Total: 8 chamadas ajustadas, nenhuma nova lógica de teste — só a
desconstrução da tupla.

Depois, adicionar ao final da classe (antes da última chave):

```csharp
    [Fact]
    public void VerTotal_ComandaSemItens_ComandoDesabilitado()
    {
        var (viewModel, _, _, _) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);

        Assert.False(viewModel.VerTotalCommand.CanExecute(null));
    }

    [Fact]
    public void VerTotal_DialogoConfirma_ComandaSaiDaGrade()
    {
        var (viewModel, produtos, _, encerramentoDialog) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);
        var produto = produtos.Pesquisar(null, null)[0];
        viewModel.AdicionarProdutoAoItemCommand.Execute(produto);
        var comandaId = viewModel.ComandaAtual!.Id;
        encerramentoDialog.ProximaResposta = true;

        viewModel.VerTotalCommand.Execute(null);

        Assert.Equal(comandaId, encerramentoDialog.ComandaIdRecebida);
        Assert.Null(viewModel.ComandaAtual);
        Assert.Empty(viewModel.ComandasAbertas);
    }

    [Fact]
    public void VerTotal_DialogoCancela_ComandaPermaneceAberta()
    {
        var (viewModel, produtos, _, encerramentoDialog) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);
        var produto = produtos.Pesquisar(null, null)[0];
        viewModel.AdicionarProdutoAoItemCommand.Execute(produto);
        encerramentoDialog.ProximaResposta = false;

        viewModel.VerTotalCommand.Execute(null);

        Assert.NotNull(viewModel.ComandaAtual);
        Assert.Single(viewModel.ComandasAbertas);
    }
```

- [ ] **Step 3: Rodar os testes e confirmar que falham**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~AtendimentoViewModelTests"
```

Esperado: falha de build — `AtendimentoViewModel` não tem 10 parâmetros no
construtor nem `VerTotalCommand`.

- [ ] **Step 4: Adicionar o comando `VerTotal` a `AtendimentoViewModel`**

Em `backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoViewModel.cs`,
adicionar o campo e o parâmetro de construtor (logo após `_confirmador`):

```csharp
    private readonly IConfirmador _confirmador;
    private readonly IEncerramentoDialog _encerramentoDialog;

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
        IEncerramentoDialog encerramentoDialog)
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
```

(as linhas seguintes do construtor — `ComandasAbertas = new ...` em diante —
continuam exatamente como estão).

Adicionar o novo comando logo após `FecharEdicao()`:

```csharp
    [RelayCommand(CanExecute = nameof(PodeVerTotal))]
    private void VerTotal()
    {
        if (ComandaAtual is null)
        {
            return;
        }

        if (_encerramentoDialog.Abrir(ComandaAtual.Id))
        {
            FecharEdicao();
            AtualizarComandasAbertas();
        }
    }

    private bool PodeVerTotal() => ComandaAtual is not null && Itens.Count > 0;
```

E notificar o `CanExecute` sempre que `Itens`/`ComandaAtual` mudam — adicionar
`VerTotalCommand.NotifyCanExecuteChanged();` como última linha de
`AbrirParaEdicao`, `FecharEdicao` e `AplicarResultado`:

```csharp
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
        VerTotalCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void FecharEdicao()
    {
        ComandaAtual = null;
        Itens.Clear();
        VerTotalCommand.NotifyCanExecuteChanged();
    }
```

```csharp
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
        VerTotalCommand.NotifyCanExecuteChanged();
    }
```

- [ ] **Step 5: Rodar os testes e confirmar que passam**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release --filter "FullyQualifiedName~AtendimentoViewModelTests"
```

Esperado: PASS em todos os testes (os 8 antigos ajustados + os 3 novos de
`VerTotal`).

- [ ] **Step 6: Criar `EncerramentoView.xaml`**

Criar `backend/src/VarthexComanda.Desktop/Atendimento/EncerramentoView.xaml`:

```xml
<Window x:Class="VarthexComanda.Desktop.Atendimento.EncerramentoView"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:local="clr-namespace:VarthexComanda.Desktop.Atendimento"
        mc:Ignorable="d"
        Title="Encerrar comanda" Width="480" SizeToContent="Height"
        WindowStartupLocation="CenterOwner" ResizeMode="NoResize">
    <Window.Resources>
        <local:CentavosParaMoedaConverter x:Key="Moeda" />
    </Window.Resources>
    <StackPanel Margin="16">
        <TextBlock Text="{Binding NumeroComanda, StringFormat='Comanda {0}'}" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" />

        <ListView ItemsSource="{Binding Itens}" MaxHeight="240">
            <ListView.View>
                <GridView>
                    <GridViewColumn Header="Produto" DisplayMemberBinding="{Binding NomeProduto}" Width="160" />
                    <GridViewColumn Header="Qtd" DisplayMemberBinding="{Binding Quantidade}" Width="50" />
                    <GridViewColumn Header="Preço unit." DisplayMemberBinding="{Binding PrecoUnitarioCentavos, Converter={StaticResource Moeda}}" Width="90" />
                    <GridViewColumn Header="Subtotal" DisplayMemberBinding="{Binding SubtotalCentavos, Converter={StaticResource Moeda}}" Width="90" />
                </GridView>
            </ListView.View>
        </ListView>

        <StackPanel Orientation="Horizontal" Margin="0,12,0,0">
            <TextBlock Text="Total a pagar: " FontWeight="Bold" FontSize="18" />
            <TextBlock Text="{Binding TotalCentavos, Converter={StaticResource Moeda}}" FontWeight="Bold" FontSize="18" />
        </StackPanel>
        <TextBlock Text="Digite este valor na maquininha." Margin="0,4,0,12" TextWrapping="Wrap" />

        <CheckBox Content="A cobrança foi aprovada fora do sistema?" IsChecked="{Binding CobrancaAprovada}" Margin="0,0,0,12" />

        <TextBlock Text="{Binding Mensagem}" Foreground="Red" TextWrapping="Wrap" Margin="0,0,0,12" />

        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
            <Button Content="Voltar para a comanda" Command="{Binding VoltarCommand}" Margin="0,0,8,0" />
            <Button Content="Confirmar e encerrar" Command="{Binding ConfirmarEncerrarCommand}" FontWeight="Bold" />
        </StackPanel>
    </StackPanel>
</Window>
```

- [ ] **Step 7: Criar `EncerramentoView.xaml.cs`**

Criar `backend/src/VarthexComanda.Desktop/Atendimento/EncerramentoView.xaml.cs`:

```csharp
using System.Windows;

namespace VarthexComanda.Desktop.Atendimento;

public partial class EncerramentoView : Window
{
    public EncerramentoView(EncerramentoViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Concluido += (sender, sucesso) =>
        {
            DialogResult = sucesso;
            Close();
        };
    }
}
```

- [ ] **Step 8: Criar `EncerramentoDialog` (implementação de `IEncerramentoDialog`)**

Criar `backend/src/VarthexComanda.Desktop/Atendimento/EncerramentoDialog.cs`:

```csharp
using System.Windows;

namespace VarthexComanda.Desktop.Atendimento;

public class EncerramentoDialog : IEncerramentoDialog
{
    private readonly Func<EncerramentoViewModel> _fabricaViewModel;

    public EncerramentoDialog(Func<EncerramentoViewModel> fabricaViewModel)
    {
        _fabricaViewModel = fabricaViewModel;
    }

    public bool Abrir(int comandaId)
    {
        var viewModel = _fabricaViewModel();
        viewModel.Carregar(comandaId);
        var view = new EncerramentoView(viewModel) { Owner = Application.Current.MainWindow };
        return view.ShowDialog() == true;
    }
}
```

- [ ] **Step 9: Adicionar o botão "Ver total" em `AtendimentoView.xaml`**

Em `backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoView.xaml`, no
painel de itens (`Grid.Row="1" Grid.Column="2"`), adicionar o botão logo
**acima** do bloco do total (antes de `<StackPanel Orientation="Horizontal" Margin="0,12,0,0">`
que mostra "Total: "), separado dele por espaço próprio — substituir:

```xml
                <StackPanel Orientation="Horizontal" Margin="0,12,0,0">
                    <TextBlock Text="Total: " FontWeight="Bold" FontSize="16" />
                    <TextBlock Text="{Binding ComandaAtual.TotalCentavos, Converter={StaticResource Moeda}}" FontWeight="Bold" FontSize="16" />
                </StackPanel>
```

por:

```xml
                <StackPanel Orientation="Horizontal" Margin="0,12,0,0">
                    <TextBlock Text="Total: " FontWeight="Bold" FontSize="16" />
                    <TextBlock Text="{Binding ComandaAtual.TotalCentavos, Converter={StaticResource Moeda}}" FontWeight="Bold" FontSize="16" />
                </StackPanel>
                <Button Content="Ver total e encerrar" Command="{Binding VerTotalCommand}"
                        FontWeight="Bold" Padding="8,4" Margin="0,8,0,0" HorizontalAlignment="Left" />
```

- [ ] **Step 10: Registrar tudo no DI (`App.xaml.cs`)**

Em `backend/src/VarthexComanda.Desktop/App.xaml.cs`, adicionar logo após
`services.AddTransient<IConfirmador, MessageBoxConfirmador>();`:

```csharp
        services.AddTransient<EncerrarComanda>();
        services.AddTransient<EncerramentoViewModel>();
        services.AddTransient<Func<EncerramentoViewModel>>(sp => () => sp.GetRequiredService<EncerramentoViewModel>());
        services.AddTransient<IEncerramentoDialog, EncerramentoDialog>();
```

Nenhum `using` novo é necessário — `EncerrarComanda` já resolve por
`VarthexComanda.Application.Atendimento` (já importado), `EncerramentoViewModel`/
`IEncerramentoDialog`/`EncerramentoDialog` já resolvem por
`VarthexComanda.Desktop.Atendimento` (já importado).

- [ ] **Step 11: Build completo e suíte inteira**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet build backend/VarthexComanda.slnx
```

Esperado: build limpo (0 erros).

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

Esperado: todos os projetos PASS.

- [ ] **Step 12: Verificação manual na aplicação real**

Rodar a aplicação:

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet run --project backend/src/VarthexComanda.Desktop
```

Roteiro manual (mesmo padrão de verificação usado nas Etapas 2-3):

1. Abrir uma comanda nova, lançar 2 itens.
2. Confirmar que o botão "Ver total e encerrar" está habilitado e clicar.
3. Na janela modal: confirmar que aparecem os itens com preço unitário e
   subtotal corretos, o total bate com a comanda, e o botão "Confirmar e
   encerrar" está **desabilitado** até marcar o checkbox.
4. Clicar "Voltar para a comanda" — a janela fecha, a comanda continua aberta
   com os mesmos itens.
5. Reabrir "Ver total e encerrar", marcar o checkbox, clicar "Confirmar e
   encerrar" — a janela fecha, a comanda some da grade de abertas.
6. Abrir uma comanda nova sem itens — confirmar que "Ver total e encerrar"
   está **desabilitado**.
7. Repetir o encerramento em uma segunda comanda e confirmar (via logs ou
   inspeção do banco, se necessário) que os números de venda saem
   sequenciais.

- [ ] **Step 13: Commitar**

```bash
git add backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoViewModel.cs backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoView.xaml backend/src/VarthexComanda.Desktop/Atendimento/EncerramentoView.xaml backend/src/VarthexComanda.Desktop/Atendimento/EncerramentoView.xaml.cs backend/src/VarthexComanda.Desktop/Atendimento/EncerramentoDialog.cs backend/src/VarthexComanda.Desktop/App.xaml.cs backend/tests/VarthexComanda.Desktop.Tests/Atendimento/AtendimentoViewModelTests.cs backend/tests/VarthexComanda.Desktop.Tests/Atendimento/FakeEncerramentoDialog.cs
git commit -m "feat: adiciona tela de encerramento e comando VerTotal"
```

---

## Task 5: Verificação final e changelog

**Files:**
- Modify: `docs/CHANGELOG.md`

**Interfaces:**
- Consumes: nada novo — só confirma o estado final da fatia.
- Produces: nada — última tarefa do plano.

- [ ] **Step 1: Rodar a suíte inteira e conferir a contagem de testes**

```bash
export PATH="$PATH:/c/Program Files/dotnet" && dotnet test backend/VarthexComanda.slnx --configuration Release
```

Contagem esperada por projeto (partindo de 82 ao final da Etapa 3): Domain 4
(sem mudança), Application 37 + 4 (`EncerrarComandaTests`) = 41,
Infrastructure 31 + 4 (`EfComandaRepositoryTests` novos) = 35, Desktop.Tests
10 + 5 (`EncerramentoViewModelTests`) + 3 (`VerTotal` em
`AtendimentoViewModelTests`) = 18. **Total esperado: 98.**

- [ ] **Step 2: Atualizar o changelog**

Em `docs/CHANGELOG.md`, adicionar uma nova seção no topo (mesmo formato das
entradas anteriores — verificar o cabeçalho exato das seções `1.4`/`1.5`/`1.6`
existentes antes de escrever, para manter o estilo):

```markdown
## 1.7 - 2026-09-18

Encerramento e venda (Etapa 4, RF14-17): tela de encerramento exibindo
itens, preços e total a pagar; confirmação manual de cobrança aprovada fora
do sistema; encerramento grava a venda e fecha a comanda em uma única
transação (RN13-15); número da comanda é liberado após o fechamento; comanda
vazia não pode ser encerrada (RN09).
```

- [ ] **Step 3: Commitar**

```bash
git add docs/CHANGELOG.md
git commit -m "docs: registra encerramento e venda no changelog"
```
