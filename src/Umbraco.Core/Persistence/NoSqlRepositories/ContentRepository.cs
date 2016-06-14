using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Umbraco.Core.Dynamics;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.EntityBase;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.Models.Rdbms;
using Umbraco.Core.Cache;
using Umbraco.Core.Persistence.DatabaseModelDefinitions;
using Umbraco.Core.Persistence.Factories;
using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.Persistence.NoSqlRepositories.Interfaces;

namespace Umbraco.Core.Persistence.NoSqlRepositories
{
    /// <summary>
    /// Represents a repository for doing CRUD operations for <see cref="IContent"/>
    /// </summary>
    internal class ContentRepository : AzureRecycleBinRepository<int, IContent>, IContentRepository
    {
        private readonly Repositories.IContentTypeRepository _contentTypeRepository;
        private readonly Repositories.ITemplateRepository _templateRepository;
        private readonly Repositories.ITagRepository _tagRepository;
        private readonly CacheHelper _cacheHelper;
        private readonly ContentPreviewRepository<IContent> _contentPreviewRepository;
        private readonly ContentXmlRepository<IContent> _contentXmlRepository;

        public ContentRepository(IAzureTablesUnitOfWork work, CacheHelper cacheHelper, ILogger logger, Repositories.IContentTypeRepository contentTypeRepository, 
            Repositories.ITemplateRepository templateRepository, Repositories.ITagRepository tagRepository, IContentSection contentSection)
            : base(work, cacheHelper, logger, contentSection)
        {
            if (contentTypeRepository == null) throw new ArgumentNullException("contentTypeRepository");
            if (templateRepository == null) throw new ArgumentNullException("templateRepository");
            if (tagRepository == null) throw new ArgumentNullException("tagRepository");
            _contentTypeRepository = contentTypeRepository;
            _templateRepository = templateRepository;
            _tagRepository = tagRepository;
            _cacheHelper = cacheHelper;
            _contentPreviewRepository = new ContentPreviewRepository<IContent>(work, CacheHelper.CreateDisabledCacheHelper(), logger);
            _contentXmlRepository = new ContentXmlRepository<IContent>(work, CacheHelper.CreateDisabledCacheHelper(), logger);

            EnsureUniqueNaming = true;
        }

        public bool EnsureUniqueNaming { get; set; }

        #region Overrides of RepositoryBase<IContent>

        protected override IContent PerformGet(int id)
        {
            var sql = GetBaseQuery(false)
                .Where(GetBaseWhereClause(), new { Id = id })
                .Where<DocumentDto>(x => x.Newest)
                .OrderByDescending<ContentVersionDto>(x => x.VersionDate);

            var dto = Database.Fetch<DocumentDto, ContentVersionDto, ContentDto, NodeDto, DocumentPublishedReadOnlyDto>(sql).FirstOrDefault();

            if (dto == null)
                return null;

            var content = CreateContentFromDto(dto, dto.ContentVersionDto.VersionId, sql);

            return content;
        }

        protected override IContent PerformGet(string filter)
        {
            throw new NotImplementedException();
        }

        protected override IEnumerable<IContent> PerformGetAll(params int[] ids)
        {
            var sql = GetBaseQuery(false);
            if (ids.Any())
            {
                sql.Where("umbracoNode.id in (@ids)", new { ids = ids });
            }

            //we only want the newest ones with this method
            sql.Where<DocumentDto>(x => x.Newest);

            return ProcessQuery(sql);
        }
        
        #endregion
        
        #region Unit of Work Implementation

        protected override void PersistDeletedItem(IContent entity)
        {
            //We need to clear out all access rules but we need to do this in a manual way since 
            // nothing in that table is joined to a content id
            var subQuery = new Sql()
                .Select("umbracoAccessRule.accessId")
                .From<AccessRuleDto>(SqlSyntax)
                .InnerJoin<AccessDto>(SqlSyntax)
                .On<AccessRuleDto, AccessDto>(SqlSyntax, left => left.AccessId, right => right.Id)
                .Where<AccessDto>(dto => dto.NodeId == entity.Id);
            Database.Execute(SqlSyntax.GetDeleteSubquery("umbracoAccessRule", "accessId", subQuery));

            //now let the normal delete clauses take care of everything else
            base.PersistDeletedItem(entity);
        }

