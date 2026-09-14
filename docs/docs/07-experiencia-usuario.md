# Experiência do usuário

## Navegação

| Área | Objetivo | Elementos essenciais |
| --- | --- | --- |
| Atendimento | Abrir e lançar comandas | Grade, busca, categorias, produtos, itens e total |
| Produtos | Manter catálogo | Lista, filtros, formulário, preço, categoria e situação |
| Encerramento | Cobrar fora do aplicativo | Itens, subtotais, total destacado, aviso e confirmação |
| Histórico | Consultar vendas | Data, número, total, horário e detalhe |
| Resumo | Acompanhar o dia | Quantidade, total e ticket médio |
| Configurações | Ajustar operação | Estabelecimento, comandas, backup e retenção |

## Tela inicial

- abrir diretamente na grade de comandas;
- mostrar livres e ocupadas com texto, ícone e cor;
- permitir digitar o número para localizar;
- não depender apenas de cor;
- manter comandas mais usadas sem rolagem excessiva.

## Tela da comanda

- catálogo por categoria;
- busca por nome;
- botões grandes com nome e preço;
- painel de itens com quantidade, preço e subtotal;
- controles `+`, `-` e remover;
- total sempre visível;
- ações `Cancelar comanda` e `Ver total` visualmente separadas.

## Tela de encerramento

Ordem visual obrigatória:

1. número da comanda;
2. lista de produtos;
3. quantidades, preços e subtotais;
4. total a pagar em destaque;
5. texto: `Digite este valor na maquininha`;
6. botão `Voltar para a comanda`;
7. confirmação: `A cobrança foi aprovada fora do sistema?`;
8. botão principal `Confirmar e encerrar`.

O sistema não deve mostrar botões Dinheiro, Pix, Débito ou Crédito.

## Mensagens

| Situação | Mensagem | Ação |
| --- | --- | --- |
| Nenhum produto | Cadastre ao menos um produto ativo | Ir para Produtos |
| Comanda vazia | Adicione um item antes de encerrar | Voltar ao catálogo |
| Cobrança não concluída | A comanda continua aberta e nenhum pagamento foi registrado | Voltar à comanda |
| Falha ao encerrar | A venda não foi registrada e a comanda continua aberta | Tentar novamente |
| Sem vendas | Ainda não há vendas concluídas nesta data | Alterar data |
| Backup inacessível | Não foi possível acessar a pasta de backup | Escolher outra pasta |

## Atalhos mínimos

| Ação | Sugestão |
| --- | --- |
| Buscar produto | `Ctrl+F` |
| Aumentar quantidade | `+` |
| Diminuir quantidade | `-` |
| Ver total | `F4` |
| Voltar | `Esc` |
| Confirmar diálogo | `Enter` |

Os atalhos finais devem ser validados no equipamento e não podem conflitar com o sistema operacional.

## Comportamento no Windows

- abrir apenas uma instância do Varthex Comanda por sessão;
- exibir aviso simples quando o usuário tentar abrir uma segunda instância;
- preservar legibilidade com escala de exibição entre 100% e 200%;
- não depender de permissões de administrador para a operação diária;
- mostrar as comandas abertas normalmente após reinício inesperado;
- manter foco e ordem de tabulação previsíveis em todas as ações essenciais.

A resolução, escala e eventual uso de tela sensível ao toque devem ser confirmados no computador da lanchonete durante a homologação.
