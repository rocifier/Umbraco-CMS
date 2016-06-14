using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;

namespace Umbraco.Core.Persistence.NoSqlRepositories
{
    [Serializable]
    internal class XmlTableEntity : TableEntity
    {
        public string Xml { get; set; }
    }

    /// <summary>
    /// Internal class to handle content/published xml insert/update based on standard principles and units of work with transactions
    /// </summary>
    internal class ContentXmlRepository<TContent> : AzureTablesRepositoryBase<int, ContentXmlEntity<TContent>> 
        where TContent : IContentBase
    {
        public ContentXmlRepository(IAzureTablesUnitOfWork work, CacheHelper cache, ILogger logger)
            : base(work, cache, logger)
        {
        }

        #region Not implemented (don't need to for the purposes of this repo)
        protected override ContentXmlEntity<TContent> PerformGet(int id)
        {
            throw new NotImplementedException();
        }

        protected override ContentXmlEntity<TContent> PerformGet(string filter)
        {
            throw new NotImplementedException();
        }

        protected override IEnumerable<ContentXmlEntity<TContent>> PerformGetAll(params int[] ids)
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

        //NOTE: Not implemented because all ContentXmlEntity will always return false for having an Identity
        protected override void PersistUpdatedItem(ContentXmlEntity<TContent> entity)
        {
            throw new NotImplementedException();
        }
        
        #endregion

        protected override void PersistDeletedItem(ContentXmlEntity<TContent> entity)
        {
            //Remove 'published' xml from the cmsContentXml table for the unpublished content...
            CloudTable table = UnitOfWork.Database.GetTableReference("umb_contentxml");

            // Create a retrieve operation that expects a customer entity.
            TableOperation retrieveOperation = TableOperation.Retrieve<XmlTableEntity>("contentxml", entity.Id.ToString());

            // Execute the operation.
            TableResult retrievedResult = table.Execute(retrieveOperation);
            XmlTableEntity retrievedEntity = retrievedResult.Result as XmlTableEntity;
            if (retrievedEntity == null)
            {
                throw new Exception("Wrong schema for xml entity " + entity.Id);
            }

            TableOperation deleteOperation = TableOperation.Delete(retrievedEntity);

            // Execute the operation.
            table.Execute(deleteOperation);
            
        }

        protected override void PersistNewItem(ContentXmlEntity<TContent> entity)
        {
            if (entity.Content.HasIdentity == false)
            {
                throw new InvalidOperationException("Cannot insert or update an xml entry for a content item that has no identity");
            }
            
            CloudTable table = UnitOfWork.Database.GetTableReference("umb_contentxml");
            
            // Assign the result to a CustomerEntity object.
            XmlTableEntity newEntity = new XmlTableEntity();
            newEntity.PartitionKey = "xml";
            newEntity.RowKey = entity.Id.ToString();
            newEntity.Xml = entity.Xml.ToDataString();

            // Create the InsertOrReplace TableOperation.
            TableOperation insertOrReplaceOperation = TableOperation.InsertOrReplace(newEntity);

            // Execute the operation.
            table.Execute(insertOrReplaceOperation);

        }

    }
}