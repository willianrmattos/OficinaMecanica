using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OficinaMecanica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaSagaEPrazos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrdemDeServicoSagas",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentState = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OrcamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PagamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PrazoAprovacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PrazoPagamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelamentoPorStaff = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MotivoCancelamento = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CriadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AtualizadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdemDeServicoSagas", x => x.CorrelationId);
                });

            migrationBuilder.CreateTable(
                name: "SagaPrazos",
                columns: table => new
                {
                    PrazoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ExpiraEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Processado = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaPrazos", x => x.PrazoId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrdemDeServicoSagas_CurrentState",
                table: "OrdemDeServicoSagas",
                column: "CurrentState");

            migrationBuilder.CreateIndex(
                name: "IX_SagaPrazos_Processado_ExpiraEm",
                table: "SagaPrazos",
                columns: new[] { "Processado", "ExpiraEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrdemDeServicoSagas");

            migrationBuilder.DropTable(
                name: "SagaPrazos");
        }
    }
}
