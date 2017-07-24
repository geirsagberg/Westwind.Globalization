#region License

/*
 **************************************************************
 *  Author: Rick Strahl 
 *          � West Wind Technologies, 2009-2015
 *          http://www.west-wind.com/
 * 
 *
 * Permission is hereby granted, free of charge, to any person
 * obtaining a copy of this software and associated documentation
 * files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use,
 * copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following
 * conditions:
 * 
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 **************************************************************  
*/

#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Westwind.Globalization.Core.DbResourceDataManager.DbResourceDataManagers;
using Westwind.Globalization.Core.DbResourceSupportClasses;
using Westwind.Utilities;
using Westwind.Utilities.Data;

namespace Westwind.Globalization.Core.DbResourceDataManager
{
    public abstract class DbResourceDataManager : IDbResourceDataManager
    {
        protected DbResourceDataManager(DbResourceConfiguration configuration)
        {
            // assign default configuration from configuration file
            Configuration = configuration;
        }

        /// <summary>
        ///     Code used to create a database (if required) for the
        ///     given data provider.
        /// </summary>
        protected virtual string TableCreationSql { get; set; }

        /// <summary>
        ///     Internally used Transaction object
        /// </summary>
        protected virtual DbTransaction Transaction { get; set; }

        /// <inheritdoc />
        public DbResourceConfiguration Configuration { get; set; }

        /// <inheritdoc />
        public string ErrorMessage { get; set; }

        /// <inheritdoc />
        public virtual DataAccessBase GetDb(string connectionString = null)
        {
            if (connectionString == null)
                connectionString = Configuration.ConnectionString;

            return new SqlDataAccess(connectionString);
        }

        /// <inheritdoc />
        public virtual IDictionary GetResourceSet(string cultureName, string resourceSet)
        {
            if (cultureName == null)
                cultureName = string.Empty;
            if (resourceSet == null)
                resourceSet = string.Empty;

            var resourceFilter = " ResourceSet=@ResourceSet";

            var resources = new Dictionary<string, object>();

            using (var data = GetDb())
            {
                DbDataReader reader;

                if (string.IsNullOrEmpty(cultureName))
                    reader = data.ExecuteReader(
                        "select ResourceId,Value,Type,BinFile,TextFile,FileName,ValueType from " +
                        Configuration.GetResourceTableNameWithSchema() + " where " + resourceFilter +
                        " and (LocaleId is null OR LocaleId = '') order by ResourceId",
                        data.CreateParameter("@ResourceSet", resourceSet));
                else
                    reader = data.ExecuteReader(
                        "select ResourceId,Value,Type,BinFile,TextFile,FileName,ValueType from " +
                        Configuration.GetResourceTableNameWithSchema() + " where " + resourceFilter +
                        " and LocaleId=@LocaleId order by ResourceId",
                        data.CreateParameter("@ResourceSet", resourceSet),
                        data.CreateParameter("@LocaleId", cultureName));

                if (reader == null)
                {
                    SetError(data.ErrorMessage);
                    return resources;
                }

                try
                {
                    while (reader.Read())
                    {
                        object resourceValue = reader["Value"] as string;
                        var resourceType = reader["Type"] as string;
                        var valueType = 0;

                        valueType = Convert.ToInt32(reader["ValueType"]);

                        if (!string.IsNullOrWhiteSpace(resourceType))
                        {
                            try
                            {
                                // FileResource is a special type that is raw file data stored
                                // in the BinFile or TextFile data. Value contains
                                // filename and type data which is used to create: String, Bitmap or Byte[]
                                if (resourceType == "FileResource")
                                    resourceValue = LoadFileResource(reader);
                                else
                                {
                                    resourceValue = DeserializeValue(resourceValue as string, resourceType);
                                }
                            }
                            catch
                            {
                                // ignore this error
                                resourceValue = null;
                            }
                        }
                        else
                        {
                            if (resourceValue == null)
                                resourceValue = string.Empty;
                        }
                        var key = reader["ResourceId"].ToString();


                        OnResourceSetValueConvert(ref resourceValue, key, valueType);

                        if (!resources.ContainsKey(key))
                            resources.Add(key, resourceValue);
                    }
                }
                catch (Exception ex)
                {
                    SetError(ex.GetBaseException().Message);
                    return resources;
                }
                finally
                {
                    // close reader and connection
                    reader.Close();
                }
            }

            return resources;
        }


        /// <inheritdoc />
        public virtual Dictionary<string, object> GetResourceSetNormalizedForLocaleId(string cultureName,
            string resourceSet)
        {
            if (cultureName == null)
                cultureName = string.Empty;

            var resDictionary = new Dictionary<string, object>();

            using (var data = GetDb())
            {
                Trace.WriteLine("GetResourceSetNormalizedForId: " + cultureName + " - " + resourceSet + "\r\n" +
                    "\t" + data.ConnectionString);


                DbDataReader reader = null;

                var sql =
                    @"select resourceId, LocaleId, Value, Type, BinFile, TextFile, FileName
    from " + Configuration.GetResourceTableNameWithSchema() + @"
	where ResourceSet=@ResourceSet and (LocaleId = '' {0} )
    order by ResourceId, LocaleId DESC";


                // use like parameter or '' if culture is empty/invariant
                var localeFilter = string.Empty;

                var parameters = new List<DbParameter>();
                parameters.Add(data.CreateParameter("@ResourceSet", resourceSet));

                if (!string.IsNullOrEmpty(cultureName))
                {
                    localeFilter += " OR LocaleId = @LocaleId";
                    parameters.Add(data.CreateParameter("@LocaleId", cultureName));

                    // *** grab shorter version
                    if (cultureName.Contains("-"))
                    {
                        localeFilter += " OR LocaleId = @LocaleId1";
                        parameters.Add(data.CreateParameter("@LocaleId1", cultureName.Split('-')[0]));
                    }
                }

                sql = string.Format(sql, localeFilter);

                reader = data.ExecuteReader(sql, parameters.ToArray());

                if (reader == null)
                {
                    SetError(data.ErrorMessage);
                    return resDictionary;
                }

                try
                {
                    var lastResourceId = "xxxyyy";

                    while (reader.Read())
                    {
                        // only pick up the first ID returned - the most specific locale
                        var resourceId = reader["ResourceId"].ToString();
                        if (resourceId == lastResourceId)
                            continue;
                        lastResourceId = resourceId;

                        // Read the value into this                        
                        object resourceValue = reader["Value"] as string;
                        var resourceType = reader["Type"] as string;

                        if (!string.IsNullOrWhiteSpace(resourceType))
                        {
                            // FileResource is a special type that is raw file data stored
                            // in the BinFile or TextFile data. Value contains
                            // filename and type data which is used to create: String, Bitmap or Byte[]
                            if (resourceType == "FileResource")
                                resourceValue = LoadFileResource(reader);
                            else
                                DeserializeValue(resourceValue as string, resourceType);
                        }
                        else
                        {
                            if (resourceValue == null)
                                resourceValue = string.Empty;
                        }

                        resDictionary.Add(resourceId, resourceValue);
                    }
                }
                catch
                {
                }
                finally
                {
                    // close reader and connection
                    reader.Close();
                    data.CloseConnection();
                }
            }

            return resDictionary;
        }

