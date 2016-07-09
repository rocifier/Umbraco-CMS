using System.Collections.Generic;
using Umbraco.Core.Models;

namespace Umbraco.Core.Persistence.NoSqlRepositories
{
    interface IPropertiesRepository
    {
        int Count(string filter);
        PropertyCollection Get(string filter);
        PropertyCollection Get(DocumentDefinition doc);
        PropertyCollection GetAll(IEnumerable<DocumentDefinition> docs);
    }
}
