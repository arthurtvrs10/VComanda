# ADR 0001 Aplicação desktop local

## Status

Aceita.

## Contexto

O MVP funcionará em um único computador, deve operar sem internet e precisa minimizar custo de infraestrutura.

## Decisão

Construir aplicação desktop que contenha interface, casos de uso, domínio e persistência no mesmo pacote instalável.

## Consequências

- não existe servidor mensal;
- instalação e backup são locais;
- falha do computador afeta toda a operação;
- acesso por vários terminais exige arquitetura futura;
- regras devem permanecer desacopladas da interface para permitir evolução.

