using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddNewtonsoftJson();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS ekle
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Rastgele port oluştur (5000-9999 arası)
var random = new Random();
var port = random.Next(5000, 10000);
var url = $"http://localhost:{port}";

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

// Static files için wwwroot
app.UseStaticFiles();
app.UseDefaultFiles();

app.UseAuthorization();
app.MapControllers();

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("╔════════════════════════════════════════════════════════╗");
Console.WriteLine("║        🚀 RebIQ Akıllı Arama Motoru                   ║");
Console.WriteLine("╚════════════════════════════════════════════════════════╝");
Console.ResetColor();
Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"✅ API Port     : {port}");
Console.WriteLine($"🌐 Web Arayüz   : {url}/index.html");
Console.WriteLine($"📚 Swagger API  : {url}/swagger");
Console.ResetColor();
Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("💡 Not: Port her çalıştırmada otomatik değişir!");
Console.ResetColor();
Console.WriteLine();
Console.WriteLine("▶ Sunucu çalışıyor... Durdurmak için CTRL+C");
Console.WriteLine(new string('─', 56));

app.Run(url);
