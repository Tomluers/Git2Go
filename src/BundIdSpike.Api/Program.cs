using BundIdSpike.Api;
using BundIdSpike.ServiceInterface;
using ServiceStack;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.AddServiceStack(typeof(HelloService).Assembly);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

// Hinweis: Bewusst ohne natives ASP.NET Core MapEndpoints()/UseAuthorization() - der
// [Authenticate]-Check laeuft ueber ServiceStack's eigene Request-Pipeline (Session-Cookie
// "ss-id", siehe BundIdAuthProvider), nicht ueber das ASP.NET-Core-Authorization-Schema.
app.UseServiceStack(new AppHost());

app.Run();