        /// <inheritdoc />
        public virtual List<ResourceItem> GetAllResources(bool localResources = false,
            bool applyValueConverters = false, string resourceSet = null)
        {
            IEnumerable<ResourceItem> items;
            using (var data = GetDb())
            {
                var resourceSetFilter = "";
                if (!string.IsNullOrEmpty(resourceSet))
                    resourceSetFilter = " AND resourceset = @ResourceSet2 ";

                var sql =
                    "select ResourceId,Value,LocaleId,ResourceSet,Type,TextFile,BinFile,FileName,Comment,ValueType,Updated from " +
                    Configuration.GetResourceTableNameWithSchema() +
                    " where ResourceSet " +
                    (!localResources ? "not" : string.Empty) + " like @ResourceSet " +
                    resourceSetFilter +
                    "ORDER BY ResourceSet,LocaleId, ResourceId";


                var parms = new List<IDbDataParameter>();
                parms.Add(data.CreateParameter("@ResourceSet", "%.%"));

                if (!string.IsNullOrEmpty(resourceSetFilter))
                    parms.Add(data.CreateParameter("@ResourceSet2", resourceSet));

                items = data.Query<ResourceItem>(sql, parms.ToArray());

                if (items == null)
                {
                    ErrorMessage = data.ErrorMessage;
                    return null;
                }

                var itemList = items.ToList();

                if (applyValueConverters && Configuration.ResourceSetValueConverters.Count > 0)
                {
                    foreach (var resourceItem in itemList)
                    {
                        foreach (var convert in Configuration.ResourceSetValueConverters)
                        {
                            if (resourceItem.ValueType == convert.ValueType)
                                resourceItem.Value = convert.Convert(resourceItem.Value, resourceItem.ResourceId);
                        }
                    }
                }

                return itemList;
            }
        }


        /// <inheritdoc />
        public virtual List<ResourceIdItem> GetAllResourceIds(string resourceSet)
        {
            using (var data = GetDb())
            {
                var sql = string.Format(
//                    @"select ResourceId, CAST(MAX(len(Value)) as bit)  as HasValue
//	  	from {0}
//        where ResourceSet=@ResourceSet
//		group by ResourceId", Configuration.GetResourceTableNameWithSchema);
                    @"select ResourceId,CAST( MAX( 
	  case  
		WHEN len( CAST(Value as nvarchar(10))) > 0 THEN 1
		ELSE 0
	  end ) as Bit) as HasValue
	  	from {0}
        where ResourceSet=@ResourceSet 
	    group by ResourceId", Configuration.GetResourceTableNameWithSchema());

                var items = data.Query<ResourceIdItem>(sql,
                    data.CreateParameter("@ResourceSet", resourceSet));
                if (items == null)
                {
                    SetError(data.ErrorMessage);
                    return null;
                }

                return items.ToList();
            }
        }

        /// <inheritdoc />
        public virtual List<ResourceIdListItem> GetAllResourceIdListItems(string resourceSet)
        {
            var resourceIds = GetAllResourceIds(resourceSet);
            if (resourceIds == null)
                return null;

            var listItems = resourceIds.Select(id => new ResourceIdListItem
            {
                ResourceId = id.ResourceId,
                HasValue = id.HasValue,
                Value = id.Value as string
            }).ToList();

            var lastId = "xx";
            foreach (var resId in listItems)
            {
                var resourceId = resId.ResourceId;

                var tokens = resourceId.Split('.');
                if (tokens.Length == 1)
                {
                    lastId = tokens[0];
                }
                else
                {
                    if (lastId == tokens[0])
                    {
                        resId.Style = "color: maroon; margin-left: 20px;";
                    }
                    lastId = tokens[0];
                }
            }

            return listItems;
        }

        /// <inheritdoc />
        public virtual List<string> GetAllResourceSets(ResourceListingTypes type)
        {
            using (var data = GetDb())
            {
                DbDataReader dt = null;

                if (type == ResourceListingTypes.AllResources)
                    dt = data.ExecuteReader("select ResourceSet as ResourceSet from " +
                        Configuration.GetResourceTableNameWithSchema() + " group by ResourceSet");
                else if (type == ResourceListingTypes.LocalResourcesOnly)
                    dt = data.ExecuteReader(
                        "select ResourceSet as ResourceSet from " +
                        Configuration.GetResourceTableNameWithSchema() +
                        " where resourceset like '%.aspx' or resourceset like '%.ascx' or resourceset like '%.master' or resourceset like '%.sitemap' group by ResourceSet",
                        data.CreateParameter("@ResourceSet", "%.%"));
                else if (type == ResourceListingTypes.GlobalResourcesOnly)
                    dt = data.ExecuteReader("select ResourceSet as ResourceSet from " +
                        Configuration.GetResourceTableNameWithSchema() +
                        " where resourceset not like '%.aspx' and resourceset not like '%.ascx' and resourceset not like '%.master' and resourceset not like '%.sitemap' group by ResourceSet");

                if (dt == null)
                {
                    ErrorMessage = data.ErrorMessage;
                    return null;
                }

                var items = new List<string>();

                while (dt.Read())
                {
                    var id = dt["ResourceSet"] as string;
                    if (!string.IsNullOrEmpty(id))
                        items.Add(id);
                }

                return items;
            }
        }

        /// <inheritdoc />
        public virtual List<string> GetAllLocaleIds(string resourceSet)
        {
            if (resourceSet == null)
                resourceSet = string.Empty;

            using (var data = GetDb())
            {
                var reader = data.ExecuteReader("select LocaleId,'' as Language from " +
                    Configuration.GetResourceTableNameWithSchema() +
                    " where ResourceSet=@ResourceSet group by LocaleId",
                    data.CreateParameter("@ResourceSet", resourceSet));

                if (reader == null)
                    return null;

                var ids = new List<string>();


                while (reader.Read())
                {
                    var id = reader["LocaleId"] as string;
                    if (id != null)
                        ids.Add(id);
                }

                return ids;
            }
        }

