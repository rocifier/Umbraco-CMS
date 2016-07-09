using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace Umbraco.Core.Models.NoSql
{
    /// <summary>
    /// PartitionKey = NodeId(int)
    /// RowKey = UniqueId(Guid)
    /// </summary>
    internal class NoSqlDocumentDto : TableEntity
    {
        public const int NodeIdSeed = 1050;

        // NodeDto
        public int ParentId { get; set; }
        public bool Trashed { get; set; }
        public short Level { get; set; }
        public string Path { get; set; }
        public int SortOrder { get; set; }
        public Guid NodeObjectType { get; set; } // A code-first constant from Umbraco.Core.Constants.ObjectTypes
        public DateTime CreateDate { get; set; }
        public int CreatorUserId { get; set; }

        // DocumentDto
        public bool Published { get; set; }
        public int WriterUserId { get; set; }
        public string Name { get; set; }
        public DateTime ReleaseDate { get; set; }
        public DateTime ExpiresDate { get; set; }
        public DateTime UpdateDate { get; set; }
        public bool Newest { get; set; }
        public string Template_Alias { get; set; } // Flattened TemplateDto
        public string Template_Design { get; set; }
        
        // ContentDto
        public int ContentTypeId { get; set; }

        // ContentVersionDto
        public Guid VersionId { get; set; }
        public DateTime VersionDate { get; set; }
        public Guid PublishedReadOnlyVersion { get; set; }
    }
}
