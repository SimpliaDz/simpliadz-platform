using SimpliaDz.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);

var app = builder.Build();

app.UseAppMiddleware();
app.MapAppEndpoints();

app.Run();