using PresentationLayer.Security;

namespace PresentationLayer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
            var builder = WebApplication.CreateBuilder(args);

            static string FirstConfigured(params string?[] values)
            {
                return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
            }

            builder.Services.AddControllersWithViews();
            var authenticationBuilder = builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
            });
            authenticationBuilder.AddCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.Cookie.Name = "CourseAssistant.Auth";
            });
            authenticationBuilder.AddCookie("External", options =>
            {
                options.Cookie.Name = "CourseAssistant.External";
                options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            });

            var googleClientId = FirstConfigured(
                builder.Configuration["Authentication:Google:ClientId"],
                builder.Configuration["GOOGLE_CLIENT_ID"],
                Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID"));
            var googleClientSecret = FirstConfigured(
                builder.Configuration["Authentication:Google:ClientSecret"],
                builder.Configuration["GOOGLE_CLIENT_SECRET"],
                Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET"));
            if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
            {
                authenticationBuilder.AddGoogle(options =>
                {
                    options.ClientId = googleClientId;
                    options.ClientSecret = googleClientSecret;
                    options.CallbackPath = "/signin-google";
                    options.SignInScheme = "External";
                    options.SaveTokens = false;
                });
            }
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy(AuthorizationPolicies.ChatAccess, policy =>
                    policy.RequireRole(AppRoles.Student, AppRoles.Lecturer, AppRoles.Admin));
                options.AddPolicy(AuthorizationPolicies.DocumentManagement, policy =>
                    policy.RequireRole(AppRoles.Lecturer, AppRoles.Admin));
                options.AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
                    policy.RequireRole(AppRoles.Admin));
            });
            var geminiSection = builder.Configuration.GetSection("Gemini");
            var geminiApiKey = FirstConfigured(
                geminiSection["ApiKey"],
                builder.Configuration["GEMINI_API_KEY"],
                Environment.GetEnvironmentVariable("GEMINI_API_KEY"));
            var geminiEnabled = !bool.TryParse(geminiSection["Enabled"], out var parsedGeminiEnabled) || parsedGeminiEnabled;
            var geminiChatModel = geminiSection["Model"] ?? "gemini-3.5-flash";
            var geminiEmbeddingModel = builder.Configuration["Embedding:Model"]
                ?? geminiSection["EmbeddingModel"]
                ?? "gemini-embedding-001";
            var geminiEmbeddingDimensions = int.TryParse(
                    builder.Configuration["Embedding:OutputDimensionality"] ?? geminiSection["EmbeddingOutputDimensionality"],
                    out var parsedEmbeddingDimensions)
                ? parsedEmbeddingDimensions
                : 768;
            var geminiTimeoutSeconds = int.TryParse(geminiSection["TimeoutSeconds"], out var parsedGeminiTimeout)
                ? parsedGeminiTimeout
                : 120;
            builder.Services.AddSingleton(new ServicesLayer.GeminiApiOptions(
                geminiApiKey,
                geminiChatModel,
                geminiEmbeddingModel,
                geminiEmbeddingDimensions,
                geminiEnabled));
            var huggingFaceApiKey = FirstConfigured(
                builder.Configuration["HuggingFace:ApiKey"],
                builder.Configuration["HUGGINGFACE_API_KEY"],
                Environment.GetEnvironmentVariable("HUGGINGFACE_API_KEY"));
            var huggingFaceBaseAddress = builder.Configuration["HuggingFace:BaseAddress"]
                ?? Environment.GetEnvironmentVariable("HUGGINGFACE_BASE_ADDRESS")
                ?? "https://api-inference.huggingface.co/";
            var huggingFaceEnabled = !bool.TryParse(builder.Configuration["HuggingFace:Enabled"], out var parsedHuggingFaceEnabled)
                || parsedHuggingFaceEnabled;
            builder.Services.AddSingleton(new ServicesLayer.HuggingFaceApiOptions(
                huggingFaceApiKey,
                huggingFaceBaseAddress,
                huggingFaceEnabled));
            builder.Services.AddSingleton<DataAccessLayer.IKnowledgeRepository>(_ =>
            {
                var repository = new DataAccessLayer.Repositories.SqlKnowledgeRepository(
                    builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty);
                repository.ImportFromJsonIfEmptyAsync(
                    Path.Combine(builder.Environment.ContentRootPath, "App_Data", "rag-store.json")).GetAwaiter().GetResult();
                return repository;
            });
            builder.Services.AddSingleton<DataAccessLayer.IResearchRepository>(_ =>
                new DataAccessLayer.Repositories.SqlResearchRepository(
                    builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty));
            builder.Services.AddSingleton<PresentationLayer.Services.IUserAccountStore>(_ =>
            {
                var seedAdminSection = builder.Configuration.GetSection("SeedAdmin");
                var seedAdminEnabled = !bool.TryParse(seedAdminSection["Enabled"], out var parsedSeedAdminEnabled)
                                       || parsedSeedAdminEnabled;

                return new PresentationLayer.Services.UserAccountStore(
                    Path.Combine(builder.Environment.ContentRootPath, "App_Data", "users.json"),
                    new PresentationLayer.Services.SeedAdminOptions(
                        seedAdminEnabled,
                        seedAdminSection["FullName"] ?? "System Admin",
                        seedAdminSection["Email"] ?? "admin@eduvietrag.local",
                        seedAdminSection["Password"] ?? "Admin@12345"));
            });
            builder.Services.AddSingleton<ServicesLayer.IEmbeddingService>(_ =>
            {
                var embedding = builder.Configuration.GetSection("Embedding");
                var timeoutSeconds = int.TryParse(embedding["TimeoutSeconds"], out var parsedTimeout)
                    ? parsedTimeout
                    : 120;

                var geminiEmbeddingService = new ServicesLayer.GeminiEmbeddingService(
                    new HttpClient
                    {
                        BaseAddress = new Uri("https://generativelanguage.googleapis.com/"),
                        Timeout = TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds))
                    },
                    new ServicesLayer.GeminiApiOptions(
                        geminiApiKey,
                        geminiChatModel,
                        geminiEmbeddingModel,
                        geminiEmbeddingDimensions,
                        geminiEnabled));

                return new ServicesLayer.FallbackEmbeddingService(
                    geminiEmbeddingService,
                    new ServicesLayer.HashingEmbeddingService());
            });
            builder.Services.AddSingleton<ServicesLayer.ILocalChatCompletionService>(_ =>
            {
                return new ServicesLayer.GeminiChatCompletionService(
                    new HttpClient
                    {
                        BaseAddress = new Uri("https://generativelanguage.googleapis.com/"),
                        Timeout = TimeSpan.FromSeconds(Math.Max(5, geminiTimeoutSeconds))
                    },
                    geminiChatModel,
                    geminiApiKey,
                    geminiEnabled);
            });
            builder.Services.AddSingleton<ServicesLayer.IDocumentTextExtractor, ServicesLayer.DocumentTextExtractor>();
            builder.Services.AddSingleton<ServicesLayer.ITextChunker, ServicesLayer.ParagraphAwareTextChunker>();
            builder.Services.AddSingleton<ServicesLayer.IDocumentIndexJobQueue, ServicesLayer.DocumentIndexJobQueue>();
            builder.Services.AddSingleton<ServicesLayer.IWebPageTextExtractor>(_ =>
                new ServicesLayer.WebPageTextExtractor(new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(35)
                }));
            builder.Services.AddSingleton<PresentationLayer.Services.IResearchReportPdfService, PresentationLayer.Services.ResearchReportPdfService>();
            builder.Services.AddScoped<ServicesLayer.IDocumentIndexingService, ServicesLayer.DocumentIndexingService>();
            builder.Services.AddScoped<ServicesLayer.IRagChatService, ServicesLayer.RagChatService>();
            builder.Services.AddHostedService<PresentationLayer.Services.DocumentIndexWorker>();
            builder.Services.AddHttpClient<ServicesLayer.IResearchBenchmarkService, ServicesLayer.ResearchBenchmarkService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(180);
            });

            var app = builder.Build();
            _ = app.Services.GetRequiredService<DataAccessLayer.IKnowledgeRepository>();
            _ = app.Services.GetRequiredService<PresentationLayer.Services.IUserAccountStore>()
                .HasAnyUsersAsync()
                .GetAwaiter()
                .GetResult();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}
