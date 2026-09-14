# Casos de uso

## UC01 Manter categorias e produtos

**Ator:** responsável pelo negócio.

**Pré-condição:** aplicativo iniciado e banco disponível.

**Fluxo principal:**

1. Abrir Produtos.
2. Cadastrar ou selecionar categoria.
3. Informar nome, preço, categoria e situação.
4. Validar campos.
5. Confirmar e atualizar o catálogo.

**Alternativas:** nome vazio ou preço inválido impedem gravação. Registro com histórico é desativado, não excluído.

## UC02 Abrir comanda

**Ator:** atendente.

**Pré-condição:** número sem comanda aberta.

1. Abrir Comandas.
2. Selecionar número livre.
3. Confirmar abertura.
4. Registrar data, hora e status `ABERTA`.
5. Exibir catálogo.

Se outro processo ocupar o número, recusar e atualizar a grade.

## UC03 Registrar item

**Ator:** atendente.

1. Selecionar categoria ou pesquisar produto.
2. Selecionar produto ativo.
3. Copiar nome e preço atuais para o item.
4. Adicionar uma unidade ou incrementar item equivalente.
5. Recalcular subtotal e total.

Produto desativado durante a operação não é incluído.

## UC04 Corrigir itens

**Ator:** atendente.

1. Selecionar item de comanda aberta.
2. Aumentar, diminuir ou informar quantidade.
3. Remover quando necessário.
4. Confirmar ação destrutiva.
5. Recalcular e persistir o total.

Quantidade fracionária ou negativa é recusada. Quantidade zero conduz à remoção confirmada.

## UC05 Exibir total e encerrar comanda

**Ator:** caixa.

**Pré-condição:** comanda aberta com itens e total positivo.

1. Localizar a comanda.
2. Revalidar itens, preços, subtotais e total.
3. Mostrar o detalhamento e destacar o total a pagar.
4. Orientar o operador a digitar o total manualmente na maquininha.
5. O operador realiza e aguarda a cobrança externa.
6. Se não aprovada, voltar sem alterar a comanda.
7. Se aprovada, o operador confirma o encerramento.
8. Em uma transação, criar venda e mudar a comanda para `FECHADA`.
9. Liberar o número.

**Pós-condição:** venda contém apenas referência da comanda, número, total, horário e status. Não contém dados do pagamento.

## UC06 Cancelar comanda

**Ator:** atendente ou caixa.

1. Abrir comanda.
2. Solicitar cancelamento.
3. Mostrar itens e efeito da ação.
4. Confirmar.
5. Marcar `CANCELADA` e liberar número.

Comanda fechada não pode ser cancelada no MVP.

## UC07 Consultar histórico e resumo

**Ator:** responsável pelo negócio.

1. Selecionar data ou período permitido.
2. Listar vendas concluídas.
3. Abrir detalhes de uma venda.
4. Exibir itens, total e horário.
5. Calcular quantidade, total e ticket médio.

Sem vendas, mostrar estado vazio e valores iguais a zero.

## UC08 Criar backup

**Ator:** responsável ou rotina automática.

1. Criar snapshot consistente.
2. Gravar arquivo com data, hora e versão.
3. Validar integridade.
4. Calcular checksum.
5. Registrar resultado e aplicar retenção.

Falha preserva backups anteriores e não bloqueia o atendimento.

## UC09 Restaurar backup

**Ator:** responsável ou suporte.

1. Impedir nova operação durante a restauração.
2. Selecionar arquivo.
3. Validar formato, versão e integridade.
4. Mostrar data e consequência.
5. Confirmar.
6. Criar cópia preventiva da base atual.
7. Substituir base e reiniciar.
8. Verificar abertura e integridade.

Arquivo incompatível ou corrompido é recusado sem alterar a base ativa.

## UC10 Recuperar atendimento interrompido

**Ator:** atendente ou caixa.

**Pré-condição:** o aplicativo foi encerrado com uma ou mais comandas abertas já persistidas.

1. Iniciar novamente o aplicativo.
2. Validar a estrutura e a integridade básica do banco.
3. Localizar comandas com status `ABERTA`.
4. Exibir os números como ocupados e restaurar itens e totais.
5. Permitir a continuidade normal do atendimento.

O aplicativo não cria venda, não fecha comanda e não repete a última alteração. Se a base falhar na validação, a aplicação bloqueia gravações e orienta a recuperação segura.
