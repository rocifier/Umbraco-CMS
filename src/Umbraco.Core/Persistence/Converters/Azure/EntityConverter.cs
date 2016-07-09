using System;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.Protocol;
using System.Collections.Generic;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence.Factories;
using Umbraco.Core.Models.Rdbms;
using Umbraco.Core.Persistence.Repositories;
using Newtonsoft.Json;

namespace Umbraco.Core.Persistence.Converters.Azure
{
    public class EntityConverter : IEntityToPocoConverter, IPocoToEntityConverter
    {
        /// <summary>
        /// Note this only transfers across public properties which have public getter and setter methods.
        /// </summary>
        /// <param name="content">existing dto object to convert</param>
        /// <returns></returns>
        public DynamicTableEntity PocoToTableEntity(object dto)
        {
            Dictionary<string, EntityProperty> entityProperties = new Dictionary<string, EntityProperty>();
            var properties = dto.GetType().GetProperties();
            string id = "";
            foreach (var property in properties)
            {
                // reserved properties
                if (property.Name == TableConstants.PartitionKey ||
                    property.Name == TableConstants.RowKey ||
                    property.Name == TableConstants.Timestamp ||
                    property.Name == TableConstants.Etag)
                {
                    continue;
                }

                // extract id for using as row key below
                if (property.Name.ToLower() == "id")
                {
                    id = property.GetValue(dto).ToString();
                }

                // enforce public getter and setter
                if (property.GetSetMethod() == null || !property.GetSetMethod().IsPublic || property.GetGetMethod() == null || !property.GetGetMethod().IsPublic)
                {
                    continue;
                }
                EntityProperty newProperty = EntityProperty.CreateEntityPropertyFromObject(property.GetValue(dto));
                entityProperties.Add(property.Name, newProperty);
            }

            DynamicTableEntity dynamicEntity = new DynamicTableEntity(dto.GetType().Name.ToLower(), id, "*", entityProperties);
            dynamicEntity.ETag = "*";
            
            return dynamicEntity;
            
        }


        /// <summary>
        /// Converts a dynamic table entity to a poco. If the poco has a custom class property,
        /// to inject an instances class the entity must contain a matching serialized json 
        /// string property with the same name.
        /// </summary>
        /// <typeparam name="Tdto">Must be a class type with a parameterless constructor</typeparam>
        /// <param name="entity">Entity to convert</param>
        /// <returns>New class type requested with properties copied across where their names match the entity property names precisely</returns>
        public Tdto TableEntityToPoco<Tdto>(DynamicTableEntity entity) where Tdto : class
        {
            IDictionary<string, EntityProperty> entityProperties = entity.Properties;
            Tdto dto = Activator.CreateInstance<Tdto>();
            var properties = typeof(Tdto).GetProperties();
            foreach (var property in properties)
            {
                if (entityProperties.ContainsKey(property.Name))
                {
                    var copyFrom = entityProperties[property.Name];
                    var copyTo = property;

                    if (copyTo.PropertyType == typeof(string) ||
                        copyTo.PropertyType == typeof(double) || copyTo.PropertyType == typeof(double?) ||
                        copyTo.PropertyType == typeof(int) || copyTo.PropertyType == typeof(int?) ||
                        copyTo.PropertyType == typeof(float) || copyTo.PropertyType == typeof(float?) ||
                        copyTo.PropertyType == typeof(DateTime) || copyTo.PropertyType == typeof(DateTime?) ||
                        copyTo.PropertyType == typeof(bool) || copyTo.PropertyType == typeof(bool?) ||
                        copyTo.PropertyType == typeof(byte) || copyTo.PropertyType == typeof(byte?) ||
                        copyTo.PropertyType == typeof(char) || copyTo.PropertyType == typeof(char?) ||
                        copyTo.PropertyType == typeof(decimal) || copyTo.PropertyType == typeof(decimal?) ||
                        copyTo.PropertyType == typeof(Enum) ||
                        copyTo.PropertyType == typeof(long) || copyTo.PropertyType == typeof(long?) ||
                        copyTo.PropertyType == typeof(sbyte) || copyTo.PropertyType == typeof(sbyte?) ||
                        copyTo.PropertyType == typeof(short) || copyTo.PropertyType == typeof(short?) ||
                        copyTo.PropertyType == typeof(uint) || copyTo.PropertyType == typeof(uint?) ||
                        copyTo.PropertyType == typeof(ulong) || copyTo.PropertyType == typeof(ulong?) ||
                        copyTo.PropertyType == typeof(ushort) || copyTo.PropertyType == typeof(ushort?))
                    {
                        // Copy values directly over
                        copyTo.SetValue(dto, copyFrom.PropertyAsObject);
                    } 
                    else
                    {
                        if (!(copyFrom.PropertyType == EdmType.String))
                        {
                            throw new Exception(String.Format("Entity property {0} should be a json representation of type {1}", property.Name, property.PropertyType.Name));
                        }

                        // Deserialize json to class object
                        var obj = JsonConvert.DeserializeObject(copyFrom.StringValue);
                        copyTo.SetValue(dto, obj);
                    }
                }
            }

            return dto;
        }
        
        public IContent TableEntityToContent(DynamicTableEntity entity, IContentType contentType)
        {
            var nodeId = int.Parse(entity.RowKey);
            var factory = new ContentFactory(contentType, new Guid(Constants.ObjectTypes.Document), nodeId);

            // Construct dto
            DocumentDto dto = TableEntityToPoco<DocumentDto>(entity);

            var content = factory.BuildEntity(dto);
            return content;
        }
        
    }
}
