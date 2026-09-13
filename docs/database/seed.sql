PRAGMA foreign_keys = ON;

BEGIN TRANSACTION;

INSERT INTO categoria (id, nome, ativo, criado_em, atualizado_em) VALUES
  (1, 'Lanches', 1, '2026-09-13T10:00:00', '2026-09-13T10:00:00'),
  (2, 'Bebidas', 1, '2026-09-13T10:00:00', '2026-09-13T10:00:00'),
  (3, 'Porções', 1, '2026-09-13T10:00:00', '2026-09-13T10:00:00');

INSERT INTO produto (id, categoria_id, nome, preco_centavos, ativo, criado_em, atualizado_em) VALUES
  (1, 1, 'X Salada', 1800, 1, '2026-09-13T10:05:00', '2026-09-13T10:05:00'),
  (2, 2, 'Refrigerante', 600, 1, '2026-09-13T10:05:00', '2026-09-13T10:05:00'),
  (3, 3, 'Batata', 1200, 1, '2026-09-13T10:05:00', '2026-09-13T10:05:00');

INSERT INTO comanda (id, numero, status, aberta_em, fechada_em, total_centavos, observacao) VALUES
  (1, 27, 'FECHADA', '2026-09-13T12:00:00', '2026-09-13T12:35:00', 5400, NULL),
  (2, 12, 'ABERTA', '2026-09-13T13:00:00', NULL, 1800, NULL);

INSERT INTO item_comanda
  (id, comanda_id, produto_id, nome_produto, preco_unitario_centavos, quantidade, subtotal_centavos, observacao, criado_em, atualizado_em)
VALUES
  (1, 1, 1, 'X Salada', 1800, 2, 3600, NULL, '2026-09-13T12:05:00', '2026-09-13T12:05:00'),
  (2, 1, 2, 'Refrigerante', 600, 1, 600, NULL, '2026-09-13T12:10:00', '2026-09-13T12:10:00'),
  (3, 1, 3, 'Batata', 1200, 1, 1200, NULL, '2026-09-13T12:15:00', '2026-09-13T12:15:00'),
  (4, 2, 1, 'X Salada', 1800, 1, 1800, NULL, '2026-09-13T13:05:00', '2026-09-13T13:05:00');

INSERT INTO venda (id, comanda_id, numero, total_centavos, finalizada_em, status) VALUES
  (1, 1, 1, 5400, '2026-09-13T12:35:00', 'CONCLUIDA');

INSERT INTO configuracao (chave, valor, atualizado_em) VALUES
  ('estabelecimento.nome', 'Lanchonete Exemplo', '2026-09-13T10:00:00'),
  ('comandas.quantidade', '30', '2026-09-13T10:00:00'),
  ('backup.retencao_dias', '30', '2026-09-13T10:00:00');

COMMIT;

