PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS categoria (
  id INTEGER PRIMARY KEY,
  nome TEXT NOT NULL COLLATE NOCASE UNIQUE,
  ativo INTEGER NOT NULL DEFAULT 1 CHECK (ativo IN (0, 1)),
  criado_em TEXT NOT NULL,
  atualizado_em TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS produto (
  id INTEGER PRIMARY KEY,
  categoria_id INTEGER NOT NULL REFERENCES categoria(id) ON DELETE RESTRICT,
  nome TEXT NOT NULL,
  preco_centavos INTEGER NOT NULL CHECK (preco_centavos > 0),
  ativo INTEGER NOT NULL DEFAULT 1 CHECK (ativo IN (0, 1)),
  criado_em TEXT NOT NULL,
  atualizado_em TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_produto_categoria_ativo
  ON produto(categoria_id, ativo);

CREATE TABLE IF NOT EXISTS comanda (
  id INTEGER PRIMARY KEY,
  numero INTEGER NOT NULL CHECK (numero > 0),
  status TEXT NOT NULL CHECK (status IN ('ABERTA', 'FECHADA', 'CANCELADA')),
  aberta_em TEXT NOT NULL,
  fechada_em TEXT,
  total_centavos INTEGER NOT NULL DEFAULT 0 CHECK (total_centavos >= 0),
  observacao TEXT,
  CHECK (
    (status = 'ABERTA' AND fechada_em IS NULL) OR
    (status IN ('FECHADA', 'CANCELADA') AND fechada_em IS NOT NULL)
  )
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_comanda_numero_aberta
  ON comanda(numero)
  WHERE status = 'ABERTA';

CREATE TABLE IF NOT EXISTS item_comanda (
  id INTEGER PRIMARY KEY,
  comanda_id INTEGER NOT NULL REFERENCES comanda(id) ON DELETE RESTRICT,
  produto_id INTEGER NOT NULL REFERENCES produto(id) ON DELETE RESTRICT,
  nome_produto TEXT NOT NULL,
  preco_unitario_centavos INTEGER NOT NULL CHECK (preco_unitario_centavos > 0),
  quantidade INTEGER NOT NULL CHECK (quantidade > 0),
  subtotal_centavos INTEGER NOT NULL CHECK (subtotal_centavos > 0),
  observacao TEXT,
  criado_em TEXT NOT NULL,
  atualizado_em TEXT NOT NULL,
  CHECK (subtotal_centavos = preco_unitario_centavos * quantidade)
);

CREATE INDEX IF NOT EXISTS idx_item_comanda_comanda
  ON item_comanda(comanda_id);

CREATE TABLE IF NOT EXISTS venda (
  id INTEGER PRIMARY KEY,
  comanda_id INTEGER NOT NULL UNIQUE REFERENCES comanda(id) ON DELETE RESTRICT,
  numero INTEGER NOT NULL UNIQUE,
  total_centavos INTEGER NOT NULL CHECK (total_centavos > 0),
  finalizada_em TEXT NOT NULL,
  status TEXT NOT NULL DEFAULT 'CONCLUIDA' CHECK (status = 'CONCLUIDA')
);

CREATE INDEX IF NOT EXISTS idx_venda_finalizada_em
  ON venda(finalizada_em);

CREATE TABLE IF NOT EXISTS configuracao (
  chave TEXT PRIMARY KEY,
  valor TEXT NOT NULL,
  atualizado_em TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS backup_registro (
  id INTEGER PRIMARY KEY,
  arquivo TEXT NOT NULL,
  destino TEXT NOT NULL,
  criado_em TEXT NOT NULL,
  status TEXT NOT NULL CHECK (status IN ('SUCESSO', 'FALHA')),
  checksum TEXT,
  mensagem TEXT
);

