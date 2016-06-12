using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using Umbraco.Core.Models.EntityBase;
using Umbraco.Core.Persistence.UnitOfWork;

namespace Umbraco.Core.Persistence.NoSqlRepositories
{
    class AzureTablesUnitOfWork : IAzureTablesUnitOfWork
    {
        public CloudTableClient Database
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public object Key
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public void Commit()
        {
            throw new NotImplementedException();
        }

        public void RegisterAdded(IEntity entity, IUnitOfWorkRepository repository)
        {
            throw new NotImplementedException();
        }

        public void RegisterChanged(IEntity entity, IUnitOfWorkRepository repository)
        {
            throw new NotImplementedException();
        }

        public void RegisterRemoved(IEntity entity, IUnitOfWorkRepository repository)
        {
            throw new NotImplementedException();
        }
    }
}
