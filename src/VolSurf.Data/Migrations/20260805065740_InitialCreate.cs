using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VolSurf.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "iv_percentile_cache",
                columns: table => new
                {
                    Underlying = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TradeDate = table.Column<DateTime>(type: "date", nullable: false),
                    AtmIv = table.Column<decimal>(type: "numeric(8,4)", nullable: true),
                    IvPercentile = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    IvMean = table.Column<decimal>(type: "numeric(8,4)", nullable: true),
                    IvStd = table.Column<decimal>(type: "numeric(8,4)", nullable: true),
                    SampleDays = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iv_percentile_cache", x => new { x.Underlying, x.TradeDate });
                });

            migrationBuilder.CreateTable(
                name: "options_contracts",
                columns: table => new
                {
                    TsCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Exchange = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Underlying = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CallPut = table.Column<string>(type: "char(1)", nullable: false),
                    ExercisePrice = table.Column<decimal>(type: "numeric(10,4)", nullable: false),
                    ExerciseType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    OptMultiplier = table.Column<decimal>(type: "numeric(10,4)", nullable: false),
                    MaturityDate = table.Column<DateTime>(type: "date", nullable: false),
                    ListDate = table.Column<DateTime>(type: "date", nullable: true),
                    DelistDate = table.Column<DateTime>(type: "date", nullable: true),
                    Adjusted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_options_contracts", x => x.TsCode);
                });

            migrationBuilder.CreateTable(
                name: "options_daily",
                columns: table => new
                {
                    TsCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TradeDate = table.Column<DateTime>(type: "date", nullable: false),
                    Underlying = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Open = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    High = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    Low = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    Close = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    Settle = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    Vol = table.Column<decimal>(type: "numeric(15,2)", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(15,4)", nullable: true),
                    Oi = table.Column<decimal>(type: "numeric(15,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_options_daily", x => new { x.TsCode, x.TradeDate });
                });

            migrationBuilder.CreateTable(
                name: "options_iv_greeks",
                columns: table => new
                {
                    TsCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TradeDate = table.Column<DateTime>(type: "date", nullable: false),
                    Underlying = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Iv = table.Column<decimal>(type: "numeric(8,4)", nullable: true),
                    Delta = table.Column<decimal>(type: "numeric(8,4)", nullable: true),
                    Gamma = table.Column<decimal>(type: "numeric(8,4)", nullable: true),
                    Theta = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    Vega = table.Column<decimal>(type: "numeric(8,4)", nullable: true),
                    Rho = table.Column<decimal>(type: "numeric(8,4)", nullable: true),
                    IvConfidence = table.Column<bool>(type: "boolean", nullable: false),
                    IvAnomaly = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_options_iv_greeks", x => new { x.TsCode, x.TradeDate });
                });

            migrationBuilder.CreateTable(
                name: "underlying_daily",
                columns: table => new
                {
                    TsCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TradeDate = table.Column<DateTime>(type: "date", nullable: false),
                    Close = table.Column<decimal>(type: "numeric(10,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_underlying_daily", x => new { x.TsCode, x.TradeDate });
                });

            migrationBuilder.CreateTable(
                name: "underlyings",
                columns: table => new
                {
                    TsCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Exchange = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AssetClass = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_underlyings", x => x.TsCode);
                });

            migrationBuilder.CreateIndex(
                name: "IX_options_contracts_MaturityDate",
                table: "options_contracts",
                column: "MaturityDate");

            migrationBuilder.CreateIndex(
                name: "IX_options_contracts_Underlying",
                table: "options_contracts",
                column: "Underlying");

            migrationBuilder.CreateIndex(
                name: "IX_options_daily_TradeDate",
                table: "options_daily",
                column: "TradeDate");

            migrationBuilder.CreateIndex(
                name: "IX_options_daily_TsCode_TradeDate",
                table: "options_daily",
                columns: new[] { "TsCode", "TradeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_options_daily_Underlying_TradeDate",
                table: "options_daily",
                columns: new[] { "Underlying", "TradeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_options_iv_greeks_TradeDate_Underlying",
                table: "options_iv_greeks",
                columns: new[] { "TradeDate", "Underlying" });

            migrationBuilder.CreateIndex(
                name: "IX_options_iv_greeks_Underlying_TradeDate",
                table: "options_iv_greeks",
                columns: new[] { "Underlying", "TradeDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "iv_percentile_cache");

            migrationBuilder.DropTable(
                name: "options_contracts");

            migrationBuilder.DropTable(
                name: "options_daily");

            migrationBuilder.DropTable(
                name: "options_iv_greeks");

            migrationBuilder.DropTable(
                name: "underlying_daily");

            migrationBuilder.DropTable(
                name: "underlyings");
        }
    }
}
