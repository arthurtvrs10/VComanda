PRAGMA foreign_keys = ON;

-- Integridade geral do arquivo.
PRAGMA integrity_check;

-- Totais materializados que divergem da soma dos itens.
SELECT
  c.id,
  c.numero,
  c.total_centavos,
  COALESCE(SUM(i.subtotal_centavos), 0) AS soma_itens
FROM comanda c
LEFT JOIN item_comanda i ON i.comanda_id = c.id
GROUP BY c.id
HAVING c.total_centavos <> COALESCE(SUM(i.subtotal_centavos), 0);

-- Vendas cujo total diverge da comanda.
SELECT
  v.id AS venda_id,
  v.total_centavos AS total_venda,
  c.total_centavos AS total_comanda
FROM venda v
JOIN comanda c ON c.id = v.comanda_id
WHERE v.total_centavos <> c.total_centavos;

-- Vendas ligadas a comandas que não estão fechadas.
SELECT v.id, c.id, c.status
FROM venda v
JOIN comanda c ON c.id = v.comanda_id
WHERE c.status <> 'FECHADA';

-- Quantidade de comandas abertas por número. A consulta deve retornar zero linhas.
SELECT numero, COUNT(*) AS quantidade
FROM comanda
WHERE status = 'ABERTA'
GROUP BY numero
HAVING COUNT(*) > 1;

-- Resumo diário.
SELECT
  DATE(finalizada_em) AS data,
  COUNT(*) AS quantidade_vendas,
  SUM(total_centavos) AS total_centavos,
  ROUND(AVG(total_centavos), 0) AS ticket_medio_centavos
FROM venda
WHERE status = 'CONCLUIDA'
GROUP BY DATE(finalizada_em)
ORDER BY data DESC;