        /// <inheritdoc />
        public virtual List<ResourceIdItem> GetAllResourcesForCulture(string resourceSet, string cultureName)
        {
            if (cultureName == null)
                cultureName = string.Empty;

            using (var data = new SqlDataAccess(Configuration.ConnectionString))
            {
                var reader =
                    data.ExecuteReader(
                        "select ResourceId, Value from " + Configuration.GetResourceTableNameWithSchema() +
                        " where ResourceSet=@ResourceSet and LocaleId=@LocaleId",
                        data.CreateParameter("@ResourceSet", resourceSet),
                        data.CreateParameter("@LocaleId", cultureName));

                if (reader == null)
                    return null;

                var ids = new List<ResourceIdItem>();

                while (reader.Read())
                {
                    var id = reader["ResourceId"] as string;
                    if (id != null)
                        ids.Add(new ResourceIdItem
                        {
                            ResourceId = id,
                            Value = reader["Value"]
                        });
                }

                return ids;
            }
        }


        /// <inheritdoc />
        public virtual string GetResourceString(string resourceId, string resourceSet, string cultureName)
        {
            SetError();

            if (cultureName == null)
                cultureName = string.Empty;

            object result;
            using (var data = GetDb())
            {
                result = data.ExecuteScalar("select Value from " + Configuration.GetResourceTableNameWithSchema() +
                    " where ResourceId=@ResourceId and ResourceSet=@ResourceSet and LocaleId=@LocaleId",
                    data.CreateParameter("@ResourceId", resourceId),
                    data.CreateParameter("@ResourceSet", resourceSet),
                    data.CreateParameter("@LocaleId", cultureName));
            }

            return result as string;
        }


        /// <inheritdoc />
        public virtual object GetResourceObject(string resourceId, string resourceSet, string cultureName)
        {
            object result = null;
            SetError();

            if (cultureName == null)
                cultureName = string.Empty;

            DbDataReader reader;
            using (var data = GetDb())
            {
                reader =
                    data.ExecuteReader(
                        "select Value,Type from " + Configuration.GetResourceTableNameWithSchema() +
                        " where ResourceId=@ResourceId and ResourceSet=@ResourceSet and LocaleId=@LocaleId",
                        data.CreateParameter("@ResourceId", resourceId),
                        data.CreateParameter("@ResourceSet", resourceSet),
                        data.CreateParameter("@LocaleId", cultureName));

                if (reader == null)
                    return null;


                if (reader.Read())
                {
                    var resourceType = reader["Type"] as string;
                    var value = reader["Value"];

                    if (string.IsNullOrEmpty(resourceType))
                        result = value;
                    else
                        DeserializeValue(value as string, resourceType);
                }
            }

            return result;
        }

        /// <inheritdoc />
        public virtual ResourceItem GetResourceItem(string resourceId, string resourceSet, string cultureName)
        {
            ErrorMessage = string.Empty;

            if (cultureName == null)
                cultureName = string.Empty;

            ResourceItem item;
            using (var data = GetDb())
            {
                using (IDataReader reader =
                    data.ExecuteReader(
                        "select * from " + Configuration.GetResourceTableNameWithSchema() +
                        " where ResourceId=@ResourceId and ResourceSet=@ResourceSet and LocaleId=@LocaleId",
                        data.CreateParameter("@ResourceId", resourceId),
                        data.CreateParameter("@ResourceSet", resourceSet),
                        data.CreateParameter("@LocaleId", cultureName)))
                {
                    if (reader == null || !reader.Read())
                        return null;

                    item = new ResourceItem();
                    item.FromDataReader(reader);

                    reader.Close();
                }
            }

            return item;
        }

        /// <inheritdoc />
        public virtual IEnumerable<ResourceItem> GetResourceItems(string resourceId, string resourceSet,
            bool forAllResourceSetLocales = false)
        {
            ErrorMessage = string.Empty;

            if (resourceSet == null)
                resourceSet = string.Empty;

            List<ResourceItem> items = null;

            using (var data = GetDb())
            {
                using (IDataReader reader =
                    data.ExecuteReader(
                        "select * from " + Configuration.GetResourceTableNameWithSchema() +
                        " where ResourceId=@ResourceId and ResourceSet=@ResourceSet " +
                        " order by LocaleId",
                        data.CreateParameter("@ResourceId", resourceId),
                        data.CreateParameter("@ResourceSet", resourceSet)))
                {
                    if (reader == null)
                    {
                        SetError(data.ErrorMessage);
                        return null;
                    }


                    items = new List<ResourceItem>();
                    while (reader.Read())
                    {
                        var item = new ResourceItem();
                        item.FromDataReader(reader);
                        items.Add(item);
                    }

                    reader.Close();
                }

                if (forAllResourceSetLocales)
                {
                    var locales = GetAllLocalesForResourceSet(resourceSet);
                    if (locales != null)
                    {
                        var usedLocales = items.Select(i => i.LocaleId);
                        var emptyLocales = locales.Where(s => !usedLocales.Contains(s));
                        foreach (var locale in emptyLocales)
                        {
                            items.Add(new ResourceItem
                            {
                                LocaleId = locale,
                                Value = "",
                                ResourceSet = resourceSet
                            });
                        }
                    }
                }
            }

            return items;
        }


        /// <inheritdoc />
        public virtual Dictionary<string, string> GetResourceStrings(string resourceId, string resourceSet,
            bool forAllResourceSetLocales = false)
        {
            var Resources = new Dictionary<string, string>();
            using (var data = GetDb())
            {
                using (var reader = data.ExecuteReader("select Value,LocaleId from " + Configuration.GetResourceTableNameWithSchema() +
                    " where ResourceId=@ResourceId and ResourceSet=@ResourceSet order by LocaleId",
                    data.CreateParameter("@ResourceId", resourceId),
                    data.CreateParameter("@ResourceSet", resourceSet)))
                {
                    if (reader == null)
                        return null;

                    while (reader.Read())
                    {
                        Resources.Add(reader["LocaleId"] as string, reader["Value"] as string);
                    }
                    reader.Dispose();
                }

                if (forAllResourceSetLocales)
                {
                    var locales = GetAllLocalesForResourceSet(resourceSet);
                    if (locales != null)
                    {
                        var usedLocales = Resources.Select(kv => kv.Key);
                        var emptyLocales = locales.Where(s => !usedLocales.Contains(s));
                        foreach (var locale in emptyLocales)
                        {
                            Resources.Add(locale, "");
                        }
                    }
                }
            }


            return Resources;
        }

        /// <inheritdoc />
        public virtual List<string> GetAllLocalesForResourceSet(string resourceSet)
        {
            var locales = new List<string>();

            using (var data = GetDb())
            {
                var localeTable = data.ExecuteTable("TLocales",
                    "select localeId from " + Configuration.GetResourceTableNameWithSchema() +
                    " where ResourceSet=@0 group by localeId", resourceSet);
                if (localeTable != null)
                {
                    foreach (DataRow row in localeTable.Rows)
                    {
                        var val = row["localeId"] as string;
                        if (val != null)
                            locales.Add(val);
                    }
                    return locales;
                }
            }

            return null;
        }

