using System;
using System.Collections.Generic;
using Umbraco.Core.Events;
using Umbraco.Core.Models;

namespace Umbraco.Core.Persistence.NoSqlRepositories
{
    interface IContentTypeRepository : IReadRepository<int, IContentType>
    {

        /// <summary>
        /// Gets all property type aliases.
        /// </summary>
        /// <returns></returns>
        IEnumerable<string> GetAllPropertyTypeAliases();

        IEnumerable<MoveEventInfo<IContentType>> Move(IContentType toMove, EntityContainer container);

        /// <summary>
        /// Gets all content type aliases
        /// </summary>
        /// <param name="objectTypes">
        /// If this list is empty, it will return all content type aliases for media, members and content, otherwise
        /// it will only return content type aliases for the object types specified
        /// </param>
        /// <returns></returns>
        IEnumerable<string> GetAllContentTypeAliases(params Guid[] objectTypes);

        /// <summary>
        /// Derives a unique alias from an existing alias.
        /// </summary>
        /// <param name="alias">The original alias.</param>
        /// <returns>The original alias with a number appended to it, so that it is unique.</returns>
        /// /// <remarks>Unique accross all content, media and member types.</remarks>
        string GetUniqueAlias(string alias);
    }
}