        protected override void PersistNewItem(IContent entity)
        {
            ((Content)entity).AddingEntity();

            //ensure the default template is assigned
            if (entity.Template == null)
            {
                entity.Template = entity.ContentType.DefaultTemplate;
            }

            //Ensure unique name on the same level
            entity.Name = EnsureUniqueNodeName(entity.ParentId, entity.Name);

            //Ensure that strings don't contain characters that are invalid in XML
            entity.SanitizeEntityPropertiesForXmlStorage();

            var factory = new ContentFactory(NodeObjectTypeId, entity.Id);
            var dto = factory.BuildDto(entity);

            //NOTE Should the logic below have some kind of fallback for empty parent ids ?
            //Logic for setting Path, Level and SortOrder
            var parent = Database.First<NodeDto>("WHERE id = @ParentId", new { ParentId = entity.ParentId });
            var level = parent.Level + 1;
            var maxSortOrder = Database.ExecuteScalar<int>(
                "SELECT coalesce(max(sortOrder),-1) FROM umbracoNode WHERE parentid = @ParentId AND nodeObjectType = @NodeObjectType",
                new { /*ParentId =*/ entity.ParentId, NodeObjectType = NodeObjectTypeId });
            var sortOrder = maxSortOrder + 1;

            //Create the (base) node data - umbracoNode
            var nodeDto = dto.ContentVersionDto.ContentDto.NodeDto;
            nodeDto.Path = parent.Path;
            nodeDto.Level = short.Parse(level.ToString(CultureInfo.InvariantCulture));
            nodeDto.SortOrder = sortOrder;
            var o = Database.IsNew(nodeDto) ? Convert.ToInt32(Database.Insert(nodeDto)) : Database.Update(nodeDto);

            //Update with new correct path
            nodeDto.Path = string.Concat(parent.Path, ",", nodeDto.NodeId);
            Database.Update(nodeDto);

            //Update entity with correct values
            entity.Id = nodeDto.NodeId; //Set Id on entity to ensure an Id is set
            entity.Path = nodeDto.Path;
            entity.SortOrder = sortOrder;
            entity.Level = level;

            //Assign the same permissions to it as the parent node
            // http://issues.umbraco.org/issue/U4-2161     
            var permissionsRepo = new PermissionRepository<IContent>(UnitOfWork, _cacheHelper, SqlSyntax);
            var parentPermissions = permissionsRepo.GetPermissionsForEntity(entity.ParentId).ToArray();
            //if there are parent permissions then assign them, otherwise leave null and permissions will become the
            // user's default permissions.
            if (parentPermissions.Any())
            {
                var userPermissions = (
                    from perm in parentPermissions
                    from p in perm.AssignedPermissions
                    select new EntityPermissionSet.UserPermission(perm.UserId, p)).ToList();

                permissionsRepo.ReplaceEntityPermissions(new EntityPermissionSet(entity.Id, userPermissions));
                //flag the entity's permissions changed flag so we can track those changes.
                //Currently only used for the cache refreshers to detect if we should refresh all user permissions cache.
                ((Content)entity).PermissionsChanged = true;
            }

            //Create the Content specific data - cmsContent
            var contentDto = dto.ContentVersionDto.ContentDto;
            contentDto.NodeId = nodeDto.NodeId;
            Database.Insert(contentDto);

            //Create the first version - cmsContentVersion
            //Assumes a new Version guid and Version date (modified date) has been set
            var contentVersionDto = dto.ContentVersionDto;
            contentVersionDto.NodeId = nodeDto.NodeId;
            Database.Insert(contentVersionDto);

            //Create the Document specific data for this version - cmsDocument
            //Assumes a new Version guid has been generated
            dto.NodeId = nodeDto.NodeId;
            Database.Insert(dto);

            //Create the PropertyData for this version - cmsPropertyData
            var propertyFactory = new PropertyFactory(entity.ContentType.CompositionPropertyTypes.ToArray(), entity.Version, entity.Id);
            var propertyDataDtos = propertyFactory.BuildDto(entity.Properties);
            var keyDictionary = new Dictionary<int, int>();

            //Add Properties
            foreach (var propertyDataDto in propertyDataDtos)
            {
                var primaryKey = Convert.ToInt32(Database.Insert(propertyDataDto));
                keyDictionary.Add(propertyDataDto.PropertyTypeId, primaryKey);
            }

            //Update Properties with its newly set Id
            foreach (var property in entity.Properties)
            {
                property.Id = keyDictionary[property.PropertyTypeId];
            }

            //lastly, check if we are a creating a published version , then update the tags table
            if (entity.Published)
            {
                UpdatePropertyTags(entity, _tagRepository);
            }

            // published => update published version infos, else leave it blank
            if (entity.Published)
            {
                dto.DocumentPublishedReadOnlyDto = new DocumentPublishedReadOnlyDto
                {
                    VersionId = dto.VersionId,
                    Newest = true,
                    NodeId = dto.NodeId,
                    Published = true
                };
                ((Content)entity).PublishedVersionGuid = dto.VersionId;
            }

            entity.ResetDirtyProperties();
        }

