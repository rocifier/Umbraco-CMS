using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage.Table;
using ObjectFlattenerRecomposer;

namespace Umbraco.Core.Persistence.Converters.Azure
{
    internal class FlatEntityConverter : IDtoEntityConverter
    {
        public DynamicTableEntity DtoToTableEntity(object dto, string partitionKey, string rowKeyProperty)
        {
            //Flatten object and convert it to EntityProperty Dictionary
            Dictionary<string, EntityProperty> flattenedProperties = EntityPropertyConverter.Flatten(dto);

            // Create a DynamicTableEntity and set its PK and RK
            DynamicTableEntity dynamicTableEntity = new DynamicTableEntity(partitionKey, flattenedProperties[rowKeyProperty].StringValue);
            dynamicTableEntity.Properties = flattenedProperties;
            return dynamicTableEntity;
        }
        
        public Tdto TableEntityToDto<Tdto>(DynamicTableEntity entity) where Tdto : class
        {
            Tdto order = EntityPropertyConverter.ConvertBack<Tdto>(entity.Properties);
            return order;
        }
    }
}
