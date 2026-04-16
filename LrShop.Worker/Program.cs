using LrShop.Infrastructure;
using LrShop.Shared;
using LrShop.Worker;

var builder = Host.CreateApplicationBuilder(args);

// CONFIG
builder.Services.Configure<AppSettings>(builder.Configuration);

// BASE
builder.Services.AddSingleton<DbFactory>();
builder.Services.AddSingleton<AnuncioService>();
builder.Services.AddSingleton<ProdutoRepository>();
builder.Services.AddSingleton<LogRepository>();
builder.Services.AddSingleton<MetaTokenService>();
builder.Services.AddSingleton<TelegramService>();
builder.Services.AddSingleton<FacebookService>();
builder.Services.AddSingleton<WhatsappService>();
builder.Services.AddSingleton<CrawlerStateService>();

builder.Services.AddTransient<ShopeeCrawlerService>();

builder.Services.AddScoped<JsonExportService>();

// HTTP CLIENT SERVICES
builder.Services.AddHttpClient<InstagramService>();
builder.Services.AddHttpClient<CloudinaryStorageService>();
builder.Services.AddHttpClient<ShopeeApiService>();
builder.Services.AddHttpClient<ShopeeOfferApiService>();

// WORKER
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// INIT DATABASE
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DbFactory>();
    DatabaseInitializer.Initialize(db);
}

host.Run();