        protected override void PersistUpdatedItem(IContent entity)
        {
            var publishedState = ((Content)entity).PublishedState;

            //check if we need to make any database changes at all
            if (entity.RequiresSaving(publishedState) == false)
            {
                entity.ResetDirtyProperties();
                return;
            }

            //check if we need to create a new version
            bool shouldCreateNewVersion = entity.ShouldCreateNewVersion(publishedState);
            if (shouldCreateNewVersion)
            {
                //Updates Modified date and Version Guid
                ((Content)entity).UpdatingEntity();
            }
            else
            {
                entity.UpdateDate = DateTime.Now;
            }

            //Ensure unique name on the same level
            entity.Name = EnsureUniqueNodeName(entity.ParentId, entity.Name, entity.Id);

            //Ensure that strings don't contain characters that are invalid in XML
            entity.SanitizeEntityPropertiesForXmlStorage();

            //Look up parent to get and set the correct Path and update SortOrder if ParentId has changed
            if (entity.IsPropertyDirty("ParentId"))
            {
                var parent = Database.First<NodeDto>("WHERE id = @ParentId", new { ParentId = entity.ParentId });
                entity.Path = string.Concat(parent.Path, ",", entity.Id);
                entity.Level = parent.Level + 1;
                var maxSortOrder =
                    Database.ExecuteScalar<int>(
                        "SELECT coalesce(max(sortOrder),0) FROM umbracoNode WHERE parentid = @ParentId AND nodeObjectType = @NodeObjectType",
                        new { ParentId = entity.ParentId, NodeObjectType = NodeObjectTypeId });
                entity.SortOrder = maxSortOrder + 1;

                //Question: If we move a node, should we update permissions to inherit from the new parent if the parent has permissions assigned?
                // if we do that, then we'd need to propogate permissions all the way downward which might not be ideal for many people.
                // Gonna just leave it as is for now, and not re-propogate permissions.
            }

            var factory = new ContentFactory(NodeObjectTypeId, entity.Id);
            //Look up Content entry to get Primary for updating the DTO
            var contentDto = Database.SingleOrDefault<ContentDto>("WHERE nodeId = @Id", new { Id = entity.Id });
            factory.SetPrimaryKey(contentDto.PrimaryKey);
            var dto = factory.BuildDto(entity);

            //Updates the (base) node data - umbracoNode
            var nodeDto = dto.ContentVersionDto.ContentDto.NodeDto;
            var o = Database.Update(nodeDto);

            //Only update this DTO if the contentType has actually changed
            if (contentDto.ContentTypeId != entity.ContentTypeId)
            {
                //Create the Content specific data - cmsContent
                var newContentDto = dto.ContentVersionDto.ContentDto;
                Database.Update(newContentDto);
            }

            //a flag that we'll use later to create the tags in the tag db table
            var publishedStateChanged = false;

            //If Published state has changed then previous versions should have their publish state reset.
            //If state has been changed to unpublished the previous versions publish state should also be reset.
            //if (((ICanBeDirty)entity).IsPropertyDirty("Published") && (entity.Published || publishedState == PublishedState.Unpublished))
            if (entity.ShouldClearPublishedFlagForPreviousVersions(publishedState, shouldCreateNewVersion))
            {
                var publishedDocs = Database.Fetch<DocumentDto>("WHERE nodeId = @Id AND published = @IsPublished", new { Id = entity.Id, IsPublished = true });
                foreach (var doc in publishedDocs)
                {
                    var docDto = doc;
                    docDto.Published = false;
                    Database.Update(docDto);
                }

                //this is a newly published version so we'll update the tags table too (end of this method)
                publishedStateChanged = true;
            }

            //Look up (newest) entries by id in cmsDocument table to set newest = false
            var documentDtos = Database.Fetch<DocumentDto>("WHERE nodeId = @Id AND newest = @IsNewest", new { Id = entity.Id, IsNewest = true });
            foreach (var documentDto in documentDtos)
            {
                var docDto = documentDto;
                docDto.Newest = false;
                Database.Update(docDto);
            }

            var contentVersionDto = dto.ContentVersionDto;
            if (shouldCreateNewVersion)
            {
                //Create a new version - cmsContentVersion
                //Assumes a new Version guid and Version date (modified date) has been set
                Database.Insert(contentVersionDto);
                //Create the Document specific data for this version - cmsDocument
                //Assumes a new Version guid has been generated
                Database.Insert(dto);
            }
            else
            {
                //In order to update the ContentVersion we need to retrieve its primary key id
                var contentVerDto = Database.SingleOrDefault<ContentVersionDto>("WHERE VersionId = @Version", new { Version = entity.Version });
                contentVersionDto.Id = contentVerDto.Id;

                Database.Update(contentVersionDto);
                Database.Update(dto);
            }

            //Create the PropertyData for this version - cmsPropertyData
            var propertyFactory = new PropertyFactory(entity.ContentType.CompositionPropertyTypes.ToArray(), entity.Version, entity.Id);
            var propertyDataDtos = propertyFactory.BuildDto(entity.Properties);
            var keyDictionary = new Dictionary<int, int>();

            //Add Properties
            foreach (var propertyDataDto in propertyDataDtos)
            {
                if (shouldCreateNewVersion == false && propertyDataDto.Id > 0)
                {
                    Database.Update(propertyDataDto);
                }
                else
                {
                    int primaryKey = Convert.ToInt32(Database.Insert(propertyDataDto));
                    keyDictionary.Add(propertyDataDto.PropertyTypeId, primaryKey);
                }
            }

            //Update Properties with its newly set Id
            if (keyDictionary.Any())
            {
                foreach (var property in entity.Properties)
                {
                    if (keyDictionary.ContainsKey(property.PropertyTypeId) == false) continue;

                    property.Id = keyDictionary[property.PropertyTypeId];
                }
            }

            //lastly, check if we are a newly published version and then update the tags table
            if (publishedStateChanged && entity.Published)
            {
                UpdatePropertyTags(entity, _tagRepository);
            }
            else if (publishedStateChanged && (entity.Trashed || entity.Published == false))
            {
                //it's in the trash or not published remove all entity tags
                ClearEntityTags(entity, _tagRepository);
            }

            // published => update published version infos,
            // else if unpublished then clear published version infos
            if (entity.Published)
            {
                dto.DocumentPublishedReadOnlyDto = new DocumentPublishedReadOnlyDto
                {
                    VersionId = dto.VersionId,
                    Newest = true,
                    NodeId = dto.NodeId,
                    Published = true
                };
                ((Content)entity).PublishedVersionGuid = dto.VersionId;
            }
            else if (publishedStateChanged)
            {
                dto.DocumentPublishedReadOnlyDto = new DocumentPublishedReadOnlyDto
                {
                    VersionId = default(Guid),
                    Newest = false,
                    NodeId = dto.NodeId,
                    Published = false
                };
                ((Content)entity).PublishedVersionGuid = default(Guid);
            }

            entity.ResetDirtyProperties();
        }


