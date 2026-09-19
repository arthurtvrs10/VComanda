<div align="center">

# Varthex Comanda

![Plataforma](https://img.shields.io/badge/plataforma-Windows-blue)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![WPF](https://img.shields.io/badge/UI-WPF-5C2D91)
![SQLite](https://img.shields.io/badge/banco-SQLite-003B57)
![Testes](https://img.shields.io/badge/testes-240%20passando-brightgreen)
![Licen%C3%A7a](https://img.shields.io/badge/licen%C3%A7a-n%C3%A3o%20definida-lightgrey)

**Um aplicativo desktop para Windows que controla o consumo por comandas
numeradas: você lança os itens, ele calcula o total e mostra o valor para você
digitar na maquininha — sem nunca tocar em pagamento.**

</div>

Pensado para o balcão de um pequeno negócio: uma grade com todas as comandas
(livres ou abertas), um menu de produtos com foto e um carrinho em tabela, tudo
com botões grandes o bastante para o toque. Funciona 100% offline, com os dados
num banco SQLite local.

## Instalação

Ainda **não há instalador**. O app roda a partir do código, com o SDK do .NET 10
instalado no Windows:

```sh
dotnet run --project backend/src/VarthexComanda.Desktop
```

A publicação autocontida (`win-x64`) e o instalador estão no roteiro — veja
[Próximos passos](#próximos-passos) e
[Plataforma Windows e .NET](docs/docs/19-plataforma-windows-dotnet.md).

## O que ele faz

| Tela | O que resolve |
|---|---|
| **Atendimento** | Grade fixa de comandas numeradas (20 por padrão, configurável). Cada card mostra se está **livre** ou **aberta**, há quanto tempo e o total parcial. Clicar abre a comanda; dentro dela, o **menu** (cards de produto com foto, filtrados por categoria) fica à esquerda e o **carrinho** em tabela à direita, com `−` / `+` / `Excluir` por linha. |
| **Encerramento** | **Finalizar comanda (F4)** mostra o total para digitar na maquininha e só encerra depois que você confirma que a cobrança foi aprovada fora do sistema. Se der erro na maquininha, a comanda continua aberta. |
| **Produtos** | Cadastro em dois modos (**novo** / **edição**) com foto, nome, categoria e preço. Desativar em vez de apagar, para não quebrar o histórico. |
| **Histórico** | Vendas por dia, busca por número, detalhe dos itens e resumo do dia (quantidade, total e ticket médio). |
| **Backup** | Cópia automática na primeira abertura do dia e ao fechar o app, cópia manual (inclusive numa pasta externa), validação e restauração. |
| **Configurações** | Nome do estabelecimento, quantidade de comandas e pasta de backup externa. |

### Foto do produto

Cada produto pode ter uma foto (JPG, PNG ou BMP, até 10 MB). O app **copia** a
imagem para a própria pasta de dados — o arquivo original nunca é alterado — e o
banco guarda só o nome do arquivo. Arquivos que não são imagem de verdade são
recusados antes de qualquer cópia, e trocar ou remover a foto apaga o arquivo
antigo.

### Backup e recuperação

- Usa a API de snapshot nativa do SQLite (nunca uma cópia crua do arquivo), com
  checagem de integridade e checksum SHA-256.
- Mantém as **30 cópias mais recentes** na pasta gerenciada.
- **Restaurar** valida formato, versão do esquema, integridade e checksum, faz
  uma cópia preventiva do banco atual antes de tocar em qualquer coisa e reinicia
  o aplicativo ao terminar.

## Onde ficam os dados

Tudo em `%LOCALAPPDATA%\VarthexComanda\`:

| Pasta | Conteúdo |
|---|---|
| `data\` | o banco `varthex-comanda.db` |
| `backups\` | cópias gerenciadas (`.db` + `.sha256`) |
| `fotos\` | fotos dos produtos |
| `logs\` | logs técnicos (Serilog), sem dados sensíveis |

Valores são guardados em **centavos** e datas em **UTC**; a tela converte para o
horário de Brasília (UTC−3, fixo) só na exibição. Só uma instância do app roda
por vez.

## O que ele não faz

Não processa pagamentos, não integra com a maquininha e não registra forma de
pagamento, valor recebido, troco, cartão ou autorização da adquirente. É de
propósito: o operador digita o total na maquininha, e o sistema só guarda que a
comanda foi encerrada.

## Compilando

Requer Windows e o [SDK do .NET 10](https://dotnet.microsoft.com/download).

```sh
dotnet build backend/VarthexComanda.slnx
dotnet test  backend/VarthexComanda.slnx --configuration Release
dotnet run --project backend/src/VarthexComanda.Desktop
```

O banco é criado e migrado sozinho na primeira execução; se houver migrações
pendentes, o app faz uma cópia preventiva antes de aplicá-las.

## Arquitetura

Camadas com dependência sempre para dentro, e cada capacidade de negócio em sua
própria pasta/namespace em todas as camadas (`Atendimento`, `Catalogo`,
`Backup`, `Configuracao`):

```
Desktop (WPF, MVVM)  →  Application (casos de uso, Resultado<T>)  →  Domain (entidades)
        └────────────→  Infrastructure (EF Core/SQLite, arquivos, backup)  ──┘
```

| Camada | Papel |
|---|---|
| `VarthexComanda.Domain` | Entidades puras, sem dependências |
| `VarthexComanda.Application` | Casos de uso e interfaces de repositório; erros esperados viram `Resultado<T>` |
| `VarthexComanda.Infrastructure` | Repositórios EF Core, armazenamento de fotos, motor de backup, logs |
| `VarthexComanda.Desktop` | Telas WPF e ViewModels (sem `System.Windows` nos ViewModels) |

Cada operação que muda dados abre um único contexto e salva uma única vez — a
atomicidade vem da construção, sem camada de unit-of-work. Os testes (xUnit)
ficam em `backend/tests/`, um projeto por camada.

## Documentação

A documentação em [`docs/`](docs/README.md) é a fonte de verdade (requisitos
`RF`, regras `RN`, casos de uso `UC` e testes de aceitação `CT`):

- [Visão e decisões](docs/docs/00-visao-e-decisoes.md) e
  [Escopo do MVP](docs/docs/01-escopo-mvp.md)
- [Guia de implementação](docs/docs/14-guia-implementacao.md)
- [Histórico de alterações](docs/CHANGELOG.md)
- [`specs/`](specs/): o design e o plano de cada fatia entregue
- [AGENTS.md](docs/AGENTS.md): leia antes de qualquer alteração assistida por IA

## Status

Etapas de catálogo, comandas, encerramento, histórico, backup/recuperação e
configurações estão entregues e cobertas por testes automatizados.

### Próximos passos

- Publicação autocontida e instalador limpo (RNF09 / RNF17).
- Navegação completa por teclado com foco visível (RNF18); hoje só o **F4** existe.
- Rotação e limite de tamanho dos logs (RNF21).
- Incluir as fotos no backup e na restauração.
- Homologação no computador real (CT01–CT22, teste sem internet, treinamento).

### Limitações conhecidas

- Fotos ficam fora do backup: restaurar num computador novo traz os produtos,
  mas as imagens aparecem como o cinza padrão até serem escolhidas de novo.
- Sem instalador, quem usa precisa do SDK do .NET 10 para rodar o app.
- A versão e a arquitetura exatas do Windows-alvo ainda estão pendentes
  (QV07 em [pendências](docs/docs/13-pendencias-validacao.md)).

## Licença

Nenhuma licença definida. Repositório privado até decisão do proprietário.
