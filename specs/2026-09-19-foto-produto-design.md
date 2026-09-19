# Foto do produto

Data: 2026-09-19
Escopo: permitir cadastrar, trocar e remover uma foto por produto, e
exibi-la nos cards do menu da tela de Atendimento (e num preview na tela
de Produtos).

## Contexto

O redesign da tela de Atendimento (1.11) deixou um espaço de foto em cada
card do menu, hoje preenchido por um bloco cinza genérico — `Produto` não
tem campo de imagem. Esta fatia fecha essa lacuna, que foi deixada de fora
de propósito naquela fatia.

## Decisões confirmadas com o usuário

- **Backup das fotos fica para uma fatia futura.** O backup continua
  copiando só o `.db`. Consequência documentada: o banco guarda só o nome
  do arquivo da foto; restaurar um backup na mesma máquina mantém as fotos
  (os arquivos continuam na pasta), mas um backup restaurado em outra
  máquina traz os produtos sem as imagens (o app mostra o placeholder, nunca
  falha).
- **Operador pode escolher/trocar e remover a foto** ("Escolher foto..." e
  "Remover foto") no formulário do produto.

## Decisões técnicas

**Onde a foto vive:** o app **copia** a imagem escolhida para
`%LOCALAPPDATA%\VarthexComanda\fotos\` (nova `AppPaths.FotosDirectory`)
com um nome único (`<guid>.<ext>`, extensão em minúsculas). O arquivo
original do operador nunca é tocado. Assim a foto não some se o original
for movido/apagado.

**O que o banco guarda:** só o **nome do arquivo** (`Produto.FotoArquivo`,
`string?`, coluna `foto_arquivo`, nula por padrão) — nunca o caminho
absoluto. O caminho é resolvido contra `AppPaths.FotosDirectory` na hora de
exibir; o banco fica independente de onde o app está instalado.

**Formatos e limite:** `.jpg`, `.jpeg`, `.png`, `.bmp`; até 10 MB. Validado
no armazenamento; violações viram `Resultado.Falha` com mensagem clara
(mesmo padrão de `NumeroComandaOcupadoException` em `AbrirComanda`).

**Trocar/remover apaga o arquivo antigo** da pasta de fotos (limpeza
imediata, sem lixo acumulado). Falha ao apagar é registrada em log
(Serilog) e não derruba a operação.

**Exibição sem travar o arquivo:** conversor XAML carrega a imagem com
`BitmapCacheOption.OnLoad` + `Freeze()` + `DecodePixelWidth = 320`. O
`OnLoad` solta o arquivo logo após carregar — sem isso o preview travaria o
arquivo e "Trocar/Remover" não conseguiria apagá-lo no Windows. Imagem
ausente ou corrompida → o conversor devolve `null` e o placeholder cinza
aparece (degradação intencional, sem exceção na tela).

**Conversor precisa do diretório de fotos:** conversores XAML são
instanciados sem parâmetros; o diretório vem de uma propriedade estática
`FotoArquivoParaImagemConverter.DiretorioFotos`, definida uma vez em
`App.OnStartup`. É estado global de apresentação, aceito de propósito para
não empurrar `AppPaths` pelo construtor de `AtendimentoViewModel` (e pelos
~8 pontos de construção dele nos testes).

**Fluxo na tela de Produtos:** os botões de foto valem só para um produto
já salvo e selecionado (mesmo modelo do botão "Desativar", que também age
na hora). Para um produto novo: salvar primeiro, depois selecioná-lo na
lista e escolher a foto. Limitação conhecida — o formulário limpa a seleção
após salvar (comportamento existente, coberto por teste), então o operador
precisa clicar no produto recém-criado.

## Arquitetura (domínio-por-pasta)

- Domain: `Produto.FotoArquivo` (`string?`, não `required`).
- Application/Catalogo: `IFotoStorage` (`Importar`, `Excluir`),
  `FotoInvalidaException`, casos de uso `DefinirFotoProduto` e
  `RemoverFotoProduto`.
- Infrastructure: mapeamento + migração `AddProdutoFoto`;
  `Storage/ArquivoFotoStorage` (implementa `IFotoStorage`);
  `AppPaths.FotosDirectory`.
- Desktop: `ProdutosViewModel` (`FotoArquivoAtual`, `DefinirFoto(caminho)`,
  `RemoverFotoCommand`), `FotoArquivoParaImagemConverter`, XAML de
  Produtos (preview + botões) e dos cards do menu (imagem sobre o
  placeholder). O `OpenFileDialog` fica no code-behind, fora do ViewModel.

## Fora de escopo

- Incluir a pasta de fotos no backup/restauração.
- Redimensionar/recortar a imagem ao importar (o `DecodePixelWidth` só
  reduz o custo de exibição; o arquivo guardado é a cópia integral).
- Foto no cadastro de um produto ainda não salvo.
- Miniatura na lista de produtos.

## Critérios de pronto

- `dotnet build`/`dotnet test` limpos;
- escolher uma foto para um produto existente mostra o preview na tela de
  Produtos e a foto no card do menu do Atendimento;
- trocar a foto apaga o arquivo antigo da pasta de fotos; remover volta ao
  placeholder e apaga o arquivo;
- arquivo de formato inválido/inexistente/grande demais mostra a mensagem
  de erro e não altera o produto;
- produto sem foto (ou com foto ausente/corrompida) continua exibindo o
  placeholder sem erro.