        #endregion

        #region Implementation of IContentRepository

        public IEnumerable<IContent> GetByPublishedVersion(string filter)
        {
            // we WANT to return contents in top-down order, ie parents should come before children
            // ideal would be pure xml "document order" which can be achieved with:
            // ORDER BY substring(path, 1, len(path) - charindex(',', reverse(path))), sortOrder
            // but that's probably an overkill - sorting by level,sortOrder should be enough

            var sqlClause = GetBaseQuery(false);
            var translator = new SqlTranslator<IContent>(sqlClause, query);
            var sql = translator.Translate()
                                .Where<DocumentDto>(x => x.Published)
                                .OrderBy<NodeDto>(x => x.Level, SqlSyntax)
                                .OrderBy<NodeDto>(x => x.SortOrder, SqlSyntax);

            //NOTE: This doesn't allow properties to be part of the query
            var dtos = Database.Fetch<DocumentDto, ContentVersionDto, ContentDto, NodeDto, DocumentPublishedReadOnlyDto>(sql);

            foreach (var dto in dtos)
            {
                //Check in the cache first. If it exists there AND it is published
                // then we can use that entity. Otherwise if it is not published (which can be the case
                // because we only store the 'latest' entries in the cache which might not be the published
                // version)
                var fromCache = RuntimeCache.GetCacheItem<IContent>(GetCacheIdKey<IContent>(dto.NodeId));
                //var fromCache = TryGetFromCache(dto.NodeId);
                if (fromCache != null && fromCache.Published)
                {
                    yield return fromCache;
                }
                else
                {
                    yield return CreateContentFromDto(dto, dto.VersionId, sql);
                }
            }
        }

