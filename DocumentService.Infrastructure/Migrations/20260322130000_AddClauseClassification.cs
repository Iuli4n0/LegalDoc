using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClauseClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AbusiveProbability",
                table: "Clauses",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClassifiedAt",
                table: "Clauses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAbusive",
                table: "Clauses",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbusiveProbability",
                table: "Clauses");

            migrationBuilder.DropColumn(
                name: "ClassifiedAt",
                table: "Clauses");

            migrationBuilder.DropColumn(
                name: "IsAbusive",
                table: "Clauses");
        }
    }
}

