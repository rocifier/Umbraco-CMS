using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence.SqlSyntax;

namespace Umbraco.Core.Persistence.NoSqlRepositories
{

    /// <summary>
    /// Private class to handle preview insert/update based on standard principles and units of work with transactions
    /// </summary>
    internal class ContentPreviewRepository<TContent> : AzureTablesRepositoryBase<int, ContentPreviewEntity<TContent>> 
        where TContent : IContentBase
    {
        public ContentPreviewRepository(IAzureTablesUnitOfWork work, CacheHelper cache, ILogger logger)
            : base(work, cache, logger)
        {
        }

        #region Not implemented (don't need to for the purposes of this repo)
        protected override ContentPreviewEntity<TContent> PerformGet(int id)
        {
            throw new NotImplementedException();
        }

        protected override IEnumerable<ContentPreviewEntity<TContent>> PerformGetAll(params int[] ids)
        {
            throw new NotImplementedException();
        }
        
        protected override ContentPreviewEntity<TContent> PerformGet(string filter)
        {
            throw new NotImplementedException();
        }

        protected override Sql GetBaseQuery(bool isCount)
        {
            throw new NotImplementedException();
        }

        protected override string GetBaseWhereClause()
        {
            throw new NotImplementedException();
        }

        protected override IEnumerable<string> GetDeleteClauses()
        {
            return new List<string>();
        }

        protected override Guid NodeObjectTypeId
        {
            get { throw new NotImplementedException(); }
        }

        protected override void PersistDeletedItem(ContentPreviewEntity<TContent> entity)
        {
            throw new NotImplementedException();
        }

        //NOTE: Not implemented because all ContentPreviewEntity will always return false for having an Identity
        protected override void PersistUpdatedItem(ContentPreviewEntity<TContent> entity)
        {
            throw new NotImplementedException();
        }

        #endregion

        protected override void PersistNewItem(ContentPreviewEntity<TContent> entity)
        {
            if (entity.Content.HasIdentity == false)
            {
                throw new InvalidOperationException("Cannot insert or update a preview for a content item that has no identity");
            }
            CloudTable table = UnitOfWork.Database.GetTableReference("umb_previewxml");

            // Assign the result to a CustomerEntity object.
            XmlTableEntity newEntity = new XmlTableEntity();
            newEntity.PartitionKey = entity.Version.ToString();
            newEntity.RowKey = entity.Id.ToString();
            newEntity.Xml = entity.Xml.ToDataString();
            
            // Create the InsertOrReplace TableOperation.
            TableOperation insertOrReplaceOperation = TableOperation.InsertOrReplace(newEntity);

            // Execute the operation.
            table.Execute(insertOrReplaceOperation);
            
        }

    }
}