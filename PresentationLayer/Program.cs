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
                new DataAccessLayer.JsonKnowledgeRepository(
                    Path.Combine(builder.Environment.ContentRootPath, "App_Data", "rag-store.json")));
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
                var localLlm = builder.Configuration.GetSection("LocalLlm");
                var baseUrl = localLlm["BaseUrl"] ?? "http://localhost:11434";
                var model = localLlm["Model"] ?? "gemma4:latest";
                var enabled = !bool.TryParse(localLlm["Enabled"], out var parsedEnabled) || parsedEnabled;
                var timeoutSeconds = int.TryParse(localLlm["TimeoutSeconds"], out var parsedTimeout)
                    ? parsedTimeout
                    : 120;

                var httpClient = new HttpClient
                {
                    BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
                    Timeout = TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds))
                };

                return new ServicesLayer.OllamaChatCompletionService(httpClient, model, enabled);
            });
            builder.Services.AddSingleton<ServicesLayer.IDocumentTextExtractor, ServicesLayer.DocumentTextExtractor>();
            builder.Services.AddSingleton<ServicesLayer.IWebPageTextExtractor>(_ =>
                new ServicesLayer.WebPageTextExtractor(new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(35)
                }));
            builder.Services.AddScoped<ServicesLayer.IDocumentIndexingService, ServicesLayer.DocumentIndexingService>();
            builder.Services.AddScoped<ServicesLayer.IRagChatService, ServicesLayer.RagChatService>();

            var app = builder.Build();

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
