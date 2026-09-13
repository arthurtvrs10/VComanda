# Histórias de usuário

## Catálogo

### US01 Cadastrar categoria

Como responsável, quero cadastrar categorias para organizar os produtos na tela de atendimento.

**Aceite:** nome obrigatório, unicidade sem diferenciar maiúsculas e situação ativa.

### US02 Cadastrar produto

Como responsável, quero cadastrar produto com preço para que ele possa ser lançado na comanda.

**Aceite:** categoria ativa, nome obrigatório e preço positivo em centavos.

### US03 Alterar produto sem perder histórico

Como responsável, quero alterar preço ou desativar produto sem mudar vendas anteriores.

**Aceite:** itens existentes mantêm o snapshot original.

## Atendimento

### US04 Abrir comanda

Como atendente, quero abrir um número livre para iniciar o registro do consumo.

**Aceite:** duas comandas abertas não usam o mesmo número.

### US05 Registrar produto rapidamente

Como atendente, quero adicionar produto frequente em poucos cliques para não atrasar o balcão.

**Aceite:** até dois cliques após abrir a comanda e total atualizado.

### US06 Corrigir consumo

Como atendente, quero alterar quantidade ou remover item para corrigir erros antes do encerramento.

**Aceite:** somente comanda aberta, quantidade inteira positiva e remoção confirmada.

## Encerramento

### US07 Consultar total

Como caixa, quero ver todos os itens e o total para digitar o valor correto na maquininha.

**Aceite:** produto, quantidade, preço, subtotal e total visíveis.

### US08 Manter comanda aberta

Como caixa, quero voltar quando a cobrança externa não for concluída para não registrar uma venda incorreta.

**Aceite:** nenhuma alteração, venda ou pagamento é gravado.

### US09 Encerrar após confirmação

Como caixa, quero confirmar que a cobrança foi aprovada para fechar a comanda e liberar o número.

**Aceite:** venda e comanda gravadas atomicamente; nenhum dado de pagamento.

## Gestão

### US10 Consultar histórico

Como responsável, quero consultar vendas e itens por data para acompanhar o movimento.

**Aceite:** snapshot, total e horário corretos.

### US11 Consultar resumo

Como responsável, quero visualizar quantidade, total e ticket médio do dia.

**Aceite:** somente vendas concluídas entram no cálculo.

## Continuidade

### US12 Criar backup

Como responsável, quero uma cópia validada para recuperar dados se o computador falhar.

**Aceite:** arquivo datado, integridade válida e checksum.

### US13 Restaurar backup

Como suporte, quero restaurar uma cópia sem destruir a base atual em caso de arquivo inválido.

**Aceite:** validação anterior, cópia preventiva e substituição atômica.

