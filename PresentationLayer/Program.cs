namespace PresentationLayer
{
    public class Program
    {
        public static void Main(string[] args)
        {
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
                var localLlm = builder.Configuration.GetSection("LocalLlm");
                var baseUrl = embedding["BaseUrl"] ?? localLlm["BaseUrl"] ?? "http://localhost:11434";
                var model = embedding["Model"] ?? "nomic-embed-text";
                var enabled = !bool.TryParse(embedding["Enabled"], out var parsedEnabled) || parsedEnabled;
                var fallbackToHashing = !bool.TryParse(embedding["FallbackToHashing"], out var parsedFallback) || parsedFallback;
                var timeoutSeconds = int.TryParse(embedding["TimeoutSeconds"], out var parsedTimeout)
                    ? parsedTimeout
                    : 120;

                var httpClient = new HttpClient
                {
                    BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
                    Timeout = TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds))
                };

                return new ServicesLayer.OllamaEmbeddingService(httpClient, model, enabled, fallbackToHashing);
            });
            builder.Services.AddSingleton<ServicesLayer.ILocalChatCompletionService>(_ =>
            {
                var gemini = builder.Configuration.GetSection("Gemini");
                var localLlm = builder.Configuration.GetSection("LocalLlm");
                var provider = builder.Configuration["ChatCompletion:Provider"]
                    ?? gemini["Provider"]
                    ?? "Gemini";

                if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
                {
                    var apiKey = gemini["ApiKey"]
                        ?? builder.Configuration["GEMINI_API_KEY"]
                        ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
                    var model = gemini["Model"] ?? "gemini-3.5-flash";
                    var enabled = !bool.TryParse(gemini["Enabled"], out var parsedEnabled) || parsedEnabled;
                    var timeoutSeconds = int.TryParse(gemini["TimeoutSeconds"], out var parsedTimeout)
                        ? parsedTimeout
                        : 120;

                    return new ServicesLayer.GeminiChatCompletionService(
                        new HttpClient
                        {
                            BaseAddress = new Uri("https://generativelanguage.googleapis.com/"),
                            Timeout = TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds))
                        },
                        model,
                        apiKey,
                        enabled);
                }

                var baseUrl = localLlm["BaseUrl"] ?? "http://localhost:11434";
                var ollamaModel = localLlm["Model"] ?? "qwen2.5:3b";
                var ollamaEnabled = !bool.TryParse(localLlm["Enabled"], out var parsedOllamaEnabled) || parsedOllamaEnabled;
                var ollamaTimeoutSeconds = int.TryParse(localLlm["TimeoutSeconds"], out var parsedOllamaTimeout)
                    ? parsedOllamaTimeout
                    : 120;

                var httpClient = new HttpClient
                {
                    BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
                    Timeout = TimeSpan.FromSeconds(Math.Max(5, ollamaTimeoutSeconds))
                };

                return new ServicesLayer.OllamaChatCompletionService(httpClient, ollamaModel, ollamaEnabled);
            });
            builder.Services.AddSingleton<ServicesLayer.IDocumentTextExtractor, ServicesLayer.DocumentTextExtractor>();
            builder.Services.AddSingleton<ServicesLayer.IWebPageTextExtractor>(_ =>
                new ServicesLayer.WebPageTextExtractor(new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(35)
                }));
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
