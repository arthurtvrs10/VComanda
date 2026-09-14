# Visão e decisões

## Problema

A lanchonete não registra formalmente o consumo durante o atendimento. No caixa, o cliente relata o que consumiu e o valor é calculado naquele momento. O processo depende de memória, boa-fé e conferência manual, causando risco de itens esquecidos, preços incorretos, demora e ausência de histórico gerencial.

## Solução proposta

O **Varthex Comanda**, aplicativo desktop executado em um único computador Windows, permitirá:

- cadastrar categorias e produtos;
- abrir comandas numeradas;
- registrar e corrigir itens durante o consumo;
- calcular subtotais e total;
- mostrar o total que deverá ser digitado manualmente na maquininha;
- encerrar a comanda após confirmação externa do operador;
- consultar histórico e resumo diário;
- criar e restaurar backups.

## Objetivos

| Código | Objetivo | Indicador inicial |
| --- | --- | --- |
| OBJ01 | Registrar o consumo no momento da entrega | Pelo menos 95% dos itens lançados antes do encerramento |
| OBJ02 | Agilizar a cobrança manual | Comanda localizada com itens e total visíveis em até 10 segundos |
| OBJ03 | Preservar histórico | 100% das vendas encerradas consultáveis localmente |
| OBJ04 | Evitar perda total de dados | Backup automático válido e restauração testada |
| OBJ05 | Manter custo operacional baixo | Nenhuma infraestrutura remota obrigatória |

## Decisões confirmadas

| Código | Decisão | Consequência |
| --- | --- | --- |
| DEC01 | O consumo será registrado no balcão | A tela principal prioriza lançamento rápido |
| DEC02 | O MVP funcionará em um computador | SQLite local é suficiente |
| DEC03 | O MVP apenas calcula e exibe o total | Pagamento e maquininha ficam fora do sistema |
| DEC04 | A cobrança será digitada manualmente | O operador confirma o encerramento após resultado externo |
| DEC05 | O funcionamento será offline | Nenhum fluxo essencial depende de API ou internet |
| DEC06 | Preços históricos serão copiados para os itens | Alteração posterior do catálogo não muda vendas antigas |
| DEC07 | A família do sistema operacional será Windows | Interface e distribuição podem usar tecnologias nativas da plataforma |
| DEC08 | A stack será C# .NET 10 LTS WPF e SQLite | O projeto terá publicação autocontida e persistência local com Entity Framework Core |

## Premissas

- produtos são vendidos por unidades inteiras;
- cada comanda possui um número visível reutilizável;
- apenas uma comanda aberta pode usar determinado número;
- a moeda é o real brasileiro;
- valores são armazenados como inteiros em centavos;
- atendente e caixa podem ser a mesma pessoa;
- não existe autenticação de usuários no MVP.
- o aplicativo terá uma única instância por sessão do Windows;
- comandas abertas serão recuperadas após encerramento inesperado.

## Limitação operacional conhecida

O sistema não recebe o resultado da maquininha. Se o operador encerrar a comanda antes da aprovação ou digitar um valor incorreto, o histórico pode divergir do valor efetivamente recebido. A interface deve destacar o total e exigir confirmação clara.
