using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketService.Infrastructure.Migrations.ReportingDb
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "event_sales_summaries",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    venue = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    event_date = table.Column<DateOnly>(type: "date", nullable: false),
                    event_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    total_capacity = table.Column<int>(type: "integer", nullable: false),
                    total_tickets_sold = table.Column<int>(type: "integer", nullable: false),
                    available_tickets = table.Column<int>(type: "integer", nullable: false),
                    total_revenue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_sales_summaries", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "tier_sales_summaries",
                columns: table => new
                {
                    pricing_tier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_quantity = table.Column<int>(type: "integer", nullable: false),
                    quantity_sold = table.Column<int>(type: "integer", nullable: false),
                    quantity_available = table.Column<int>(type: "integer", nullable: false),
                    revenue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tier_sales_summaries", x => x.pricing_tier_id);
                    table.ForeignKey(
                        name: "FK_tier_sales_summaries_event_sales_summaries_event_id",
                        column: x => x.event_id,
                        principalTable: "event_sales_summaries",
                        principalColumn: "event_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tier_sales_summaries_event_id",
                table: "tier_sales_summaries",
                column: "event_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tier_sales_summaries");

            migrationBuilder.DropTable(
                name: "event_sales_summaries");
        }
    }
}
