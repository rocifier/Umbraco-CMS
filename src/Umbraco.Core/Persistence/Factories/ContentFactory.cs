using System;
using System.Globalization;
using Umbraco.Core.Models;
using Umbraco.Core.Models.NoSql;
using Umbraco.Core.Models.Rdbms;

namespace Umbraco.Core.Persistence.Factories
{
    internal class ContentFactory
    {
        private readonly IContentType _contentType;
        private readonly Guid _nodeObjectTypeId;
        private readonly int _id;
        private int _primaryKey;

        public ContentFactory(IContentType contentType, Guid nodeObjectTypeId, int id)
        {
            _contentType = contentType;
            _nodeObjectTypeId = nodeObjectTypeId;
            _id = id;
        }

        public ContentFactory(Guid nodeObjectTypeId, int id)
        {
            _nodeObjectTypeId = nodeObjectTypeId;
            _id = id;
        }

        #region Implementation of IEntityFactory<IContent,DocumentDto>

        public IContent BuildEntity(DocumentDto dto)
        {
            var content = new Content(dto.Text, dto.ContentVersionDto.ContentDto.NodeDto.ParentId, _contentType)
            {
                Id = _id,
                Key = dto.ContentVersionDto.ContentDto.NodeDto.UniqueId,
                Name = dto.Text,
                NodeName = dto.ContentVersionDto.ContentDto.NodeDto.Text,
                Path = dto.ContentVersionDto.ContentDto.NodeDto.Path,
                CreatorId = dto.ContentVersionDto.ContentDto.NodeDto.UserId.Value,
                WriterId = dto.WriterUserId,
                Level = dto.ContentVersionDto.ContentDto.NodeDto.Level,
                ParentId = dto.ContentVersionDto.ContentDto.NodeDto.ParentId,
                SortOrder = dto.ContentVersionDto.ContentDto.NodeDto.SortOrder,
                Trashed = dto.ContentVersionDto.ContentDto.NodeDto.Trashed,
                Published = dto.Published,
                CreateDate = dto.ContentVersionDto.ContentDto.NodeDto.CreateDate,
                UpdateDate = dto.ContentVersionDto.VersionDate,
                ExpireDate = dto.ExpiresDate.HasValue ? dto.ExpiresDate.Value : (DateTime?)null,
                ReleaseDate = dto.ReleaseDate.HasValue ? dto.ReleaseDate.Value : (DateTime?)null,
                Version = dto.ContentVersionDto.VersionId,
                PublishedState = dto.Published ? PublishedState.Published : PublishedState.Unpublished,
                PublishedVersionGuid = dto.DocumentPublishedReadOnlyDto == null ? default(Guid) : dto.DocumentPublishedReadOnlyDto.VersionId
            };
            //on initial construction we don't want to have dirty properties tracked
            // http://issues.umbraco.org/issue/U4-1946
            content.ResetDirtyProperties(false);
            return content;
        }

        public DocumentDto BuildDto(IContent entity)
        {
            //NOTE Currently doesn't add Alias (legacy that eventually will go away)
            var documentDto = new DocumentDto
                                  {
                                      Newest = true,
                                      NodeId = entity.Id,
                                      Published = entity.Published,
                                      Text = entity.Name,
                                      UpdateDate = entity.UpdateDate,
                                      WriterUserId = entity.WriterId,
                                      VersionId = entity.Version,
                                      ExpiresDate = null,
                                      ReleaseDate = null,
                                      ContentVersionDto = BuildContentVersionDto(entity)
                                  };

            if (entity.Template != null && entity.Template.Id > 0)
                documentDto.TemplateId = entity.Template.Id;

            if (entity.ExpireDate.HasValue)
                documentDto.ExpiresDate = entity.ExpireDate.Value;

            if (entity.ReleaseDate.HasValue)
                documentDto.ReleaseDate = entity.ReleaseDate.Value;

            return documentDto;
        }

        #endregion

        public void SetPrimaryKey(int primaryKey)
        {
            _primaryKey = primaryKey;
        }