        /// <inheritdoc />
        public virtual int UpdateOrAddResource(ResourceItem resource)
        {
            if (!IsValidCulture(resource.LocaleId))
            {
                ErrorMessage = string.Format("Can't save resource: Invalid culture id passed: {0}", resource.LocaleId);
                return -1;
            }

            var result = 0;
            result = UpdateResource(resource);

            // We either failed or we updated
            if (result != 0)
                return result;

            // We have no records matched in the Update - Add instead
            result = AddResource(resource);

            if (result == -1)
                return -1;

            return 1;
        }

        /// <inheritdoc />
        public virtual int UpdateOrAddResource(string resourceId, object value, string cultureName, string resourceSet,
            string comment = null, bool valueIsFileName = false, int valueType = 0)
        {
            if (!IsValidCulture(cultureName))
            {
                ErrorMessage = string.Format("Can't save resource: Invalid culture id passed: {0}", cultureName);
                return -1;
            }

            var result = 0;
            result = UpdateResource(resourceId, value, cultureName, resourceSet, comment, valueIsFileName);

            // We either failed or we updated
            if (result != 0)
                return result;

            // We have no records matched in the Update - Add instead
            result = AddResource(resourceId, value, cultureName, resourceSet, comment, valueIsFileName);

            if (result == -1)
                return -1;

            return 1;
        }


        /// <inheritdoc />
        public virtual int AddResource(ResourceItem resource)
        {
            var Type = string.Empty;

            if (resource.LocaleId == null)
                resource.LocaleId = string.Empty;

            if (string.IsNullOrEmpty(resource.ResourceId))
            {
                ErrorMessage = "No ResourceId specified; can't add resource";
                return -1;
            }

            if (resource.Value != null && !(resource.Value is string))
            {
                Type = resource.Value.GetType().AssemblyQualifiedName;
                try
                {
                    SerializeValue(resource.Value);
                }
                catch (Exception ex)
                {
                    ErrorMessage = ex.Message;
                    return -1;
                }
            }
            else
                Type = string.Empty;


            if (resource.Value == null)
                resource.Value = string.Empty;

            using (var data = GetDb())
            {
                if (Transaction != null)
                    data.Transaction = Transaction;

                var BinFileParm = data.CreateParameter("@BinFile", resource.BinFile, DbType.Binary);
                var TextFileParm = data.CreateParameter("@TextFile", resource.TextFile);

                var Sql = "insert into " + Configuration.GetResourceTableNameWithSchema() +
                    " (ResourceId,Value,LocaleId,Type,Resourceset,BinFile,TextFile,Filename,Comment,ValueType,Updated) Values (@ResourceID,@Value,@LocaleId,@Type,@ResourceSet,@BinFile,@TextFile,@FileName,@Comment,@ValueType,@Updated)";
                if (data.ExecuteNonQuery(Sql,
                    data.CreateParameter("@ResourceId", resource.ResourceId),
                    data.CreateParameter("@Value", resource.Value),
                    data.CreateParameter("@LocaleId", resource.LocaleId),
                    data.CreateParameter("@Type", resource.Type),
                    data.CreateParameter("@ResourceSet", resource.ResourceSet),
                    BinFileParm, TextFileParm,
                    data.CreateParameter("@FileName", resource.FileName),
                    data.CreateParameter("@Comment", resource.Comment),
                    data.CreateParameter("@ValueType", resource.ValueType),
                    data.CreateParameter("@Updated", DateTime.UtcNow)) == -1)
                {
                    ErrorMessage = data.ErrorMessage;
                    return -1;
                }
            }

            return 1;
        }

        /// <inheritdoc />
        public virtual int AddResource(string resourceId, object value,
            string cultureName, string resourceSet,
            string comment = null, bool valueIsFileName = false,
            int valueType = 0)
        {
            var Type = string.Empty;

            if (cultureName == null)
                cultureName = string.Empty;

            if (string.IsNullOrEmpty(resourceId))
            {
                ErrorMessage = "No ResourceId specified; can't add resource";
                return -1;
            }

            if (value != null && !(value is string))
            {
                Type = value.GetType().AssemblyQualifiedName;
                try
                {
                    SerializeValue(value);
                }
                catch (Exception ex)
                {
                    ErrorMessage = ex.Message;
                    return -1;
                }
            }
            else
                Type = string.Empty;

            byte[] BinFile = null;
            string TextFile = null;
            var FileName = string.Empty;

            if (valueIsFileName)
            {
                FileInfoFormat FileData = null;
                try
                {
                    FileData = GetFileInfo(value as string);
                }
                catch (Exception ex)
                {
                    ErrorMessage = ex.Message;
                    return -1;
                }

                Type = "FileResource";
                value = FileData.ValueString;
                FileName = FileData.FileName;

                if (FileData.FileFormatType == FileFormatTypes.Text)
                    TextFile = FileData.TextContent;
                else
                    BinFile = FileData.BinContent;
            }

            if (value == null)
                value = string.Empty;

            using (var data = GetDb())
            {
                if (Transaction != null)
                    data.Transaction = Transaction;

                var BinFileParm = data.CreateParameter("@BinFile", BinFile, DbType.Binary);
                var TextFileParm = data.CreateParameter("@TextFile", TextFile);

                var Sql = "insert into " + Configuration.GetResourceTableNameWithSchema() +
                    " (ResourceId,Value,LocaleId,Type,Resourceset,BinFile,TextFile,Filename,Comment,ValueType,Updated) Values (@ResourceID,@Value,@LocaleId,@Type,@ResourceSet,@BinFile,@TextFile,@FileName,@Comment,@ValueType,@Updated)";
                if (data.ExecuteNonQuery(Sql,
                    data.CreateParameter("@ResourceId", resourceId),
                    data.CreateParameter("@Value", value),
                    data.CreateParameter("@LocaleId", cultureName),
                    data.CreateParameter("@Type", Type),
                    data.CreateParameter("@ResourceSet", resourceSet),
                    BinFileParm, TextFileParm,
                    data.CreateParameter("@FileName", FileName),
                    data.CreateParameter("@Comment", comment),
                    data.CreateParameter("@ValueType", valueType),
                    data.CreateParameter("@Updated", DateTime.UtcNow)) == -1)
                {
                    ErrorMessage = data.ErrorMessage;
                    return -1;
                }
            }

            return 1;
        }

