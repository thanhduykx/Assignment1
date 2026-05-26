using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class InitialEduVietRagSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chunking_strategies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    chunk_size = table.Column<int>(type: "int", nullable: false),
                    overlap = table.Column<int>(type: "int", nullable: false),
                    method = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chunking_strategies", x => x.id);
                    table.UniqueConstraint("AK_chunking_strategies_name", x => x.name);
                });

            migrationBuilder.CreateTable(
                name: "embedding_models",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    provider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    model_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    dimensions = table.Column<int>(type: "int", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    config = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_embedding_models", x => x.id);
                    table.UniqueConstraint("AK_embedding_models_name", x => x.name);
                    table.CheckConstraint("CK_embedding_models_config_json", "[config] IS NULL OR ISJSON([config]) = 1");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    full_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    password_hash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    last_login = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subjects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    semester = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subjects", x => x.id);
                    table.ForeignKey(
                        name: "FK_subjects_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "chat_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    subject_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    session_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_chat_sessions_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_chat_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    subject_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    file_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    file_path = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    file_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    chapter = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    indexed_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_documents_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "experiments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    subject_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    experiment_type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    config = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    started_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ended_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experiments", x => x.id);
                    table.CheckConstraint("CK_experiments_config_json", "[config] IS NULL OR ISJSON([config]) = 1");
                    table.ForeignKey(
                        name: "FK_experiments_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_experiments_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "test_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    subject_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    question = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ground_truth = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    difficulty = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_test_questions", x => x.id);
                    table.ForeignKey(
                        name: "FK_test_questions_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_test_questions_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "chat_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    session_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    retrieved_chunks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    model_used = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    response_time = table.Column<double>(type: "float", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_messages", x => x.id);
                    table.CheckConstraint("CK_chat_messages_retrieved_chunks_json", "[retrieved_chunks] IS NULL OR ISJSON([retrieved_chunks]) = 1");
                    table.ForeignKey(
                        name: "FK_chat_messages_chat_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "chat_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    document_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    chunk_index = table.Column<int>(type: "int", nullable: false),
                    content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    token_count = table.Column<int>(type: "int", nullable: true),
                    chunk_strategy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    chunk_size = table.Column<int>(type: "int", nullable: true),
                    chunk_overlap = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chunks", x => x.id);
                    table.ForeignKey(
                        name: "FK_chunks_chunking_strategies_chunk_strategy",
                        column: x => x.chunk_strategy,
                        principalTable: "chunking_strategies",
                        principalColumn: "name",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_chunks_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "experiment_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    experiment_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    embedding_model_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    chunking_strategy_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    run_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    parameters = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    completed_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experiment_runs", x => x.id);
                    table.CheckConstraint("CK_experiment_runs_parameters_json", "[parameters] IS NULL OR ISJSON([parameters]) = 1");
                    table.ForeignKey(
                        name: "FK_experiment_runs_chunking_strategies_chunking_strategy_id",
                        column: x => x.chunking_strategy_id,
                        principalTable: "chunking_strategies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_experiment_runs_embedding_models_embedding_model_id",
                        column: x => x.embedding_model_id,
                        principalTable: "embedding_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_experiment_runs_experiments_experiment_id",
                        column: x => x.experiment_id,
                        principalTable: "experiments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fine_tune_models",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    experiment_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    base_model = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    fine_tuned_model_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    training_config = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    training_metrics = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    trained_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fine_tune_models", x => x.id);
                    table.CheckConstraint("CK_fine_tune_models_training_config_json", "[training_config] IS NULL OR ISJSON([training_config]) = 1");
                    table.CheckConstraint("CK_fine_tune_models_training_metrics_json", "[training_metrics] IS NULL OR ISJSON([training_metrics]) = 1");
                    table.ForeignKey(
                        name: "FK_fine_tune_models_experiments_experiment_id",
                        column: x => x.experiment_id,
                        principalTable: "experiments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "citations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    message_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    chunk_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    similarity_score = table.Column<double>(type: "float", nullable: false),
                    rank = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_citations", x => x.id);
                    table.ForeignKey(
                        name: "FK_citations_chat_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "chat_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_citations_chunks_chunk_id",
                        column: x => x.chunk_id,
                        principalTable: "chunks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "embeddings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    chunk_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    model_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    vector_data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    dimensions = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_embeddings", x => x.id);
                    table.ForeignKey(
                        name: "FK_embeddings_chunks_chunk_id",
                        column: x => x.chunk_id,
                        principalTable: "chunks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_embeddings_embedding_models_model_name",
                        column: x => x.model_name,
                        principalTable: "embedding_models",
                        principalColumn: "name",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "benchmark_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    run_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    question_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    generated_answer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    faithfulness = table.Column<double>(type: "float", nullable: true),
                    answer_relevancy = table.Column<double>(type: "float", nullable: true),
                    context_precision = table.Column<double>(type: "float", nullable: true),
                    context_recall = table.Column<double>(type: "float", nullable: true),
                    ragas_score = table.Column<double>(type: "float", nullable: true),
                    latency_ms = table.Column<double>(type: "float", nullable: true),
                    evaluated_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_benchmark_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_benchmark_results_experiment_runs_run_id",
                        column: x => x.run_id,
                        principalTable: "experiment_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_benchmark_results_test_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "test_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_benchmark_results_question_id",
                table: "benchmark_results",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "IX_benchmark_results_run_id_question_id",
                table: "benchmark_results",
                columns: new[] { "run_id", "question_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_session_id_created_at",
                table: "chat_messages",
                columns: new[] { "session_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_sessions_subject_id",
                table: "chat_sessions",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_sessions_user_id_subject_id",
                table: "chat_sessions",
                columns: new[] { "user_id", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "IX_chunks_chunk_strategy",
                table: "chunks",
                column: "chunk_strategy");

            migrationBuilder.CreateIndex(
                name: "IX_chunks_document_id_chunk_index",
                table: "chunks",
                columns: new[] { "document_id", "chunk_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_citations_chunk_id",
                table: "citations",
                column: "chunk_id");

            migrationBuilder.CreateIndex(
                name: "IX_citations_message_id_chunk_id",
                table: "citations",
                columns: new[] { "message_id", "chunk_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_citations_message_id_rank",
                table: "citations",
                columns: new[] { "message_id", "rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documents_subject_id",
                table: "documents",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_uploaded_by",
                table: "documents",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "IX_embeddings_chunk_id_model_name",
                table: "embeddings",
                columns: new[] { "chunk_id", "model_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_embeddings_model_name",
                table: "embeddings",
                column: "model_name");

            migrationBuilder.CreateIndex(
                name: "IX_experiment_runs_chunking_strategy_id",
                table: "experiment_runs",
                column: "chunking_strategy_id");

            migrationBuilder.CreateIndex(
                name: "IX_experiment_runs_embedding_model_id",
                table: "experiment_runs",
                column: "embedding_model_id");

            migrationBuilder.CreateIndex(
                name: "IX_experiment_runs_experiment_id",
                table: "experiment_runs",
                column: "experiment_id");

            migrationBuilder.CreateIndex(
                name: "IX_experiments_created_by",
                table: "experiments",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_experiments_subject_id",
                table: "experiments",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "IX_fine_tune_models_experiment_id",
                table: "fine_tune_models",
                column: "experiment_id");

            migrationBuilder.CreateIndex(
                name: "IX_subjects_code",
                table: "subjects",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subjects_created_by",
                table: "subjects",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_test_questions_created_by",
                table: "test_questions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_test_questions_subject_id",
                table: "test_questions",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "benchmark_results");

            migrationBuilder.DropTable(
                name: "citations");

            migrationBuilder.DropTable(
                name: "embeddings");

            migrationBuilder.DropTable(
                name: "fine_tune_models");

            migrationBuilder.DropTable(
                name: "experiment_runs");

            migrationBuilder.DropTable(
                name: "test_questions");

            migrationBuilder.DropTable(
                name: "chat_messages");

            migrationBuilder.DropTable(
                name: "chunks");

            migrationBuilder.DropTable(
                name: "embedding_models");

            migrationBuilder.DropTable(
                name: "experiments");

            migrationBuilder.DropTable(
                name: "chat_sessions");

            migrationBuilder.DropTable(
                name: "chunking_strategies");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropTable(
                name: "subjects");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
