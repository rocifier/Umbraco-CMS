using System;
using System.Collections.Generic;
using Umbraco.Core.Models;

namespace Umbraco.Core.Persistence.NoSqlRepositories
{
    class PropertiesRepository : IPropertiesRepository
    {
        public int Count(string filter)
        {
            throw new NotImplementedException();
        }
        
        public PropertyCollection Get(string filter)
        {
            throw new NotImplementedException();
        }

        public PropertyCollection Get(DocumentDefinition doc)
        {
            throw new NotImplementedException();
        }

        public PropertyCollection GetAll(IEnumerable<DocumentDefinition> docs)
        {
            throw new NotImplementedException();
        }
        
    }
}
