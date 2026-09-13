# Contratos da aplicação

Os nomes são sugestões. A implementação pode adaptá-los sem alterar comportamento.

## Tipos de valor

### Dinheiro

```text
Dinheiro
  centavos: inteiro >= 0
  somar(outro): Dinheiro
  multiplicar(quantidade inteira): Dinheiro
  formatarBRL(): texto
```

Não usar `float` ou `double` para dinheiro.

### Quantidade

```text
Quantidade
  valor: inteiro > 0
```

## Repositórios

```text
CategoriaRepository
  salvar(categoria)
  buscarPorId(id)
  listarAtivas()
  existeNome(nome, ignorarId?)

ProdutoRepository
  salvar(produto)
  buscarPorId(id)
  listarAtivosPorCategoria(categoriaId)
  pesquisarAtivos(texto)

ComandaRepository
  salvar(comanda)
  buscarAbertaPorNumero(numero)
  listarNumerosOcupados()
  buscarComItens(id)

VendaRepository
  salvar(venda)
  buscarPorId(id)
  existePorComanda(comandaId)
  listarPorPeriodo(inicio, fim)

BackupRegistroRepository
  salvar(registro)
  listarRecentes(limite)
```

## Casos de uso

```text
AbrirComanda.executar(numero) -> ComandaResumo
AdicionarItem.executar(comandaId, produtoId, quantidade) -> ComandaDetalhe
AlterarQuantidade.executar(itemId, quantidade) -> ComandaDetalhe
RemoverItem.executar(itemId) -> ComandaDetalhe
CancelarComanda.executar(comandaId) -> void
PrepararEncerramento.executar(comandaId) -> ResumoCobrancaManual
EncerrarComanda.executar(comandaId, confirmacaoExterna) -> VendaDetalhe
ConsultarHistorico.executar(periodo) -> lista de VendaResumo
CriarBackup.executar(destino) -> ResultadoBackup
RestaurarBackup.executar(arquivo, confirmacao) -> ResultadoRestauracao
```

## Resumo de cobrança manual

```text
ResumoCobrancaManual
  comandaId
  numeroComanda
  itens[]
    nome
    quantidade
    precoUnitarioCentavos
    subtotalCentavos
  totalCentavos
  instrucao = "Digite este valor na maquininha"
```

Não adicionar forma de pagamento a esse contrato no MVP.

## Encerramento transacional

```text
iniciar transação
  carregar comanda e itens
  exigir status ABERTA
  exigir pelo menos um item
  recalcular total
  exigir confirmação externa verdadeira
  exigir que não exista venda para a comanda
  criar venda com total recalculado
  alterar comanda para FECHADA
  definir fechada_em
confirmar transação
```

Qualquer exceção executa rollback.

## Erros de domínio

| Código | Situação |
| --- | --- |
| COMANDA_NUMERO_OCUPADO | Já existe comanda aberta com o número |
| COMANDA_NAO_ENCONTRADA | Identificador inexistente |
| COMANDA_NAO_ABERTA | Operação exige status aberta |
| COMANDA_VAZIA | Encerramento sem itens |
| PRODUTO_INATIVO | Inclusão de produto não disponível |
| QUANTIDADE_INVALIDA | Quantidade não inteira ou menor que um |
| CONFIRMACAO_EXTERNA_AUSENTE | Encerramento sem confirmação humana |
| VENDA_DUPLICADA | Já existe venda para a comanda |
| BACKUP_INVALIDO | Arquivo incompatível ou corrompido |

Mensagens de interface devem traduzir esses códigos para linguagem simples.

