using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.DAL.Contracts
{
    public interface IBaseRepository<T> where T : class
    {
        Task<IEnumerable<T>> GetAllAsync();
        Task<T?> GetByIdAsync(int id);
        Task AddAsync(T entity);
        void Update(T entity);
        Task<bool> DeleteAsync(int id);
        IQueryable<T> GetAllAsQueryable();
        IQueryable<TDestination> GetFilteredAndProjected<TDestination>(Expression<Func<T, bool>> filter, Expression<Func<T, TDestination>> projection) where TDestination: class;

    }
}