        public int CountPublished()
        {
            var sql = GetBaseQuery(true).Where<NodeDto>(x => x.Trashed == false)
                .Where<DocumentDto>(x => x.Published == true);
            return Database.ExecuteScalar<int>(sql);
        }

        public void ReplaceContentPermissions(EntityPermissionSet permissionSet)
        {
            var repo = new PermissionRepository<IContent>(UnitOfWork, _cacheHelper, SqlSyntax);
            repo.ReplaceEntityPermissions(permissionSet);
        }

        public void ClearPublished(IContent content)
        {
            // race cond!
            var documentDtos = Database.Fetch<DocumentDto>("WHERE nodeId=@id AND published=@published", new { id = content.Id, published = true });
            foreach (var documentDto in documentDtos)
            {
                documentDto.Published = false;
                Database.Update(documentDto);
            }
        }

        /// <summary>
        /// Assigns a single permission to the current content item for the specified user ids
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="permission"></param>
        /// <param name="userIds"></param>        
        public void AssignEntityPermission(IContent entity, char permission, IEnumerable<int> userIds)
        {
            var repo = new PermissionRepository<IContent>(UnitOfWork, _cacheHelper, SqlSyntax);
            repo.AssignEntityPermission(entity, permission, userIds);
        }

        public IEnumerable<EntityPermission> GetPermissionsForEntity(int entityId)
        {
            var repo = new PermissionRepository<IContent>(UnitOfWork, _cacheHelper, SqlSyntax);
            return repo.GetPermissionsForEntity(entityId);
        }

        /// <summary>
        /// Adds/updates content/published xml
        /// </summary>
        /// <param name="content"></param>
        /// <param name="xml"></param>
        public void AddOrUpdateContentXml(IContent content, Func<IContent, XElement> xml)
        {
            _contentXmlRepository.AddOrUpdate(new ContentXmlEntity<IContent>(content, xml));
        }

        /// <summary>
        /// Used to remove the content xml for a content item
        /// </summary>
        /// <param name="content"></param>
        public void DeleteContentXml(IContent content)
        {
            _contentXmlRepository.Delete(new ContentXmlEntity<IContent>(content));
        }

        /// <summary>
        /// Adds/updates preview xml
        /// </summary>
        /// <param name="content"></param>
        /// <param name="xml"></param>
        public void AddOrUpdatePreviewXml(IContent content, Func<IContent, XElement> xml)
        {
            _contentPreviewRepository.AddOrUpdate(new ContentPreviewEntity<IContent>(content, xml));
        }

