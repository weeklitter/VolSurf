using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VolSurf.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStockTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stock_balance_sheet",
                columns: table => new
                {
                    TsCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false),
                    ReportType = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    TotalAssets = table.Column<decimal>(type: "numeric(20,4)", nullable: true),
                    TotalLiab = table.Column<decimal>(type: "numeric(20,4)", nullable: true),
                    TotalEquity = table.Column<decimal>(type: "numeric(20,4)", nullable: true),
                    Goodwill = table.Column<decimal>(type: "numeric(20,4)", nullable: true),
                    AccountRecv = table.Column<decimal>(type: "numeric(20,4)", nullable: true),
                    Inventory = table.Column<decimal>(type: "numeric(20,4)", nullable: true),
                    UpdateDate = table.Column<DateTime>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_balance_sheet", x => new { x.TsCode, x.EndDate, x.ReportType });
                });

            migrationBuilder.CreateTable(
                name: "stock_basic",
                columns: table => new
                {
                    TsCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Area = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Industry = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Market = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ListDate = table.Column<DateTime>(type: "date", nullable: true),
                    Exchange = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_basic", x => x.TsCode);
                });

            migrationBuilder.CreateTable(
                name: "stock_business",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TsCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false),
                    BusinessItem = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MainType = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Revenue = table.Column<decimal>(type: "numeric(20,4)", nullable: true),
                    Cost = table.Column<decimal>(type: "numeric(20,4)", nullable: true),
                    Profit = table.Column<decimal>(type: "numeric(20,4)", nullable: true),
                    Ratio = table.Column<decimal>(type: "numeric(8,4)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_business", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stock_cashflow",
                columns: table => new
                {
                    TsCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false),
                    ReportType = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    OperCashFlow = table.Column<decimal>(type: "numeric(20,4)", nullable: true),
                    InvestCashFlow = table.Column<decimal>(type: "numeric(20,4)", nullable: true),
                    FinCashFlow = table.Column<decimal>(type: "numeric(20,4)", nullable: true),
                    CapEx = table.Column<decimal>(type: "numeric(20,4)", nullable: true),
                    UpdateDate = table.Column<DateTime>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_cashflow", x => new { x.TsCode, x.EndDate, x.ReportType });
                });

            migrationBuilder.CreateTable(
                name: "stock_daily",
                columns: table => new
                {
                    TsCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TradeDate = table.Column<DateTime>(type: "date", nullable: false),
                    Open = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    High = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    Low = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    Close = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    PreClose = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    Change = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    PctChg = table.Column<decimal>(type: "numeric(8,4)", nullable: true),
                    Vol = table.Column<decimal>(type: "numeric(15,2)", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(15,4)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_daily", x => new { x.TsCode, x.TradeDate });
                });

            migrationBuilder.CreateTable(
                name: "stock_daily_basic",
                columns: table => new
                {
                    TsCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TradeDate = table.Column<DateTime>(type: "date", nullable: false),
                    Close = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    Pe = table.Column<decimal>(type: "numeric(12,4)", nullable: true),
                    PeTtm = table.Column<decimal>(type: "numeric(12,4)", nullable: true),
                    Pb = table.Column<decimal>(type: "numeric(12,4)", nullable: true),
                    Ps = table.Column<decimal>(type: "numeric(12,4)", nullable: true),
                    PsTtm = table.Column<decimal>(type: "numeric(12,4)", nullable: true),
                    TotalMv = table.Column<decimal>(type: "numeric(15,4)", nullable: true),
                    CircMv = table.Column<decimal>(type: "numeric(15,4)", nullable: true),
                    TurnoverRate = table.Column<decimal>(type: "numeric(8,4)", nullable: true),
                    DvRatio = table.Column<decimal>(type: "numeric(8,4)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_daily_basic", x => new { x.TsCode, x.TradeDate });
                });

            migrationBuilder.CreateTable(
                name: "stock_income",
                columns: table => new
                {
                    TsCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false),
                    ReportType = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Revenue = table.Column<decimal>(type: "numeric(20,4)", nullable: true),
                    OperCost = table.Column<decimal>(type: "numeric(20,4)", nullable: true),
                    GrossProfit = table.Column<decimal>(type: "numeric(20,4)", nullable: true),
                    NetProfit = table.Column<decimal>(type: "numeric(20,4)", nullable: true),
                    UpdateDate = table.Column<DateTime>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_income", x => new { x.TsCode, x.EndDate, x.ReportType });
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_balance_sheet_EndDate",
                table: "stock_balance_sheet",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_stock_business_TsCode_EndDate",
                table: "stock_business",
                columns: new[] { "TsCode", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_business_TsCode_EndDate_BusinessItem_MainType",
                table: "stock_business",
                columns: new[] { "TsCode", "EndDate", "BusinessItem", "MainType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_cashflow_EndDate",
                table: "stock_cashflow",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_stock_daily_TradeDate",
                table: "stock_daily",
                column: "TradeDate");

            migrationBuilder.CreateIndex(
                name: "IX_stock_daily_basic_TradeDate",
                table: "stock_daily_basic",
                column: "TradeDate");

            migrationBuilder.CreateIndex(
                name: "IX_stock_income_EndDate",
                table: "stock_income",
                column: "EndDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_balance_sheet");

            migrationBuilder.DropTable(
                name: "stock_basic");

            migrationBuilder.DropTable(
                name: "stock_business");

            migrationBuilder.DropTable(
                name: "stock_cashflow");

            migrationBuilder.DropTable(
                name: "stock_daily");

            migrationBuilder.DropTable(
                name: "stock_daily_basic");

            migrationBuilder.DropTable(
                name: "stock_income");
        }
    }
}
