using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMSolutionExplorer
{
    public static class Helper
    {
        public static Entity RetrieveSafe(this IOrganizationService service, string entityName, Guid id, ColumnSet columnSet)
        {
            Entity entity = null;
            try
            {
                entity = service.Retrieve(entityName, id, columnSet);
            }
            catch (Exception)
            {
            }
            return entity;
        }
        public static T GetAttributeValueSafe<T>(this Entity entity, string attribute)
        {
            return entity != null && entity.Contains(attribute) ? entity.GetAttributeValue<T>(attribute) : default(T);
        }
        public static string GetFormattedValueSafe(this Entity entity, string attribute)
        {
            return entity != null && entity.FormattedValues.Keys.Contains(attribute) ? entity.FormattedValues[attribute] : null;
        }
        public static string ToStringNullSafe(this object obj)
        {
            return (obj ?? String.Empty).ToString();
        }
        public static T GetSafe<T>(this Entity entity, string attribute)
        {
            attribute = attribute.ToLower();
            if (entity != null && entity.Contains(attribute))
                return entity.GetAttributeValue<T>(attribute);
            return default(T);
        }
    }
}