        /// <inheritdoc />
        public virtual int UpdateResource(string resourceId, object value,
            string cultureName, string resourceSet,
            string comment = null, bool valueIsFileName = false,
            int valueType = 0)
        {
            string type;
            if (cultureName == null)
                cultureName = string.Empty;


            if (value != null && !(value is string))
            {
                type = value.GetType().AssemblyQualifiedName;
                try
                {
                    value = SerializeValue(value);
                }
                catch (Exception ex)
                {
                    ErrorMessage = ex.Message;
                    return -1;
                }
            }
            else
            {
                type = string.Empty;

                if (value == null)
                    value = string.Empty;
            }

            byte[] BinFile = null;
            string TextFile = null;
            var FileName = string.Empty;

            if (valueIsFileName)
            {
                FileInfoFormat FileData = null;
                try
                {
                    FileData = GetFileInfo(value as string);
                }
                catch (Exception ex)
                {
                    ErrorMessage = ex.Message;
                    return -1;
                }

                type = "FileResource";
                value = FileData.ValueString;
                FileName = FileData.FileName;

                if (FileData.FileFormatType == FileFormatTypes.Text)
                    TextFile = FileData.TextContent;
                else
                    BinFile = FileData.BinContent;
            }

            if (value == null)
                value = string.Empty;


            int result;
            using (var data = GetDb())
            {
                if (Transaction != null)
                    data.Transaction = Transaction;

                // Set up Binfile and TextFile parameters which are set only for
                // file values - otherwise they'll pass as Null values.
                var binFileParm = data.CreateParameter("@BinFile", BinFile, DbType.Binary);
                var textFileParm = data.CreateParameter("@TextFile", TextFile);

                var sql = "update " + Configuration.GetResourceTableNameWithSchema() +
                    " set Value=@Value, Type=@Type, BinFile=@BinFile,TextFile=@TextFile,FileName=@FileName, Comment=@Comment, ValueType=@ValueType, updated=@Updated " +
                    "where LocaleId=@LocaleId AND ResourceSet=@ResourceSet and ResourceId=@ResourceId";
                result = data.ExecuteNonQuery(sql,
                    data.CreateParameter("@ResourceId", resourceId),
                    data.CreateParameter("@Value", value),
                    data.CreateParameter("@Type", type),
                    data.CreateParameter("@LocaleId", cultureName),
                    data.CreateParameter("@ResourceSet", resourceSet),
                    binFileParm, textFileParm,
                    data.CreateParameter("@FileName", FileName),
                    data.CreateParameter("@Comment", comment),
                    data.CreateParameter("@ValueType", valueType),
                    data.CreateParameter("@Updated", DateTime.UtcNow)
                );
                if (result == -1)
                {
                    ErrorMessage = data.ErrorMessage;
                    return -1;
                }
            }

            return result;
        }

        /// <inheritdoc />
        public virtual int UpdateResource(ResourceItem resource)
        {
            if (resource == null)
            {
                SetError("Resource passed cannot be null.");
                return -1;
            }

            string type = null;

            if (resource.LocaleId == null)
                resource.LocaleId = string.Empty;


            if (resource.Value != null && !(resource.Value is string))
            {
                type = resource.Value.GetType().AssemblyQualifiedName;
                try
                {
                    resource.Value = SerializeValue(resource.Value);
                }
                catch (Exception ex)
                {
                    ErrorMessage = ex.Message;
                    return -1;
                }
            }
            else if (resource.BinFile != null && string.IsNullOrEmpty(resource.Type))
                type = "FileResource";
            else
            {
                type = string.Empty;

                if (resource.Value == null)
                    resource.Value = string.Empty;
            }


            if (resource.Value == null)
                resource.Value = string.Empty;


            int result;
            using (var data = GetDb())
            {
                if (Transaction != null)
                    data.Transaction = Transaction;

                // Set up Binfile and TextFile parameters which are set only for
                // file values - otherwise they'll pass as Null values.
                var binFileParm = data.CreateParameter("@BinFile", resource.BinFile, DbType.Binary);
                var textFileParm = data.CreateParameter("@TextFile", resource.TextFile);

                var sql = "update " + Configuration.GetResourceTableNameWithSchema() +
                    " set Value=@Value, Type=@Type, BinFile=@BinFile,TextFile=@TextFile,FileName=@FileName, Comment=@Comment, ValueType=@ValueType, updated=@Updated " +
                    "where LocaleId=@LocaleId AND ResourceSet=@ResourceSet and ResourceId=@ResourceId";
                result = data.ExecuteNonQuery(sql,
                    data.CreateParameter("@ResourceId", resource.ResourceId),
                    data.CreateParameter("@Value", resource.Value),
                    data.CreateParameter("@Type", resource.Type),
                    data.CreateParameter("@LocaleId", resource.LocaleId),
                    data.CreateParameter("@ResourceSet", resource.ResourceSet),
                    binFileParm, textFileParm,
                    data.CreateParameter("@FileName", resource.FileName),
                    data.CreateParameter("@Comment", resource.Comment),
                    data.CreateParameter("@ValueType", resource.ValueType),
                    data.CreateParameter("@Updated", DateTime.UtcNow)
                );
                if (result == -1)
                {
                    ErrorMessage = data.ErrorMessage;
                    return -1;
                }
            }

            return result;
        }


