# Como contribuir

## Fluxo recomendado

1. Abra uma issue usando o modelo adequado.
2. Relacione a alteração a um requisito, regra ou caso de uso existente.
3. Se a alteração modificar comportamento, atualize documentação e testes na mesma entrega.
4. Crie uma branch curta, por exemplo `feature/rf09-adicionar-item`.
5. Faça commits pequenos e objetivos.
6. Abra o pull request e preencha toda a lista de verificação.

## Convenções

- requisitos funcionais usam `RFNN`;
- requisitos não funcionais usam `RNFNN`;
- regras de negócio usam `RNNN`;
- casos de uso usam `UCNN`;
- testes de aceitação usam `CTNN`;
- decisões arquiteturais usam `ADR-NNNN`.

## Alteração de escopo

Não inclua estoque, integração de pagamento, cozinha, nuvem, emissão fiscal ou autenticação no MVP sem uma solicitação de mudança aprovada. Use [templates/solicitacao-mudanca.md](templates/solicitacao-mudanca.md).

## Qualidade mínima

- testes automatizados das regras alteradas;
- migração do banco quando houver mudança estrutural;
- operação sem internet preservada;
- nenhuma gravação de dados de cartão ou credenciais de adquirente;
- documentação e rastreabilidade atualizadas.

