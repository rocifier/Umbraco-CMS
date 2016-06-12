using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.EntityBase;

namespace Umbraco.Core.Persistence.NoSqlRepositories
{
    /// <summary>
    /// Represent an abstract Repository for PetaPoco based repositories
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    /// <typeparam name="TEntity"></typeparam>
    internal abstract class AzureTablesRepositoryBase<TId, TEntity> : RepositoryBase<TId, TEntity>
        where TEntity : class, IAggregateRoot
    {

        protected AzureTablesRepositoryBase(IAzureTablesUnitOfWork work, CacheHelper cache, ILogger logger)
            : base(work, cache, logger)
        {
        }

        /// <summary>
		/// Returns the database Unit of Work added to the repository
		/// </summary>
		protected internal new IAzureTablesUnitOfWork UnitOfWork
        {
            get { return (IAzureTablesUnitOfWork)base.UnitOfWork; }
        }

        protected CloudTableClient Database
        {
            get { return UnitOfWork.Database; }
        }

        #region Abstract Methods

        protected abstract Sql GetBaseQuery(bool isCount);
        protected abstract string GetBaseWhereClause();
        protected abstract IEnumerable<string> GetDeleteClauses();
        protected abstract Guid NodeObjectTypeId { get; }
        protected abstract override void PersistNewItem(TEntity entity);
        protected abstract override void PersistUpdatedItem(TEntity entity);

        #endregion

        protected override bool PerformExists(TId id)
        {
            throw new NotImplementedException();
        }

        protected override int PerformCount(string filter)
        {
            throw new NotImplementedException();
        }

        protected override void PersistDeletedItem(TEntity entity)
        {
            throw new NotImplementedException();
        }
        
    }
}
