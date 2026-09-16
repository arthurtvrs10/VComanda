using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VarthexComanda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "backup_registro",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    arquivo = table.Column<string>(type: "TEXT", nullable: false),
                    destino = table.Column<string>(type: "TEXT", nullable: false),
                    criado_em = table.Column<DateTime>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    checksum = table.Column<string>(type: "TEXT", nullable: true),
                    mensagem = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_registro", x => x.id);
                    table.CheckConstraint("CK_backup_registro_status", "status IN ('SUCESSO', 'FALHA')");
                });

            migrationBuilder.CreateTable(
                name: "categoria",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    nome = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    ativo = table.Column<int>(type: "INTEGER", nullable: false),
                    criado_em = table.Column<DateTime>(type: "TEXT", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categoria", x => x.id);
                    table.CheckConstraint("CK_categoria_ativo", "ativo IN (0, 1)");
                });

            migrationBuilder.CreateTable(
                name: "comanda",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    numero = table.Column<int>(type: "INTEGER", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    aberta_em = table.Column<DateTime>(type: "TEXT", nullable: false),
                    fechada_em = table.Column<DateTime>(type: "TEXT", nullable: true),
                    total_centavos = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    observacao = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comanda", x => x.id);
                    table.CheckConstraint("CK_comanda_numero_positivo", "numero > 0");
                    table.CheckConstraint("CK_comanda_status_fechada_em", "(status = 'ABERTA' AND fechada_em IS NULL) OR (status IN ('FECHADA', 'CANCELADA') AND fechada_em IS NOT NULL)");
                    table.CheckConstraint("CK_comanda_total_centavos_nao_negativo", "total_centavos >= 0");
                });

            migrationBuilder.CreateTable(
                name: "configuracao",
                columns: table => new
                {
                    chave = table.Column<string>(type: "TEXT", nullable: false),
                    valor = table.Column<string>(type: "TEXT", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracao", x => x.chave);
                });

            migrationBuilder.CreateTable(
                name: "produto",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    categoria_id = table.Column<int>(type: "INTEGER", nullable: false),
                    nome = table.Column<string>(type: "TEXT", nullable: false),
                    preco_centavos = table.Column<long>(type: "INTEGER", nullable: false),
                    ativo = table.Column<int>(type: "INTEGER", nullable: false),
                    criado_em = table.Column<DateTime>(type: "TEXT", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_produto", x => x.id);
                    table.CheckConstraint("CK_produto_ativo", "ativo IN (0, 1)");
                    table.CheckConstraint("CK_produto_preco_centavos_positivo", "preco_centavos > 0");
                    table.ForeignKey(
                        name: "FK_produto_categoria_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "categoria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "venda",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    comanda_id = table.Column<int>(type: "INTEGER", nullable: false),
                    numero = table.Column<int>(type: "INTEGER", nullable: false),
                    total_centavos = table.Column<long>(type: "INTEGER", nullable: false),
                    finalizada_em = table.Column<DateTime>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "CONCLUIDA")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venda", x => x.id);
                    table.CheckConstraint("CK_venda_status_concluida", "status = 'CONCLUIDA'");
                    table.CheckConstraint("CK_venda_total_centavos_positivo", "total_centavos > 0");
                    table.ForeignKey(
                        name: "FK_venda_comanda_comanda_id",
                        column: x => x.comanda_id,
                        principalTable: "comanda",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_comanda",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    comanda_id = table.Column<int>(type: "INTEGER", nullable: false),
                    produto_id = table.Column<int>(type: "INTEGER", nullable: false),
                    nome_produto = table.Column<string>(type: "TEXT", nullable: false),
                    preco_unitario_centavos = table.Column<long>(type: "INTEGER", nullable: false),
                    quantidade = table.Column<int>(type: "INTEGER", nullable: false),
                    subtotal_centavos = table.Column<long>(type: "INTEGER", nullable: false),
                    observacao = table.Column<string>(type: "TEXT", nullable: true),
                    criado_em = table.Column<DateTime>(type: "TEXT", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_comanda", x => x.id);
                    table.CheckConstraint("CK_item_comanda_preco_unitario_positivo", "preco_unitario_centavos > 0");
                    table.CheckConstraint("CK_item_comanda_quantidade_positiva", "quantidade > 0");
                    table.CheckConstraint("CK_item_comanda_subtotal_igual_preco_vezes_quantidade", "subtotal_centavos = preco_unitario_centavos * quantidade");
                    table.CheckConstraint("CK_item_comanda_subtotal_positivo", "subtotal_centavos > 0");
                    table.ForeignKey(
                        name: "FK_item_comanda_comanda_comanda_id",
                        column: x => x.comanda_id,
                        principalTable: "comanda",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_item_comanda_produto_produto_id",
                        column: x => x.produto_id,
                        principalTable: "produto",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_categoria_nome",
                table: "categoria",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_comanda_numero_aberta",
                table: "comanda",
                column: "numero",
                unique: true,
                filter: "status = 'ABERTA'");

            migrationBuilder.CreateIndex(
                name: "idx_item_comanda_comanda",
                table: "item_comanda",
                column: "comanda_id");

            migrationBuilder.CreateIndex(
                name: "IX_item_comanda_produto_id",
                table: "item_comanda",
                column: "produto_id");

            migrationBuilder.CreateIndex(
                name: "idx_produto_categoria_ativo",
                table: "produto",
                columns: new[] { "categoria_id", "ativo" });

            migrationBuilder.CreateIndex(
                name: "idx_venda_finalizada_em",
                table: "venda",
                column: "finalizada_em");

            migrationBuilder.CreateIndex(
                name: "IX_venda_comanda_id",
                table: "venda",
                column: "comanda_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_venda_numero",
                table: "venda",
                column: "numero",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "backup_registro");

            migrationBuilder.DropTable(
                name: "configuracao");

            migrationBuilder.DropTable(
                name: "item_comanda");

            migrationBuilder.DropTable(
                name: "venda");

            migrationBuilder.DropTable(
                name: "produto");

            migrationBuilder.DropTable(
                name: "comanda");

            migrationBuilder.DropTable(
                name: "categoria");
        }
    }
}