        private ContentVersionDto BuildContentVersionDto(IContent entity)
        {
            //TODO: Change this once the Language property is public on IContent
            var content = entity as Content;
            var lang = content == null ? string.Empty : content.Language;

            var contentVersionDto = new ContentVersionDto
            {
                NodeId = entity.Id,
                VersionDate = entity.UpdateDate,
                VersionId = entity.Version,
                ContentDto = BuildContentDto(entity)
            };
            return contentVersionDto;
        }

        private ContentDto BuildContentDto(IContent entity)
        {
            var contentDto = new ContentDto
            {
                NodeId = entity.Id,
                ContentTypeId = entity.ContentTypeId,
                NodeDto = BuildNodeDto(entity)
            };

            if (_primaryKey > 0)
            {
                contentDto.PrimaryKey = _primaryKey;
            }

            return contentDto;
        }

        private NodeDto BuildNodeDto(IContent entity)
        {
            //TODO: Change this once the Language property is public on IContent            
            var nodeName = entity.Name;

            var nodeDto = new NodeDto
            {
                CreateDate = entity.CreateDate,
                NodeId = entity.Id,
                Level = short.Parse(entity.Level.ToString(CultureInfo.InvariantCulture)),
                NodeObjectType = _nodeObjectTypeId,
                ParentId = entity.ParentId,
                Path = entity.Path,
                SortOrder = entity.SortOrder,
                Text = nodeName,
                Trashed = entity.Trashed,
                UniqueId = entity.Key,
                UserId = entity.CreatorId
            };

            return nodeDto;
        }

        public IContent BuildEntity(NoSqlDocumentDto dto)
        {
            var content = new Content(dto.Name, dto.ParentId, _contentType)
            {
                Id = _id,
                Key = new Guid(dto.RowKey),
                Name = dto.Name,
                NodeName = dto.Name,
                Path = dto.Path,
                CreatorId = dto.CreatorUserId,
                WriterId = dto.WriterUserId,
                Level = dto.Level,
                ParentId = dto.ParentId,
                SortOrder = dto.SortOrder,
                Trashed = dto.Trashed,
                Published = dto.Published,
                CreateDate = dto.CreateDate,
                UpdateDate = dto.UpdateDate,
                ExpireDate = dto.ExpiresDate,
                ReleaseDate = dto.ReleaseDate,
                Version = dto.VersionId,
                PublishedState = dto.Published ? PublishedState.Published : PublishedState.Unpublished,
                PublishedVersionGuid = dto.PublishedReadOnlyVersion == null ? default(Guid) : dto.PublishedReadOnlyVersion
            };
            //on initial construction we don't want to have dirty properties tracked
            // http://issues.umbraco.org/issue/U4-1946
            content.ResetDirtyProperties(false);
            return content;
        }

        public NoSqlDocumentDto BuildNoSqlDocumentDto(IContent entity)
        {
            var documentDto = new NoSqlDocumentDto
            {
                ETag = "*",
                Newest = true,
                PartitionKey = entity.Id.ToString(),
                Published = entity.Published,
                Name = entity.Name,
                UpdateDate = entity.UpdateDate,
                WriterUserId = entity.WriterId,
                VersionId = entity.Version,
                ExpiresDate = entity.ExpireDate == null ? default(DateTime) : entity.ExpireDate.Value,
                ReleaseDate = entity.ReleaseDate == null ? default(DateTime) : entity.ReleaseDate.Value,
                ParentId = entity.ParentId,
                Path = entity.Path,
                CreateDate = entity.CreateDate,
                ContentTypeId = entity.ContentTypeId,
                CreatorUserId = entity.CreatorId,
                Level = (short)entity.Level,
                NodeObjectType = _nodeObjectTypeId,
                PublishedReadOnlyVersion = entity.PublishedVersionGuid,
                RowKey = entity.Key.ToString(),
                SortOrder = entity.SortOrder,
                Trashed = entity.Trashed,
                Template_Alias = "",
                Template_Design = ""
            };
            
            return documentDto;
        }

    }
}