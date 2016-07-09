using Microsoft.WindowsAzure.Storage.Table;

namespace Umbraco.Core.Persistence.Converters.Azure
{
    public interface IDtoEntityConverter
    {
        DynamicTableEntity DtoToTableEntity(object dto, string partitionKey, string rowKeyProperty);
        Tdto TableEntityToDto<Tdto>(DynamicTableEntity entity) where Tdto : class;
    }
}
