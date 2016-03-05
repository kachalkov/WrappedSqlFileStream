using System;
using System.Collections.Generic;
using System.Reflection;
using NHibernate;
using NHibernate.Persister.Entity;

namespace WrappedSqlFileStream.Mapping.NHibernate
{
    //http://stackoverflow.com/questions/1800930/getting-class-field-names-and-table-column-names-from-nhibernate-metadata

    /// <summary>
    /// NHibernate helper class
    /// </summary>
    /// <remarks>
    /// Assumes you are using NHibernate version 3.1.0.4000 or greater (Not tested on previous versions)
    /// </remarks>
    public static class NHibernateHelper
    {
        /// <summary>
        /// Returns the table name of an entity that has been mapped with NHibernate
        /// </summary>
        /// <param name="sessionFactory">The SessionFactory that contains the Nhibernate mappings</param>
        /// <returns></returns>
        public static string GetTableName<T>(this ISessionFactory sessionFactory)
        {
            var type = typeof(T);

            // Get the entity's NHibernate metadata
            var metaData = sessionFactory.GetClassMetadata(type.ToString());

            // Gets the entity's persister
            var persister = (AbstractEntityPersister)metaData;

            return persister.RootTableName;
        }

        public static string GetIdentifierName<T>(this ISessionFactory sessionFactory)
        {
            // Get the objects type
            Type type = typeof(T);

            // Get the entity's NHibernate metadata
            var metaData = sessionFactory.GetClassMetadata(type.ToString());

            // Get the entity's identifier
            return metaData.IdentifierPropertyName;
        }

        /// <summary>
        /// Returns a dictionary of property and database column/field name given an
        /// NHibernate mapped entity
        /// </summary>
        /// <remarks>
        /// This method uses reflection to obtain an NHibernate internal private dictionary.
        /// </remarks>
        /// <param name="sessionFactory">The SessionFactory that contains the Nhibernate mappings</param>
        /// <returns>Entity Property/Database column dictionary</returns>
        public static Dictionary<string, string> GetPropertyMappings<T>(this ISessionFactory sessionFactory)
        {
            // Get the objects type
            Type type = typeof(T);

            // Get the entity's NHibernate metadata
            var metaData = sessionFactory.GetClassMetadata(type.ToString());

            // Gets the entity's persister
            var persister = (AbstractEntityPersister)metaData;

            // Creating our own Dictionary<Entity property name, Database column/filed name>()
            var d = new Dictionary<string, string>();

            // Get the entity's identifier
            string entityIdentifier = metaData.IdentifierPropertyName;
            string databaseIdentifier = persister.KeyColumnNames[0];

            if (entityIdentifier != null)
            {
                // Get the database identifier
                // Note: We are only getting the first key column.
                // Adjust this code to your needs if you are using composite keys!
                // Adding the identifier as the first entry
                d.Add(entityIdentifier, databaseIdentifier);
            }

            // Using reflection to get a private field on the AbstractEntityPersister class
            var fieldInfo = typeof(AbstractEntityPersister)
                .GetField("subclassPropertyColumnNames", BindingFlags.NonPublic | BindingFlags.Instance);

            // This internal NHibernate dictionary contains the entity property name as a key and
            // database column/field name as the value
            var pairs = (Dictionary<string, string[]>)fieldInfo.GetValue(persister);

            foreach (var pair in pairs)
            {
                if (pair.Value.Length > 0)
                {
                    // The database identifier typically appears more than once in the NHibernate dictionary
                    // so we are just filtering it out since we have already added it to our own dictionary
                    if (pair.Value[0] == databaseIdentifier)
                        break;

                    d.Add(pair.Key, "[" + pair.Value[0] + "]");
                }
            }

            return d;
        }
    }
}
