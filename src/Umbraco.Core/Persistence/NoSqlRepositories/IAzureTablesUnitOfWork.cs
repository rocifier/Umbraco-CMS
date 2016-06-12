using Microsoft.WindowsAzure.Storage.Table;
using Umbraco.Core.Persistence.UnitOfWork;

namespace Umbraco.Core.Persistence.NoSqlRepositories
{
    interface IAzureTablesUnitOfWork : IUnitOfWork
    {
        CloudTableClient Database { get; set; }
    }
}
