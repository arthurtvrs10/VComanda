# Segurança, privacidade e backup

## Privacidade

O MVP não cadastra cliente. Não solicitar nome, CPF, telefone, endereço ou dados do cartão. Observações aceitam somente informação operacional.

## Segurança local

- usar conta do sistema operacional protegida por senha;
- armazenar dados fora da pasta de executáveis;
- restringir a pasta ao usuário autorizado;
- ativar chaves estrangeiras do SQLite;
- usar consultas parametrizadas;
- habilitar modo de diário adequado;
- não registrar senhas, tokens, cartão ou imagens de documentos;
- aplicar assinatura do instalador quando houver distribuição pública.

## Local dos arquivos

```text
dados/
  lanchonete.db
  logs/
  backups/
```

Em produção, use a pasta de dados do usuário do sistema operacional. Não grave o banco ao lado do executável.

## Política de backup

| Elemento | Padrão inicial |
| --- | --- |
| Frequência | Primeira abertura do dia e encerramento do aplicativo quando houve mudanças |
| Retenção local | 30 cópias diárias |
| Nome | `lanchonete-AAAA-MM-DD-HHMMSS.db` |
| Integridade | `PRAGMA integrity_check` e checksum |
| Cópia externa | Pendrive ou pasta sincronizada ao fim do dia |
| Teste de restauração | Trimestral em pasta separada |

## Processo de backup

1. concluir a transação em andamento;
2. criar snapshot consistente pela API de backup do SQLite;
3. salvar em arquivo temporário no destino;
4. executar verificação de integridade;
5. calcular checksum;
6. renomear para o nome definitivo;
7. registrar sucesso;
8. aplicar retenção somente após nova cópia válida.

## Processo de restauração

1. impedir novos lançamentos;
2. validar extensão, versão e integridade da cópia;
3. mostrar data e consequência;
4. obter confirmação;
5. copiar a base ativa para recuperação preventiva;
6. restaurar primeiro em arquivo temporário;
7. substituir a base apenas após validação;
8. reiniciar e verificar consultas essenciais.

Falha em qualquer etapa preserva a base ativa.

