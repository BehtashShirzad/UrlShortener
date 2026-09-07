using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<ShortLinkDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("shortlinks"));
});

var app = builder.Build();

using var scope = app.Services.CreateScope();

var dbContext =
    scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();

await dbContext.Database.MigrateAsync();