# Varthex Comanda

Repositório documental do **Varthex Comanda — Sistema Local de Controle de Consumo**, um aplicativo desktop para Windows que registra produtos consumidos em comandas numeradas, calcula o total e apresenta o valor que o operador deverá digitar manualmente na maquininha.

O MVP não processa pagamentos, não se comunica com a maquininha e não registra forma de pagamento, valor recebido, troco, cartão ou autorização da adquirente.

## Comece aqui

1. Leia [Visão e decisões](docs/00-visao-e-decisoes.md).
2. Confirme o [escopo do MVP](docs/01-escopo-mvp.md).
3. Implemente na ordem definida no [guia de implementação](docs/14-guia-implementacao.md).
4. Consulte os [requisitos](docs/02-requisitos.md), as [regras de negócio](docs/03-regras-negocio.md) e os [casos de uso](docs/04-casos-de-uso.md) durante o desenvolvimento.
5. Crie o banco com [database/schema.sql](database/schema.sql) e valide com [database/seed.sql](database/seed.sql).
6. Execute os cenários de [testes e aceitação](docs/09-testes-aceitacao.md) antes de considerar uma entrega concluída.

Para desenvolvimento assistido por IA, o arquivo [AGENTS.md](AGENTS.md) define a fonte de verdade e os limites que não podem ser ultrapassados.

## Mapa da documentação

| Documento | Finalidade |
| --- | --- |
| [00 Visão e decisões](docs/00-visao-e-decisoes.md) | Problema, objetivos e decisões já tomadas |
| [01 Escopo do MVP](docs/01-escopo-mvp.md) | Incluído, excluído e critério de conclusão |
| [02 Requisitos](docs/02-requisitos.md) | Requisitos funcionais e não funcionais |
| [03 Regras de negócio](docs/03-regras-negocio.md) | Invariantes que o código deve respeitar |
| [04 Casos de uso](docs/04-casos-de-uso.md) | Fluxos principais, alternativos e pós-condições |
| [05 Modelagem de dados](docs/05-modelagem-dados.md) | Entidades, campos, relacionamentos e cálculos |
| [06 Arquitetura](docs/06-arquitetura.md) | Componentes, responsabilidades e contratos |
| [07 Experiência do usuário](docs/07-experiencia-usuario.md) | Telas, estados, mensagens e atalhos |
| [08 Segurança e backup](docs/08-seguranca-backup.md) | Privacidade, integridade, backup e restauração |
| [09 Testes e aceitação](docs/09-testes-aceitacao.md) | Cenários executáveis e definição de pronto |
| [10 Rastreabilidade](docs/10-rastreabilidade.md) | Objetivos ligados a requisitos, regras e testes |
| [11 Roadmap e riscos](docs/11-roadmap-riscos.md) | Entregas, evoluções e tratamento de riscos |
| [12 Operação e implantação](docs/12-operacao-implantacao.md) | Instalação, atualização e rotina diária |
| [13 Pendências](docs/13-pendencias-validacao.md) | Decisões que precisam de validação humana |
| [14 Guia de implementação](docs/14-guia-implementacao.md) | Sequência de construção e critérios por etapa |
| [15 Contratos da aplicação](docs/15-contratos-aplicacao.md) | Serviços, repositórios e transações esperadas |
| [16 Referências técnicas](docs/16-referencias-tecnicas.md) | Fontes sobre integrações futuras |
| [17 Glossário](docs/17-glossario.md) | Termos funcionais e técnicos |
| [18 Histórias de usuário](docs/18-historias-usuario.md) | Backlog funcional derivado dos requisitos |
| [19 Plataforma Windows e .NET](docs/19-plataforma-windows-dotnet.md) | Estrutura da solução, comandos, persistência e publicação |

## Artefatos adicionais

- `database/`: esquema SQLite, dados de demonstração e consultas de verificação.
- `docs/diagramas/`: fontes Mermaid editáveis para GitHub.
- `docs/adr/`: decisões arquiteturais registradas.
- `documentos/`: documento consolidado em Word no padrão ABNT.
- `templates/`: modelos para caso de teste e solicitação de mudança.

## Regra central do MVP

```text
itens registrados -> subtotais -> total a pagar
                                  |
                                  v
                      digitação manual na maquininha
                                  |
                                  v
                  confirmação externa pelo operador
                                  |
                                  v
                        encerramento da comanda
```

O sistema registra a venda pelo total da comanda somente após confirmação explícita do operador. Ele não possui evidência automática de que o pagamento foi aprovado.

## Stack definida

- C# e .NET 10 LTS;
- WPF com XAML e MVVM;
- SQLite;
- Entity Framework Core com repositórios;
- migrações do Entity Framework Core;
- xUnit;
- publicação autocontida para Windows `win-x64` como padrão inicial.

A família do sistema operacional foi confirmada como Windows. A edição, versão, arquitetura e configuração do computador ainda devem ser registradas antes da homologação. Os requisitos de negócio permanecem independentes da tecnologia.

## Licença

Nenhuma licença de código foi escolhida. Antes de publicar o futuro código-fonte, o proprietário do projeto deve decidir se o repositório será privado ou qual licença será adotada. Não presuma autorização para reutilização pública apenas porque a documentação está no GitHub.

## Status

Versão documental 1.3, de 13 de setembro de 2026. Escopo preparado para implementação do MVP em um único computador Windows.
