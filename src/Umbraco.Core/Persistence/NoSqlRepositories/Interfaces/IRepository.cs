using System;
using System.Collections.Generic;

namespace Umbraco.Core.Persistence.NoSqlRepositories
{
    /// <summary>
	/// Defines the base implementation of a Repository
	/// </summary>
	/// <remarks>
	/// Currently this interface is empty but it is useful for flagging a repository without having generic parameters, it also might
	/// come in handy if we need to add anything to the base/non-generic repository interface.
	/// </remarks>
	public interface IRepository : IDisposable
    {

    }

    public interface IReadRepository<in TId, out TEntity> : IRepository
    {
        /// <summary>
        /// Gets an Entity by Id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        TEntity Get(TId id);

        /// <summary>
        /// Gets all entities under the specific filter
        /// </summary>
        /// <param name="filter">NoSql filter</param>
        /// <returns></returns>
        IEnumerable<TEntity> Get(string filter);

        /// <summary>
        /// Gets all entities of the specified type
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        IEnumerable<TEntity> GetAll(params TId[] ids);

        /// <summary>
        /// Boolean indicating whether an Entity with the specified Id exists
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        bool Exists(TId id);
    }
    
    public interface IWriteRepository<in TId, TEntity> : IRepository
    {
        /// <summary>
        /// Adds or Updates an Entity
        /// </summary>
        /// <param name="entity"></param>
        void AddOrUpdate(TEntity entity);

        /// <summary>
        /// Deletes an Entity
        /// </summary>
        /// <param name="entity"></param>
        void Delete(TEntity entity);
    }

}
