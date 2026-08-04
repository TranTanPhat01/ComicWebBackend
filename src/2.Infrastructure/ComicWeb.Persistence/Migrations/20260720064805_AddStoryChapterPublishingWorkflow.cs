using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComicWeb.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoryChapterPublishingWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chapters_Stories_StoryId",
                table: "Chapters");

            migrationBuilder.DropIndex(
                name: "IX_Stories_Title",
                table: "Stories");

            migrationBuilder.DropIndex(
                name: "IX_Chapters_StoryId",
                table: "Chapters");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Stories",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Stories",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AddColumn<string>(
                name: "AuthorName",
                table: "Stories",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Stories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "Stories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledAt",
                table: "Stories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Stories",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Stories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Chapters",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(250)",
                oldMaxLength: 250,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AffiliateLink",
                table: "Chapters",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Chapters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "Chapters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledAt",
                table: "Chapters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Chapters",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Chapters",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Chapters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE \"Stories\" SET \"Slug\" = 'story-' || \"Id\", \"Status\" = CASE \"Status\" WHEN 'Ongoing' THEN 'Draft' WHEN 'Paused' THEN 'Hidden' ELSE \"Status\" END WHERE \"Slug\" = ''");
            migrationBuilder.Sql("UPDATE \"Chapters\" SET \"Slug\" = 'chapter-' || \"Id\", \"Status\" = 'Draft' WHERE \"Slug\" = '' OR \"Status\" = ''");

            migrationBuilder.CreateIndex(
                name: "IX_Stories_ScheduledAt_Status",
                table: "Stories",
                columns: new[] { "ScheduledAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Stories_Slug",
                table: "Stories",
                column: "Slug",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Stories_Status_PublishedAt",
                table: "Stories",
                columns: new[] { "Status", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Chapters_ScheduledAt_Status",
                table: "Chapters",
                columns: new[] { "ScheduledAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Chapters_StoryId_ChapterNumber",
                table: "Chapters",
                columns: new[] { "StoryId", "ChapterNumber" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Chapters_StoryId_Slug",
                table: "Chapters",
                columns: new[] { "StoryId", "Slug" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Chapters_StoryId_Status_ChapterNumber",
                table: "Chapters",
                columns: new[] { "StoryId", "Status", "ChapterNumber" });

            migrationBuilder.AddForeignKey(
                name: "FK_Chapters_Stories_StoryId",
                table: "Chapters",
                column: "StoryId",
                principalTable: "Stories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chapters_Stories_StoryId",
                table: "Chapters");

            migrationBuilder.DropIndex(
                name: "IX_Stories_ScheduledAt_Status",
                table: "Stories");

            migrationBuilder.DropIndex(
                name: "IX_Stories_Slug",
                table: "Stories");

            migrationBuilder.DropIndex(
                name: "IX_Stories_Status_PublishedAt",
                table: "Stories");

            migrationBuilder.DropIndex(
                name: "IX_Chapters_ScheduledAt_Status",
                table: "Chapters");

            migrationBuilder.DropIndex(
                name: "IX_Chapters_StoryId_ChapterNumber",
                table: "Chapters");

            migrationBuilder.DropIndex(
                name: "IX_Chapters_StoryId_Slug",
                table: "Chapters");

            migrationBuilder.DropIndex(
                name: "IX_Chapters_StoryId_Status_ChapterNumber",
                table: "Chapters");

            migrationBuilder.DropColumn(
                name: "AuthorName",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "ScheduledAt",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Chapters");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Chapters");

            migrationBuilder.DropColumn(
                name: "ScheduledAt",
                table: "Chapters");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Chapters");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Chapters");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Chapters");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Stories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Stories",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Chapters",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(250)",
                oldMaxLength: 250);

            migrationBuilder.AlterColumn<string>(
                name: "AffiliateLink",
                table: "Chapters",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stories_Title",
                table: "Stories",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_Chapters_StoryId",
                table: "Chapters",
                column: "StoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Chapters_Stories_StoryId",
                table: "Chapters",
                column: "StoryId",
                principalTable: "Stories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
