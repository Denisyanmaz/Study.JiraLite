using JiraLite.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // dev-friendly
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// HttpContext needed for reading cookie in HttpClient handler
builder.Services.AddHttpContextAccessor();

// Message handler that attaches Bearer token from cookie
builder.Services.AddTransient<JwtCookieHandler>();

builder.Services.AddHttpClient("JiraLiteApi", client =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"];
    client.BaseAddress = new Uri(baseUrl!);
})
.AddHttpMessageHandler<JwtCookieHandler>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
