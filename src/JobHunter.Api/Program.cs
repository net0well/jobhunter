using JobHunter.Api.Common.Endpoints;
using JobHunter.Api.Common.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

app.MapEndpoints();

app.Run();
