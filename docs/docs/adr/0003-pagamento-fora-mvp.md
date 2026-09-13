# ADR 0003 Pagamento fora do MVP

## Status

Aceita.

## Contexto

A lanchonete já possui maquininha. Integrar depende de adquirente, modelo, credenciais, internet, homologação e possíveis custos. O problema prioritário é registrar o consumo e apresentar o total correto.

## Decisão

O MVP apenas calcula e exibe o total. O operador digita o valor manualmente na maquininha e confirma o encerramento após o resultado externo.

Não haverá:

- entidade pagamento;
- forma de pagamento;
- troco;
- valor recebido;
- API de adquirente;
- dados de cartão;
- conciliação automática.

## Consequências

- compatibilidade com qualquer maquininha usada manualmente;
- operação principal permanece offline;
- não há custo adicional de integração;
- existe risco de digitação incorreta;
- o sistema não comprova aprovação;
- integração pode ser criada depois como adaptador opcional.

