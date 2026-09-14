# Operação e implantação

## Instalação

1. registrar versão, arquitetura, memória, resolução e escala do Windows alvo;
2. publicar o aplicativo .NET de forma autocontida para a arquitetura confirmada;
3. criar `%LOCALAPPDATA%\VarthexComanda` com dados separados dos binários;
4. aplicar migrações;
5. criar configuração inicial;
6. testar leitura e escrita;
7. configurar pasta de backup;
8. cadastrar catálogo;
9. executar teste completo offline;
10. registrar versão instalada.

O usuário não precisa instalar o SDK do .NET. A primeira implantação pode usar a pasta autocontida publicada; um instalador deve ser adotado quando atualização e distribuição estiverem estabilizadas.

## Rotina diária

1. abrir o aplicativo e verificar aviso de integridade;
2. confirmar produtos e preços;
3. registrar cada item na entrega;
4. conferir itens e total no caixa;
5. digitar o total na maquininha;
6. encerrar somente após aprovação externa;
7. consultar resumo no fim do dia;
8. confirmar backup e cópia externa.

## Atualização

1. encerrar comandas abertas ou adiar;
2. criar e validar backup;
3. registrar versão atual;
4. executar instalador e migrações;
5. testar catálogo, última venda, nova comanda e backup;
6. manter procedimento de retorno com a versão e a cópia anteriores.

## Diagnóstico

Coletar:

- versão do aplicativo;
- sistema operacional;
- horário do erro;
- operação executada;
- mensagem exibida;
- trecho do log sem dado sensível;
- resultado do `PRAGMA integrity_check`;
- espaço disponível em disco.

Também verificar se já existe outra instância do `VarthexComanda.exe`, se a pasta em `%LOCALAPPDATA%` está acessível e se o antivírus bloqueou o executável ou algum arquivo nativo do SQLite.

Nunca solicitar foto de cartão, senha, token ou credencial da conta da maquininha.
