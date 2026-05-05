using Microsoft.EntityFrameworkCore;
using MixMe.Data;
using MixMe.Hubs;
using MixMe.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Настройка базы данных PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Регистрация HttpClient (необходим для работы IHttpClientFactory в PaymentService)
builder.Services.AddHttpClient();

// 3. Регистрация вашего сервиса оплаты
builder.Services.AddScoped<PaymentService>();

// 4. Настройка контроллеров и Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 5. Настройка CORS для связи с Blazor
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor", policy =>
        policy.SetIsOriginAllowed(origin => true) // Разрешить любые локальные порты для теста
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});

builder.Services.AddSignalR();
var app = builder.Build();

// Настройка конвейера запросов (Middleware)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // <--- ДОБАВЬТЕ ЭТУ СТРОКУ


// Важно: Cors должен идти перед Authorization
app.UseCors("AllowBlazor");

app.MapHub<ChatHub>("/chathub");

app.UseAuthorization();

app.MapControllers();

app.Run();