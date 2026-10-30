using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobHunter.Api.Common.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobCaptureSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_companies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "search_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    required_keywords = table.Column<List<string>>(type: "text[]", nullable: false),
                    preferred_keywords = table.Column<List<string>>(type: "text[]", nullable: false),
                    excluded_keywords = table.Column<List<string>>(type: "text[]", nullable: false),
                    excluded_description_keywords = table.Column<List<string>>(type: "text[]", nullable: false),
                    locations = table.Column<List<string>>(type: "text[]", nullable: false),
                    work_models = table.Column<string[]>(type: "text[]", nullable: false),
                    seniorities = table.Column<List<string>>(type: "text[]", nullable: false),
                    min_score = table.Column<int>(type: "integer", nullable: false),
                    weights = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_search_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "job_postings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    normalized_title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    normalized_city = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    work_model = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    seniority = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    employment_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description_text = table.Column<string>(type: "text", nullable: true),
                    description_html = table.Column<string>(type: "text", nullable: true),
                    requirements = table.Column<List<string>>(type: "text[]", nullable: false),
                    apply_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    normalized_apply_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    apply_channel_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    dedup_confidence = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status_before_close = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    match_score = table.Column<int>(type: "integer", nullable: true),
                    match_reasons = table.Column<string>(type: "jsonb", nullable: false),
                    scored_with_profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    discard_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_manually_discarded = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_postings", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_postings_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "source_targets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    connector_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tenant_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    search_criteria = table.Column<string>(type: "jsonb", nullable: true),
                    checkpoint = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    scan_interval = table.Column<TimeSpan>(type: "interval", nullable: true),
                    last_scan_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_scan_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consecutive_failures = table.Column<int>(type: "integer", nullable: false),
                    last_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_source_targets", x => x.id);
                    table.ForeignKey(
                        name: "fk_source_targets_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "posting_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    connector_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    external_id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    source_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    extra = table.Column<string>(type: "jsonb", nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    missed_scans = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_posting_sources", x => x.id);
                    table.ForeignKey(
                        name: "fk_posting_sources_job_postings_job_posting_id",
                        column: x => x.job_posting_id,
                        principalTable: "job_postings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_posting_sources_source_targets_source_target_id",
                        column: x => x.source_target_id,
                        principalTable: "source_targets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "scan_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connector_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    found = table.Column<int>(type: "integer", nullable: false),
                    created = table.Column<int>(type: "integer", nullable: false),
                    updated = table.Column<int>(type: "integer", nullable: false),
                    deduplicated = table.Column<int>(type: "integer", nullable: false),
                    closed = table.Column<int>(type: "integer", nullable: false),
                    error_kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    evidence_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scan_runs", x => x.id);
                    table.ForeignKey(
                        name: "fk_scan_runs_source_targets_source_target_id",
                        column: x => x.source_target_id,
                        principalTable: "source_targets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_companies_normalized_name",
                table: "companies",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_company_id",
                table: "job_postings",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_fingerprint_first_seen_at",
                table: "job_postings",
                columns: new[] { "fingerprint", "first_seen_at" });

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_match_score",
                table: "job_postings",
                column: "match_score",
                descending: new bool[0],
                filter: "status = 'Matched'");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_normalized_apply_url",
                table: "job_postings",
                column: "normalized_apply_url",
                unique: true,
                filter: "normalized_apply_url IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_status",
                table: "job_postings",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_posting_sources_connector_key_external_id",
                table: "posting_sources",
                columns: new[] { "connector_key", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_posting_sources_job_posting_id",
                table: "posting_sources",
                column: "job_posting_id");

            migrationBuilder.CreateIndex(
                name: "ix_posting_sources_source_target_id",
                table: "posting_sources",
                column: "source_target_id");

            migrationBuilder.CreateIndex(
                name: "ix_scan_runs_source_target_id_started_at",
                table: "scan_runs",
                columns: new[] { "source_target_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_scan_runs_started_at",
                table: "scan_runs",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ix_source_targets_company_id",
                table: "source_targets",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_source_targets_connector_key_tenant_ref",
                table: "source_targets",
                columns: new[] { "connector_key", "tenant_ref" },
                unique: true,
                filter: "kind = 'Tenant'");

            migrationBuilder.CreateIndex(
                name: "ix_source_targets_is_active_next_scan_at",
                table: "source_targets",
                columns: new[] { "is_active", "next_scan_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "posting_sources");

            migrationBuilder.DropTable(
                name: "scan_runs");

            migrationBuilder.DropTable(
                name: "search_profiles");

            migrationBuilder.DropTable(
                name: "job_postings");

            migrationBuilder.DropTable(
                name: "source_targets");

            migrationBuilder.DropTable(
                name: "companies");
        }
    }
}
