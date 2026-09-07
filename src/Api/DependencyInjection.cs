using Infrastructure;
using Application;
using Domain;
namespace Api
{
    public static class DependencyInjection
    {

        public static IServiceCollection AddUrlShortlinkServices(this IServiceCollection services,IConfiguration configuration)
        {

            services.AddDomainServices();
            services.AddApplicationServices();
            services.AddInfrastructureServices(configuration);

            return services;
        }
    }
}
