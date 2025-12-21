using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RegMan.Backend.DAL.Contracts;
using RegMan.Backend.DAL.DataContext;
using System.Linq.Expressions;

namespace RegMan.Backend.DAL
{
    public static class DataAccessExtension
    {
        public static IServiceCollection AddDataBaseLayer(this IServiceCollection service, IConfiguration configuration)
        {
            service.AddScoped<IUnitOfWork, UnitOfWork>();
            //service.AddDbContext<AppDbContext>(options=> { options.UseSqlServer("Data Source=.;Initial Catalog=RegManDb;Integrated Security=True;Trust Server Certificate=True"); });
            service.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("NEW_CONNECTION_STRING")));

            return service;
        }
    }
}
