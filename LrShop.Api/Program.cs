using System.Net.Http;
using LrShop.Infrastructure;
using LrShop.Shared;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AppSettings>(builder.Configuration);
builder.Services.AddSingleton<DbFactory>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<AnuncioService>();
builder.Services.AddSingleton<ProdutoRepository>();
builder.Services.AddSingleton<UsuarioRepository>();
builder.Services.AddSingleton<LogRepository>();
builder.Services.AddSingleton(new HttpClient());
builder.Services.AddSingleton<MetaTokenService>();
builder.Services.AddSingleton<TelegramService>();
builder.Services.AddSingleton<InstagramService>();
builder.Services.AddSingleton<FacebookService>();
builder.Services.AddSingleton<ProdutoLinktreeRepository>();
builder.Services.AddSingleton<LinktreeSyncService>();
builder.Services.AddSingleton<LinktreePanelService>();
builder.Services.AddSingleton<LinktreeAutomationService>();
builder.Services.AddSingleton<WhatsappService>();
builder.Services.AddSingleton<ShopeeAffiliateAutomationService>();
builder.Services.AddSingleton<CrawlerStateService>();
builder.Services.AddSingleton<JsonExportService>();

builder.Services.AddHttpClient<StoryVideoComposerService>();
builder.Services.AddHttpClient<CloudinaryStorageService>();
builder.Services.AddHttpClient<InstagramService>();
builder.Services.AddHttpClient<ShopeeCrawlerService>();
builder.Services.AddHttpClient<ShopeeOfferApiService>();
builder.Services.AddHttpClient<ShopeeApiService>();

builder.Services.AddScoped<ShopeeCrawlerService>();

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("open", p =>
        p.AllowAnyOrigin()
         .AllowAnyMethod()
         .AllowAnyHeader());
});

var app = builder.Build();

app.UseCors("open");
app.UseStaticFiles();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DbFactory>();
    DatabaseInitializer.Initialize(db);
}

app.Run("http://localhost:5180");