        /// <inheritdoc />
        public virtual bool DeleteResource(string resourceId, string resourceSet = null, string cultureName = null)
        {
            var Result = 0;

            if (cultureName == null)
                cultureName = string.Empty;
            if (resourceSet == null)
                resourceSet = string.Empty;

            using (var data = GetDb())
            {
                if (!string.IsNullOrEmpty(cultureName))
                    // Delete the specific entry only
                    Result = data.ExecuteNonQuery("delete from " + Configuration.GetResourceTableNameWithSchema() +
                        " where ResourceId=@ResourceId and LocaleId=@LocaleId and ResourceSet=@ResourceSet",
                        data.CreateParameter("@ResourceId", resourceId),
                        data.CreateParameter("@LocaleId", cultureName),
                        data.CreateParameter("@ResourceSet", resourceSet));
                else
                    // If we're deleting the invariant entry - delete ALL of the languages for this key
                    Result = data.ExecuteNonQuery("delete from " + Configuration.GetResourceTableNameWithSchema() +
                        " where ResourceId=@ResourceId and ResourceSet=@ResourceSet",
                        data.CreateParameter("@ResourceId", resourceId),
                        data.CreateParameter("@ResourceSet", resourceSet));

                if (Result == -1)
                {
                    ErrorMessage = data.ErrorMessage;
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public virtual bool RenameResource(string ResourceId, string NewResourceId, string ResourceSet)
        {
            using (var data = GetDb())
            {
                var result = data.ExecuteNonQuery("update " + Configuration.GetResourceTableNameWithSchema() +
                    " set ResourceId=@NewResourceId where ResourceId=@ResourceId AND ResourceSet=@ResourceSet",
                    data.CreateParameter("@ResourceId", ResourceId),
                    data.CreateParameter("@NewResourceId", NewResourceId),
                    data.CreateParameter("@ResourceSet", ResourceSet));
                if (result == -1)
                {
                    ErrorMessage = data.ErrorMessage;
                    return false;
                }
                if (result == 0)
                {
                    ErrorMessage = "Invalid ResourceId";
                    return false;
                }
            }


            return true;
        }

        /// <inheritdoc />
        public virtual bool RenameResourceProperty(string Property, string NewProperty, string ResourceSet)
        {
            using (var data = GetDb())
            {
                Property += ".";
                NewProperty += ".";
                var PropertyQuery = Property + "%";
                var Result = data.ExecuteNonQuery(
                    "update " + Configuration.GetResourceTableNameWithSchema() +
                    " set ResourceId=replace(resourceid,@Property,@NewProperty) where ResourceSet=@ResourceSet and ResourceId like @PropertyQuery",
                    data.CreateParameter("@Property", Property),
                    data.CreateParameter("@NewProperty", NewProperty),
                    data.CreateParameter("@ResourceSet", ResourceSet),
                    data.CreateParameter("@PropertyQuery", PropertyQuery));
                if (Result == -1)
                {
                    SetError(data.ErrorMessage);
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public virtual bool DeleteResourceSet(string ResourceSet, string cultureName = null)
        {
            if (string.IsNullOrEmpty(ResourceSet))
                return false;

            using (var data = GetDb())
            {
                int result;
                if (cultureName == null)
                    result = data.ExecuteNonQuery("delete from " + Configuration.GetResourceTableNameWithSchema() +
                        " where ResourceSet=@ResourceSet",
                        data.CreateParameter("@ResourceSet", ResourceSet));
                else
                    result = data.ExecuteNonQuery("delete from " + Configuration.GetResourceTableNameWithSchema() +
                        " where ResourceSet=@ResourceSet and LocaleId=@LocaleId",
                        data.CreateParameter("@ResourceSet", ResourceSet),
                        data.CreateParameter("@LocaleId", cultureName));
                if (result < 0)
                {
                    SetError(data.ErrorMessage);
                    return false;
                }
                if (result == 0)
                {
                    SetError("No matching recordset found");
                    return false;
                }

                return true;
            }
        }

        /// <inheritdoc />
        public virtual bool RenameResourceSet(string OldResourceSet, string NewResourceSet)
        {
            using (var data = GetDb())
            {
                var result = data.ExecuteNonQuery(
                    "update " + Configuration.GetResourceTableNameWithSchema() +
                    " set ResourceSet=@NewResourceSet where ResourceSet=@OldResourceSet",
                    data.CreateParameter("@NewResourceSet", NewResourceSet),
                    data.CreateParameter("@OldResourceSet", OldResourceSet));
                if (result == -1)
                {
                    SetError(data.ErrorMessage);
                    return false;
                }
                if (result == 0)
                {
                    SetError("No matching recordset found");
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public virtual bool ResourceExists(string ResourceId, string CultureName, string ResourceSet)
        {
            if (CultureName == null)
                CultureName = string.Empty;

            using (var Data = GetDb())
            {
                var result = Data.ExecuteScalar("select ResourceId from " + Configuration.GetResourceTableNameWithSchema() +
                    " where ResourceId=@ResourceId and LocaleID=@LocaleId and ResourceSet=@ResourceSet group by ResourceId",
                    Data.CreateParameter("@ResourceId", ResourceId),
                    Data.CreateParameter("@LocaleId", CultureName),
                    Data.CreateParameter("@ResourceSet", ResourceSet));

                if (result == null)
                    return false;
            }

            return true;
        }

        /// <inheritdoc />
        public virtual bool IsValidCulture(string IetfTag)
        {
            try
            {
                var culture = CultureInfo.GetCultureInfoByIetfLanguageTag(IetfTag);
            }
            catch
            {
                return false;
            }
            return true;
        }

        /// <inheritdoc />
        public virtual bool GenerateResources(IDictionary resourceList, string cultureName, string resourceSet,
            bool deleteAllResourceFirst)
        {
            if (resourceList == null)
                throw new InvalidOperationException("No Resources");

            if (cultureName == null)
                cultureName = string.Empty;

            using (var data = GetDb())
            {
                if (!data.BeginTransaction())
                    return false;

                // Set transaction to be shared by other methods
                Transaction = data.Transaction;
                try
                {
                    // First delete all resources for this resource set
                    if (deleteAllResourceFirst)
                    {
                        var result = data.ExecuteNonQuery(
                            "delete " + Configuration.GetResourceTableNameWithSchema() +
                            " where LocaleId=@LocaleId and ResourceSet=@ResourceSet",
                            data.CreateParameter("@LocaleId", cultureName),
                            data.CreateParameter("@ResourceSet", resourceSet));
                        if (result == -1)
                        {
                            data.RollbackTransaction();
                            return false;
                        }
                    }
                    // Now add them all back in one by one
                    foreach (DictionaryEntry Entry in resourceList)
                    {
                        if (Entry.Value != null)
                        {
                            var Result = 0;
                            if (deleteAllResourceFirst)
                                Result = AddResource(Entry.Key.ToString(), Entry.Value, cultureName, resourceSet, null);
                            else
                                Result = UpdateOrAddResource(Entry.Key.ToString(), Entry.Value, cultureName,
                                    resourceSet, null);
                            if (Result == -1)
                            {
                                data.RollbackTransaction();
                                return false;
                            }
                        }
                    }
                }
                catch
                {
                    data.RollbackTransaction();
                    return false;
                }
                data.CommitTransaction();
            }

            // Clear out the resources
            resourceList = null;

            return true;
        }


        /// <inheritdoc />
        public virtual string GetResourcesAsJavascriptObject(string javaScriptVarName, string resourceSet,
            string localeId)
        {
            if (localeId == null)
                localeId = CultureInfo.CurrentUICulture.IetfLanguageTag;
            if (resourceSet == null)
                resourceSet = string.Empty;

            IDictionary resources = GetResourceSetNormalizedForLocaleId(
                localeId, resourceSet);

            // Filter the list to non-control resources 
            var localRes = new Dictionary<string, string>();
            foreach (string key in resources.Keys)
            {
                // We're only interested in non control local resources 
                if (!key.Contains(".") && resources[key] is string)
                    localRes.Add(key, resources[key] as string);
            }

            var json = JsonConvert.SerializeObject(localRes, Formatting.Indented);
            return "var " + javaScriptVarName + " = " + json + ";\r\n";
        }


        /// <inheritdoc />
        public virtual bool IsLocalizationTable(string tableName = null)
        {
            if (tableName == null)
                tableName = Configuration.ResourceTableName;
            if (string.IsNullOrEmpty(tableName))
                tableName = "Localizations";

            var sql = "SELECT * FROM INFORMATION_SCHEMA.TABLES where TABLE_NAME=@0 AND TABLE_SCHEMA=@1";

            using (var data = GetDb())
            {
                var tables = data.ExecuteTable("TTables", sql, tableName, Configuration.ResourceTableSchema);

                if (tables == null || tables.Rows.Count < 1)
                {
                    SetError(data.ErrorMessage);
                    return false;
                }
            }

            return true;
        }


        /// <inheritdoc />
        public virtual bool CreateBackupTable(string BackupTableName)
        {
            if (BackupTableName == null)
                BackupTableName = Configuration.GetResourceTableNameWithSchema() + "_Backup";

            using (var data = GetDb())
            {
                data.ExecuteNonQuery("drop table " + BackupTableName);
                if (data.ExecuteNonQuery(
                    "select * into " + BackupTableName + " from " + Configuration.GetResourceTableNameWithSchema()) < 0)
                {
                    SetError(data.ErrorMessage);
                    return false;
                }
            }

            return true;
        }


        /// <inheritdoc />
        public virtual bool RestoreBackupTable(string backupTableName)
        {
            if (backupTableName == null)
                backupTableName = Configuration.GetResourceTableNameWithSchema() + "_Backup";

            using (var data = GetDb())
            {
                data.BeginTransaction();

                if (data.ExecuteNonQuery("delete from " + Configuration.GetResourceTableNameWithSchema()) < 0)
                {
                    data.RollbackTransaction();
                    ErrorMessage = data.ErrorMessage;
                    return false;
                }

                var sql =
                    @"insert into {0}
  (ResourceId,Value,LocaleId,ResourceSet,Type,BinFile,TextFile,FileName,Comment) 
   select ResourceId,Value,LocaleId,ResourceSet,Type,BinFile,TextFile,FileName,Comment from {1}";

                sql = string.Format(sql, Configuration.GetResourceTableNameWithSchema(), backupTableName);

                if (data.ExecuteNonQuery(sql) < 0)
                {
                    data.RollbackTransaction();
                    SetError(data.ErrorMessage);
                    return false;
                }

                data.CommitTransaction();
            }

            return true;
        }


        /// <inheritdoc />
        public virtual bool CreateLocalizationTable(string tableName = null)
        {
            if (tableName == null)
                tableName = Configuration.ResourceTableName;
            if (string.IsNullOrEmpty(tableName))
                tableName = "Localizations";

            var Sql = string.Format(TableCreationSql, tableName, Configuration.ResourceTableSchema);


            // Check for table existing already
            if (IsLocalizationTable(tableName))
            {
                SetError("Localization table exists already");
                return false;
            }

            SetError();

            using (var data = GetDb())
            {
                if (!data.RunSqlScript(Sql, false, false))
                {
                    ErrorMessage = data.ErrorMessage;
                    return false;
                }
            }

            return true;
        }

        public void SetError()
        {
            SetError("CLEAR");
        }

        public void SetError(string message)
        {
            if (message == null || message == "CLEAR")
            {
                ErrorMessage = string.Empty;
                return;
            }
            ErrorMessage += message;
        }

        public void SetError(Exception ex)
        {
            if (ex == null)
            {
                ErrorMessage = string.Empty;
                return;
            }

            ErrorMessage = ex.GetBaseException().Message;
        }

        /// <summary>
        ///     Creates an instance of the DbResourceDataManager based on configuration settings
        /// </summary>
        /// <returns></returns>
        public static DbResourceDataManager CreateDbResourceDataManager(DbResourceConfiguration configuration,
            Type managerType = null)
        {
            if (managerType == null)
                managerType = configuration.DbResourceDataManagerType;
            if (managerType == null)
                managerType = typeof(DbResourceSqlServerDataManager);

            return ReflectionUtils.CreateInstanceFromType(managerType, configuration) as DbResourceDataManager;
        }

        /// <summary>
        ///     Create an instance of the provider based on the resource type
        /// </summary>
        /// <returns></returns>
        public static DbResourceDataManager CreateDbResourceDataManager(DbResourceProviderTypes type,
            DbResourceConfiguration configuration)
        {
            switch (type)
            {
                case DbResourceProviderTypes.SqlServer:
                    return new DbResourceSqlServerDataManager(configuration);
                case DbResourceProviderTypes.MySql:
                    return new DbResourceMySqlDataManager(configuration);
                case DbResourceProviderTypes.SqLite:
                    return new DbResourceSqLiteDataManager(configuration);
                default:
                    return null;
            }
        }

        protected virtual void OnResourceSetValueConvert(ref object resourceValue, string key, int valueType)
        {
            foreach (var resourceSetValueConverter in Configuration.ResourceSetValueConverters)
            {
                if (valueType == resourceSetValueConverter.ValueType)
                    resourceValue = resourceSetValueConverter.Convert(resourceValue, key);
            }
        }

        public static ResourceItem SetFileDataOnResourceItem(ResourceItem item, byte[] data, string fileName)
        {
            if (data == null || item == null || string.IsNullOrEmpty(fileName))
                throw new ArgumentException("Missing file upload data");

            var ext = Path.GetExtension(fileName).TrimStart('.').ToLower();
            const string filter = ",bmp,ico,gif,jpg,jpeg,png,css,js,txt,html,htm,xml,wav,mp3,";
            if (!filter.Contains("," + ext + ","))
                throw new ArgumentException("Invalid file extension for file resource");

            string type;
            if ("jpg,jpeg,png,gif,bmp".Contains(ext))
//                type = typeof (Bitmap).AssemblyQualifiedName;
                throw new NotImplementedException();
            if ("ico" == ext)
//                type = typeof(Icon).AssemblyQualifiedName;
                throw new NotImplementedException();
            if ("txt,css,htm,html,xml,js".Contains(ext))
                type = typeof(string).AssemblyQualifiedName;
            else
                type = typeof(byte[]).AssemblyQualifiedName;

            using (var ms = new MemoryStream())
            {
                item.Value = fileName + ";" + type;
                item.BinFile = data;
                item.Type = "FileResource";
                item.FileName = fileName;
            }

            return item;
        }


        /// <summary>
        ///     Internal method used to parse the data in the database into a 'real' value.
        ///     Value field hold filename and type string
        ///     TextFile,BinFile hold the actual file content
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private object LoadFileResource(IDataReader reader)
        {
            object value = null;

            try
            {
                var TypeInfo = reader["Value"] as string;

                if (TypeInfo.IndexOf("System.String") > -1)
                {
                    value = reader["TextFile"] as string;
                }
                else if (TypeInfo.Contains("System.Drawing.Bitmap"))
                {
                    // IMPORTANT: don't release the mem stream or Jpegs won't render/save
                    if (TypeInfo.Contains(".jpg") || TypeInfo.Contains(".jpeg"))
                    {
                        throw new NotImplementedException();
                        // Some JPEGs require that the memory stream stays open in order
                        // to use the Bitmap later. Let CLR worry about garbage collection
                        // Prefer: Don't store jpegs
//                        var ms = new MemoryStream(reader["BinFile"] as byte[]);
//                        value = new Bitmap(ms);                        
                    }
                    throw new NotImplementedException();
//                        using (var ms = new MemoryStream(reader["BinFile"] as byte[]))
//                        {
//                            value = new Bitmap(ms);
//                        }
                }
                else if (TypeInfo.Contains("System.Drawing.Icon"))
                {
                    throw new NotImplementedException();
                    // IMPORTANT: don't release the mem stream 
//                    var ms = new MemoryStream(reader["BinFile"] as byte[]);
//                    value = new Icon(ms);
                }
                else
                {
                    value = reader["BinFile"] as byte[];
                }
            }
            catch (Exception ex)
            {
                SetError(reader["ResourceKey"] + ": " + ex.Message);
            }

            return value;
        }


        /// <summary>
        ///     Internal routine that looks at a file and based on its
        ///     extension determines how that file needs to be stored in the
        ///     database. Returns FileInfoFormat structure
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static FileInfoFormat GetFileInfo(string fileName, bool noPhysicalFile = false)
        {
            var fileInfo = new FileInfoFormat();

            var fi = new FileInfo(fileName);
            if (!noPhysicalFile && !fi.Exists)
                throw new InvalidOperationException("Invalid Filename");

            var Extension = fi.Extension.ToLower().TrimStart('.');
            fileInfo.FileName = fi.Name;

            if (Extension == "txt" || Extension == "css" || Extension == "js" || Extension.StartsWith("htm") ||
                Extension == "xml")
            {
                fileInfo.FileFormatType = FileFormatTypes.Text;
                fileInfo.Type = "FileResource";

                if (!noPhysicalFile)
                {
                    using (var sr = new StreamReader(fileName, Encoding.Default, true))
                    {
                        fileInfo.TextContent = sr.ReadToEnd();
                    }
                }
                fileInfo.ValueString = fileInfo.FileName + ";" + typeof(string).AssemblyQualifiedName + ";" +
                    Encoding.Default.HeaderName;
            }
            else if (Extension == "gif" || Extension == "jpg" || Extension == "jpeg" || Extension == "bmp" ||
                Extension == "png")
            {
                throw new NotImplementedException();
//                fileInfo.FileFormatType = FileFormatTypes.Image;
//                fileInfo.Type = "FileResource";
//                if(!noPhysicalFile)
//                    fileInfo.BinContent = File.ReadAllBytes(fileName);
//                fileInfo.ValueString = fileInfo.FileName + ";" + typeof(Bitmap).AssemblyQualifiedName;
            }
            else if (Extension == "ico")
            {
                throw new NotImplementedException();
//                fileInfo.FileFormatType = FileFormatTypes.Image;
//                fileInfo.Type = "System.Drawing.Icon, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
//                if (!noPhysicalFile)
//                    fileInfo.BinContent = File.ReadAllBytes(fileName);
//                fileInfo.ValueString = fileInfo.FileName + ";" + typeof(Icon).AssemblyQualifiedName;
            }
            else
            {
                fileInfo.FileFormatType = FileFormatTypes.Binary;
                fileInfo.Type = "FileResource";
                if (!noPhysicalFile)
                    fileInfo.BinContent = File.ReadAllBytes(fileName);
                fileInfo.ValueString = fileInfo.FileName + ";" + typeof(byte[]).AssemblyQualifiedName;
            }

            return fileInfo;
        }

        /// <summary>
        ///     Serializes a value to string that can be stored in
        ///     data storage.
        ///     Used for serializing arbitrary objects to store in the application
        /// </summary>
        /// <param name="value"></param>
        /// <returns>JSON string or null (no exceptions thrown on error)</returns>
        protected virtual string SerializeValue(object value) => JsonConvert.SerializeObject(value);

        /// <summary>
        ///     Deserializes serialized data in JSON format based on a
        ///     type name provided in the resource type parameter.
        /// </summary>
        /// <param name="serializedValue">JSON encoded string</param>
        /// <param name="resourceType">Type name to deserialize - type must be referenced by the app</param>
        /// <returns>value or null on failure (no exceptions thrown)</returns>
        protected virtual object DeserializeValue(string serializedValue, string resourceType)
        {
            var type = ReflectionUtils.GetTypeFromName(resourceType);
            if (type == null)
                return null;

            return JsonConvert.DeserializeObject(serializedValue, type);
        }
    }

    /// <summary>
    ///     The data managers supported by this library
    /// </summary>
    public enum DbResourceDataManagerTypes
    {
        SqlServer,
        SqlServerCe,
        MySql,
        SqlLite,
        MongoDb, // not implemented yet
        None
    }

    /// <summary>
    ///     Short form ResourceItem for passing Ids
    /// </summary>
    public class ResourceIdItem
    {
        public string ResourceId { get; set; }
        public bool HasValue { get; set; }
        public object Value { get; set; }
    }

    public class BasicResourceItem
    {
        public string ResourceId { get; set; }
        public string LocaleId { get; set; }
        public string ResourceSet { get; set; }
        public string Value { get; set; }
    }

    public class ResourceIdListItem : ResourceIdItem
    {
        public string Text { get; set; }
        public bool Selected { get; set; }
        public string Style { get; set; }
    }

    /// <summary>
    ///     Determines how hte GetAllResourceSets method returns its data
    /// </summary>
    public enum ResourceListingTypes
    {
        LocalResourcesOnly,
        GlobalResourcesOnly,
        AllResources
    }

    public enum FileFormatTypes
    {
        Text,
        Image,
        Binary
    }

    public enum DbResourceProviderTypes
    {
        SqlServer,
        MySql,
        SqLite
    }

    /// <summary>
    ///     Internal structure that contains format information about a file
    ///     resource. Used internally to figure out how to write
    ///     a resource into the database
    /// </summary>
    public class FileInfoFormat
    {
        public byte[] BinContent;
        public string Encoding = string.Empty;
        public FileFormatTypes FileFormatType = FileFormatTypes.Binary;
        public string FileName = string.Empty;
        public string TextContent = string.Empty;
        public string Type = "File";
        public string ValueString = string.Empty;
    }
}