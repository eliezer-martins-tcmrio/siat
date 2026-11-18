using Tcmrj.Siat.Infra.Configurations;
using Tcmrj.Siat.Web.Configurations;
using Tcmrj.Siat.Web.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddServidoresJson();  // Lista fechada de servidores (servidores.json, recarga automática)

builder.AddDBLogConfiguration();            // Serilog + DBLog em AUDITORIADB.audit.Log
builder.AddRazorConfiguration();            // Blazor (Interactive Server), Razor Pages, Blazor Blueprint, pt-BR
builder.AddAuthenticationConfiguration();   // OIDC no SIA, permissões do UserManager, inatividade, fallback policy
builder.AddTcmrjSiat();                     // Mediador, validadores, services do App, servidores e consultas

var app = builder.Build();

app.UseDBLogConfiguration();                // AppID do SIAT nos registros e log de requisições

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseRazorConfiguration();                // HTTPS, autenticação, autorização, antiforgery e endpoints

app.Run();
