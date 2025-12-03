using CourseRegistrationSystem.DAL.DataContext;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.DAL.Contracts;
using System.Linq.Expressions;

namespace StudentManagementSystem.DAL.Repositories
{
    internal class BaseRepository <T> : IBaseRepository<T> where T : class
    {
        private readonly AppDbContext dbContext;
        private DbSet<T> dbset;
        public BaseRepository(AppDbContext dbContext)
        {
            this.dbContext = dbContext;
            this.dbset = dbContext.Set<T>();
        }
        public async Task AddAsync(T entity)
        {
            await dbset.AddAsync(entity);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await dbset.FindAsync(id);
            if (entity != null)
            {
                dbset.Remove(entity);
                return true;
            }
            return false; //false if not found to be deleted
        }

        public IQueryable<T> GetAllAsQueryable()
        {
            return dbset.AsQueryable();
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await dbset.ToListAsync();
        }

        public async Task<T?> GetByIdAsync(int id)
        {
            return await dbset.FindAsync(id); //will return entity if found, null otherwise
        }

        public void Update(T entity)
        {
            dbset.Update(entity);
        }
        public IQueryable<TDestination> GetFilteredAndProjected<TDestination>(Expression<Func<T, bool>> filter, Expression<Func<T, TDestination>> projection)
        where TDestination : class
        {
            var query = GetAllAsQueryable();
            if (filter != null)
            {
                query = query.Where(filter); //here i apply filtering on entire table
            }
            //then we project, apply mapping to destination DTO
            return query.Select(projection);

        }
        //public IQueryable<T> GetQueryWithFilter(Expression<Func<T, bool>> filter) 
        //{
        //    // Start with the full DbSet
        //    IQueryable<T> query = dbset;

        //    // Apply the filtering expression
        //    if (filter != null)
        //    {
        //        query = query.Where(filter);
        //    }

        //    return query;
        //}
    }
}
