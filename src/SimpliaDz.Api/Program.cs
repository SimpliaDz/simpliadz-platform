using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Diagnostics;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using FluentValidation;
using MediatR;

using SimpliaDz.Identity.Infrastructure.Data;
using SimpliaDz.Identity.Infrastructure.Data.Entities;
using SimpliaDz.BuildingBlocks.Mediation;

var builder = WebApplication.CreateBuilder(args);

// Module assemblies (UseCases)
var inventoryAssembly = typeof(SimpliaDz.Inventory.UseCases.AssemblyMarker).Assembly;
var identityAssembly = typeof(SimpliaDz.Identity.UseCases.AssemblyMarker).Assembly;


// --------------------
// Swagger (dev-friendly)
// --------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --------------------
// Load dev tenant connection string (temporary)
// Later we’ll resolve tenant per request.
// --------------------
var devConnectionString = builder.Configuration["Tenancy:Tenants:dev:ConnectionString"];
if (string.IsNullOrWhiteSpace(devConnectionString))
{
    throw new InvalidOperationException("Missing Tenancy:Tenants:dev:ConnectionString in API appsettings.json");
}

// --------------------
// Identity DbContext + Identity services
// --------------------
builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseSqlServer(devConnectionString));

builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<AppRole>()
    .AddEntityFrameworkStores<IdentityDbContext>()
    .AddSignInManager();


// ----------------------------------------------------------------    MediatR: register handlers from UseCases assemblies -------------------------------
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
    inventoryAssembly,
    identityAssembly
));


//--------------------------------------------------------------------- Fluent Validation  Validators ------------------------------------------------------
builder.Services.AddValidatorsFromAssemblies(
    [
        inventoryAssembly,
        identityAssembly
    ]
);

// Pipeline behavior (shared)
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));



// --------------------
// JWT Auth
// --------------------
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SimpliaDz";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "SimpliaDz";
var jwtKey = builder.Configuration["Jwt:SigningKey"];

if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey is missing or too short (min 32 chars).");
}

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();


//----------------------------------------------------------------------- API: translate validation exceptions to HTTP 400 (Move this in a seprate File ) ----------------------------------

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        if (exception is ValidationException vex)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                Error = "ValidationFailed",
                Details = vex.Errors.Select(e => new
                {
                    e.PropertyName,
                    e.ErrorMessage
                })
            });
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { Error = "ServerError" });
    });
});



// ------------------------------------------------------------------------------- API ENDPOINTS FOR TESTING --------------------------------------------------------
// --------------------
// Minimal API endpoints
// --------------------

int expiresMinutes = int.TryParse(builder.Configuration["Jwt:ExpiresMinutes"], out var m) ? m : 60;

string CreateJwt(AppUser user, IList<string> roles)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Email, user.Email ?? ""),
        new(ClaimTypes.Name, user.UserName ?? user.Email ?? "")
    };

    foreach (var role in roles)
        claims.Add(new Claim(ClaimTypes.Role, role));

    var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: jwtIssuer,
        audience: jwtAudience,
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
        signingCredentials: creds);

    return new JwtSecurityTokenHandler().WriteToken(token);
}

app.MapPost("/auth/login", async (
    LoginRequest req,
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager) =>
{
    var user = await userManager.FindByEmailAsync(req.Email);
    if (user is null)
        return Results.Unauthorized();

    var result = await signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
    if (!result.Succeeded)
        return Results.Unauthorized();

    var roles = await userManager.GetRolesAsync(user);
    var token = CreateJwt(user, roles);

    return Results.Ok(new LoginResponse(token, expiresMinutes));
});

app.MapGet("/auth/me", (ClaimsPrincipal principal) =>
{
    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    var email = principal.FindFirstValue(ClaimTypes.Email);
    var roles = principal.FindAll(ClaimTypes.Role).Select(r => r.Value).ToArray();

    return Results.Ok(new { userId, email, roles });
}).RequireAuthorization();

app.MapGet("/", () => Results.Ok(new { status = "ok" }));

app.Run();

record LoginRequest(string Email, string Password);
record LoginResponse(string AccessToken, int ExpiresMinutes);