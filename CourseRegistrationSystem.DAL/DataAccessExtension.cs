using CourseRegistrationSystem.DAL.DataContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudentManagementSystem.DAL.Contracts;
using StudentManagementSystem.DAL.DataContext;
using StudentManagementSystem.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.DAL
{
    public static class DataAccessExtension
    {
        public static IQueryable<TDestination> ProjectTo<TEntity, TDestination>(this IQueryable<TEntity> source, Expression<Func<TEntity, TDestination>> projection)
            where TEntity : class
            where TDestination : class
        {
            // Simply applies the provided Select projection to the IQueryable.
            // Entity Framework Core/LINQ Provider translates this into efficient SQL SELECT.
            return source.Select(projection);
        }
      
        public static IServiceCollection AddDataBaseLayer( this IServiceCollection service, IConfigurationManager configuration) 
        {
            service.AddScoped<IUnitOfWork, UnitOfWork>();
            service.AddDbContext<AppDbContext>(options=> { options.UseSqlServer(configuration.GetConnectionString("ConnectionString")); });
            return service;
        }
    }
}
