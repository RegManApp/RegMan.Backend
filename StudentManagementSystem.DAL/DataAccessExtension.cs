using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudentManagementSystem.DAL.Contracts;
using StudentManagementSystem.DAL.DataContext;
using System.Linq.Expressions;

namespace StudentManagementSystem.DAL
{
    public static class DataAccessExtension
    {     
        public static IServiceCollection AddDataBaseLayer( this IServiceCollection service, IConfiguration configuration) 
        {
            service.AddScoped<IUnitOfWork, UnitOfWork>();
            //service.AddDbContext<AppDbContext>(options=> { options.UseSqlServer("Data Source=.;Initial Catalog=StudentManagementDb;Integrated Security=True;Trust Server Certificate=True"); });
            service.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            return service;
        }
    }
}
