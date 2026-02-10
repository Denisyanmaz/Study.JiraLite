using JiraLite.Api.Filters;
using JiraLite.Api.Services;
using JiraLite.Application.Interfaces;
using JiraLite.Infrastructure.Persistence;
using JiraLite.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NSwag;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

// ✅ VERY IMPORTANT: stop ASP.NET from remapping JWT claims (sub → NameIdentifier)
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var builder = WebApplication.CreateBuilder(args);

// 🔹 Add services to the container
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed"
            };
            return new BadRequestObjectResult(problem);
        };
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
// 🔹 NSwag / Swagger UI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config =>
{
    config.Title = "JiraLite API";

    config.AddSecurity("JWT", Enumerable.Empty<string>(), new OpenApiSecurityScheme
    {
        Type = OpenApiSecuritySchemeType.ApiKey,
        Name = "Authorization",
        In = OpenApiSecurityApiKeyLocation.Header,
        Description = "Enter 'Bearer {your token here}'"
    });
});

// 🔹 Database
// 🔹 Database (always Postgres; tests override the connection string)
builder.Services.AddDbContext<JiraLiteDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));


// 🔹 Dependency Injection
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ITaskService, TaskService>();

// 🔹 JWT Authentication
var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false; // ✅ critical fix

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],

        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],

        ValidateLifetime = true,

        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuerSigningKey = true,

        // ✅ Explicit claim mapping
        NameClaimType = "id",
        RoleClaimType = ClaimTypes.Role
    };
});
builder.Services.AddTransient<JiraLite.Api.Middleware.ExceptionHandlingMiddleware>();
builder.Services.AddAuthorization();

var app = builder.Build();

// 🔹 Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi();
}
app.UseMiddleware<JiraLite.Api.Middleware.ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
