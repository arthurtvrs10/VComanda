# Roadmap e riscos

## Ordem de entregas do MVP

| Entrega | Conteúdo | Saída verificável |
| --- | --- | --- |
| E1 Base técnica | Projeto, banco, migrações, configuração e logs | Aplicativo instala e inicia |
| E2 Catálogo | Categorias, produtos, preços, filtros e desativação | Catálogo pronto |
| E3 Comandas | Grade, abertura, itens, quantidades, remoção e total | Consumo registrado |
| E4 Encerramento | Total itemizado, confirmação, transação e histórico | Venda sem pagamento e número liberado |
| E5 Gestão mínima | Histórico, resumo e detalhes | Movimento diário consultável |
| E6 Continuidade | Backup, restauração, atualização e testes | Operação recuperável |

## Evoluções posteriores

- MVP 2: descontos, cancelamento de venda, usuários, permissões, auditoria, impressão e relatórios;
- MVP 3: estoque, ficha técnica, custos, margem, cozinha e múltiplos terminais;
- MVP 4: integração opcional com maquininha, nuvem, delivery, fidelidade e fiscal.

Integração com maquininha só deve avançar após identificar adquirente, modelo, sistema operacional, custo, taxas, documentação e processo de homologação.

## Riscos

| Código | Risco | Probabilidade | Impacto | Tratamento |
| --- | --- | --- | --- | --- |
| R01 | Funcionário não registra itens | Alta | Alto | Botões rápidos e teste no balcão |
| R02 | Computador ou disco falha | Média | Crítico | Backup externo e restauração testada |
| R03 | Escopo cresce antes da validação | Alta | Alto | Congelar MVP e usar solicitação de mudança |
| R04 | Preço atual altera histórico | Média | Alto | Snapshot no item e regressão |
| R05 | Duas comandas usam o mesmo número | Baixa | Alto | Índice único parcial e transação |
| R06 | Encerramento fica parcial | Baixa | Crítico | Transação única e rollback |
| R07 | Backup existe mas não restaura | Média | Crítico | Validação e ensaio periódico |
| R08 | Atualização quebra banco antigo | Média | Alto | Migração versionada e cópia prévia |
| R09 | Falta de energia | Média | Alto | Diário do SQLite e nobreak opcional |
| R10 | Número físico não identifica cliente | Média | Médio | Ficha, mesa, pulseira ou outro mecanismo visível |
| R11 | Sistema operacional indefinido | Média | Médio | Confirmar antes do instalador final |
| R12 | Obrigação fiscal não prevista | Baixa | Alto | Consultar contador e separar do registro interno |
| R13 | Erro manual na maquininha ou encerramento precoce | Média | Alto | Total destacado, confirmação e treinamento |

