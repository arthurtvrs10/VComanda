# Escopo do MVP

## Incluído

- cadastro, alteração e desativação de categorias;
- cadastro, alteração e desativação de produtos;
- grade de números livres e ocupados;
- abertura de comanda;
- inclusão e remoção de itens;
- alteração de quantidade;
- cópia do nome e preço do produto para o item;
- cálculo automático de subtotal e total;
- tela itemizada de total a pagar;
- orientação para digitação manual na maquininha;
- retorno à comanda quando a cobrança externa não for concluída;
- confirmação manual e encerramento da comanda;
- histórico de vendas encerradas;
- resumo diário com quantidade, total e ticket médio;
- cancelamento de comanda aberta;
- backup automático e manual;
- validação e restauração de backup;
- logs técnicos locais;
- funcionamento offline em um computador.

## Fora do MVP

| Tema | Motivo |
| --- | --- |
| Processamento e registro de pagamento | A cobrança será externa e manual |
| Integração com maquininha | Depende de adquirente, terminal, internet e condições comerciais |
| Forma de pagamento e troco | O aplicativo não controla o recebimento |
| Abertura, fechamento, sangria e suprimento de caixa | Exige módulo financeiro adicional |
| Estoque e ficha técnica | Exige insumos, perdas e inventário |
| Tela de cozinha | Exige outro terminal e rede local |
| Usuários e permissões | Não é essencial no terminal único inicial |
| Impressão | Depende do equipamento e da rotina |
| Emissão fiscal | Depende de legislação, contador e fornecedor especializado |
| Delivery, fidelidade e CRM | Não resolvem o problema prioritário |
| Nuvem e sincronização | A operação deve permanecer independente da internet |

## Critério de conclusão

O MVP estará concluído quando, em um computador sem internet, um operador conseguir:

1. cadastrar categorias e produtos;
2. abrir uma comanda numerada;
3. adicionar, remover e corrigir itens;
4. visualizar produtos, quantidades, preços, subtotais e total;
5. digitar o total manualmente na maquininha;
6. manter a comanda aberta se a cobrança externa falhar;
7. encerrar a comanda após confirmação externa;
8. consultar a venda no histórico e no resumo diário;
9. reiniciar o computador sem perder dados;
10. criar, validar e restaurar um backup em ambiente de teste.

## Regra de controle de escopo

Uma nova funcionalidade somente entra no MVP se for necessária para registrar o consumo, calcular o total, preservar o histórico ou impedir perda crítica de dados. Toda mudança deve atualizar requisitos, testes e rastreabilidade.

