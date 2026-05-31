namespace PresentationLayer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllersWithViews();
            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
                })
                .AddCookie(options =>
                {
                    options.LoginPath = "/Account/Login";
                    options.LogoutPath = "/Account/Logout";
                    options.AccessDeniedPath = "/Account/Login";
                    options.Cookie.Name = "CourseAssistant.Auth";
                })
                .AddCookie("External", options =>
                {
                    options.Cookie.Name = "CourseAssistant.External";
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                })
                .AddGoogle(options =>
                {
                    options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? string.Empty;
                    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? string.Empty;
                    options.CallbackPath = "/signin-google";
                    options.SignInScheme = "External";
                    options.SaveTokens = false;
                });
            var geminiSection = builder.Configuration.GetSection("Gemini");
            var geminiApiKey = geminiSection["ApiKey"]
                ?? builder.Configuration["GEMINI_API_KEY"]
                ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                ?? string.Empty;
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
                new PresentationLayer.Services.UserAccountStore(
                    Path.Combine(builder.Environment.ContentRootPath, "App_Data", "users.json")));
            builder.Services.AddSingleton<ServicesLayer.IEmbeddingService>(_ =>
            {
                var embedding = builder.Configuration.GetSection("Embedding");
                var timeoutSeconds = int.TryParse(embedding["TimeoutSeconds"], out var parsedTimeout)
                    ? parsedTimeout
                    : 120;

                return new ServicesLayer.GeminiEmbeddingService(
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
            builder.Services.AddSingleton<ServicesLayer.IWebPageTextExtractor>(_ =>
                new ServicesLayer.WebPageTextExtractor(new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(35)
                }));
            builder.Services.AddSingleton<PresentationLayer.Services.IResearchReportPdfService, PresentationLayer.Services.ResearchReportPdfService>();
            builder.Services.AddScoped<ServicesLayer.IDocumentIndexingService, ServicesLayer.DocumentIndexingService>();
            builder.Services.AddScoped<ServicesLayer.IRagChatService, ServicesLayer.RagChatService>();
            builder.Services.AddHttpClient<ServicesLayer.IResearchBenchmarkService, ServicesLayer.ResearchBenchmarkService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(180);
            });

            var app = builder.Build();
            _ = app.Services.GetRequiredService<DataAccessLayer.IKnowledgeRepository>();

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