        /// <summary>
        /// Gets paged content results
        /// </summary>
        /// <param name="query">Query to excute</param>
        /// <param name="pageIndex">Page number</param>
        /// <param name="pageSize">Page size</param>
        /// <param name="totalRecords">Total records query would return without paging</param>
        /// <param name="orderBy">Field to order by</param>
        /// <param name="orderDirection">Direction to order by</param>
        /// <param name="orderBySystemField">Flag to indicate when ordering by system field</param>
        /// <param name="filter">Search text filter</param>
        /// <returns>An Enumerable list of <see cref="IContent"/> objects</returns>
        public IEnumerable<IContent> GetPagedResultsByQuery(IQuery<IContent> query, long pageIndex, int pageSize, out long totalRecords,
            string orderBy, Direction orderDirection, bool orderBySystemField, string filter = "")
        {

            //NOTE: This uses the GetBaseQuery method but that does not take into account the required 'newest' field which is 
            // what we always require for a paged result, so we'll ensure it's included in the filter

            var args = new List<object>();
            var sbWhere = new StringBuilder("AND (cmsDocument.newest = 1)");

            if (filter.IsNullOrWhiteSpace() == false)
            {
                sbWhere.Append(" AND (cmsDocument." + SqlSyntax.GetQuotedColumnName("text") + " LIKE @" + args.Count + ")");
                args.Add("%" + filter + "%");
            }

            Func<Tuple<string, object[]>> filterCallback = () => new Tuple<string, object[]>(sbWhere.ToString(), args.ToArray());

            return GetPagedResultsByQuery<DocumentDto, Content>(query, pageIndex, pageSize, out totalRecords,
                new Tuple<string, string>("cmsDocument", "nodeId"),
                ProcessQuery, orderBy, orderDirection, orderBySystemField,
                filterCallback);

        }

        #endregion

        #region IRecycleBinRepository members

        protected override int RecycleBinId
        {
            get { return Constants.System.RecycleBinContent; }
        }

        #endregion

        protected override Guid NodeObjectTypeId
        {
            get { return new Guid(Constants.ObjectTypes.Document); }
        }

        /// <summary>
        /// Private method to create a content object from a DocumentDto, which is used by Get and GetByVersion.
        /// </summary>
        /// <param name="dto"></param>
        /// <param name="contentType"></param>
        /// <param name="template"></param>
        /// <param name="propCollection"></param>
        /// <returns></returns>
        private IContent CreateContentFromDto(DocumentDto dto,
            IContentType contentType,
            ITemplate template,
            Models.PropertyCollection propCollection)
        {
            var factory = new ContentFactory(contentType, NodeObjectTypeId, dto.NodeId);
            var content = factory.BuildEntity(dto);

            //Check if template id is set on DocumentDto, and get ITemplate if it is.
            if (dto.TemplateId.HasValue && dto.TemplateId.Value > 0)
            {
                content.Template = template ?? _templateRepository.Get(dto.TemplateId.Value);
            }
            else
            {
                //ensure there isn't one set.
                content.Template = null;
            }

            content.Properties = propCollection;

            //on initial construction we don't want to have dirty properties tracked
            // http://issues.umbraco.org/issue/U4-1946
            ((Entity)content).ResetDirtyProperties(false);
            return content;
        }
        
        private string EnsureUniqueNodeName(int parentId, string nodeName, int id = 0)
        {
            if (EnsureUniqueNaming == false)
                return nodeName;

            var sql = new Sql();
            sql.Select("*")
               .From<NodeDto>()
               .Where<NodeDto>(x => x.NodeObjectType == NodeObjectTypeId && x.ParentId == parentId && x.Text.StartsWith(nodeName));

            int uniqueNumber = 1;
            var currentName = nodeName;

            var dtos = Database.Fetch<NodeDto>(sql);
            if (dtos.Any())
            {
                var results = dtos.OrderBy(x => x.Text, new SimilarNodeNameComparer());
                foreach (var dto in results)
                {
                    if (id != 0 && id == dto.NodeId) continue;

                    if (dto.Text.ToLowerInvariant().Equals(currentName.ToLowerInvariant()))
                    {
                        currentName = nodeName + string.Format(" ({0})", uniqueNumber);
                        uniqueNumber++;
                    }
                }
            }

            return currentName;
        }

        /// <summary>
        /// Dispose disposable properties
        /// </summary>
        /// <remarks>
        /// Ensure the unit of work is disposed
        /// </remarks>
        protected override void DisposeResources()
        {
            _contentTypeRepository.Dispose();
            _templateRepository.Dispose();
            _tagRepository.Dispose();
            _contentPreviewRepository.Dispose();
            _contentXmlRepository.Dispose();
        }

        public IEnumerable<IContent> GetByPublishedVersion(string filter)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IContent> GetPagedResults(string filter, int pageIndex, int pageSize, out long totalRecords, string orderBy, Direction orderDirection, bool orderBySystemField)
        {
            throw new NotImplementedException();
        }

        public bool DeleteMediaFiles(IEnumerable<string> files)
        {
            throw new NotImplementedException();
        }

    }
}