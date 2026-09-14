# Referências técnicas

## Formatação documental

- ABNT NBR 14724:2024, documentação acadêmica e apresentação de trabalhos;
- Catálogo ABNT: <https://www.abntcatalogo.com.br/>.

## Integração futura com maquininha

Estas referências demonstram que integrações são específicas por fornecedor e terminal. Elas não fazem parte do MVP.

- Mercado Pago Point: <https://www.mercadopago.com.br/developers/pt/docs/mp-point/overview>;
- processamento Point: <https://www.mercadopago.com.br/developers/pt/docs/mp-point/payment-processing>;
- PagBank PlugPag: <https://developer.pagbank.com.br/docs/estrutura-da-aplicacao>;
- guia PlugPag Android: <https://developer.pagbank.com.br/docs/guide-android>;
- TEF Stone: <https://tefdoc.stone.com.br/>.

Antes de implementar, confirmar documentação atual, modelos compatíveis, credenciais, conectividade, taxas, contratação, suporte e homologação diretamente com a operadora.

## Tecnologias propostas

Referências verificadas em 13 de setembro de 2026. Nessa data, o .NET 10 estava em suporte ativo como versão LTS, com término de suporte previsto para 14 de novembro de 2028. O desenvolvimento deve usar o patch 10.0.x mais recente disponível e manter os pacotes do Entity Framework Core na mesma versão principal do runtime.

- suporte do .NET: <https://dotnet.microsoft.com/platform/support/policy/dotnet-core>;
- WPF: <https://learn.microsoft.com/dotnet/desktop/wpf/overview/>;
- MVVM Toolkit: <https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/>;
- Entity Framework Core: <https://learn.microsoft.com/ef/core/>;
- provedor SQLite do Entity Framework Core: <https://learn.microsoft.com/ef/core/providers/sqlite/>;
- publicação do .NET: <https://learn.microsoft.com/dotnet/core/deploying/>;
- SQLite: <https://www.sqlite.org/docs.html>;
- API de backup de `Microsoft.Data.Sqlite`: <https://learn.microsoft.com/dotnet/api/microsoft.data.sqlite.sqliteconnection.backupdatabase>;
- xUnit: <https://xunit.net/>;
- Serilog: <https://serilog.net/>.

Versões e suporte devem ser novamente verificados quando o desenvolvimento começar.
