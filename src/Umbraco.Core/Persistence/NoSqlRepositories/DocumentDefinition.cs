using System;
using Umbraco.Core.Models;

namespace Umbraco.Core.Persistence.NoSqlRepositories
{
    /// <summary>
    /// Extracted from VersionableRepositoryBase.cs
    /// </summary>
    public class DocumentDefinition
    {
        public DocumentDefinition(int id, Guid version, DateTime versionDate, DateTime createDate, IContentTypeComposition composition)
        {
            Id = id;
            Version = version;
            VersionDate = versionDate;
            CreateDate = createDate;
            Composition = composition;
        }

        public int Id { get; set; }
        public Guid Version { get; set; }
        public DateTime VersionDate { get; set; }
        public DateTime CreateDate { get; set; }
        public IContentTypeComposition Composition { get; set; }
    }

}
