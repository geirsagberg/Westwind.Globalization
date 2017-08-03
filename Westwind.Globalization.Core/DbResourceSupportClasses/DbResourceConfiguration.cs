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


#define IncludeWebFormsControls

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Westwind.Globalization.Core.DbResourceDataManager.DbResourceDataManagers;
using Westwind.Globalization.Core.DbResourceDataManager.ResourceSetValueConverters;
using Westwind.Globalization.Core.DbResourceManager;
using Westwind.Globalization.Core.Utilities;
using Westwind.Utilities.Configuration;

namespace Westwind.Globalization.Core.DbResourceSupportClasses
{
    /// <summary>
    /// The configuration class that is used to configure the Resource Provider.
    /// This class contains various configuration settings that the provider requires
    /// to operate both at design time and runtime.
    /// 
    /// The application uses the static Current property to access the actual
    /// configuration settings object. By default it reads the configuration settings
    /// from web.config (at runtime). You can override this behavior by creating your
    /// own configuration object and assigning it to the DbResourceConfiguration.Current property.
    /// </summary>
    public class DbResourceConfiguration
    {
        
        /// <summary>
        /// Name of a LocalizationConfiguration entry that is loaded from the database
        /// if available. Defaults to null - if set reads these configuration settings
        /// other than the database connection string from an entry in the 
        /// LocalizationConfigurations table.
        /// </summary>
        public string ActiveConfiguration { get; set; }

        /// <summary>
        /// A global instance of the current configuration. By default this instance reads its
        /// configuration values from web.config at runtime, but it can be overridden to
        /// assign specific values or completely replace this object. 
        /// 
        /// NOTE: Any assignment made to this property should be made at Application_Start
        /// or other 'application initialization' event so that these settings are applied
        /// BEFORE the resource provider is used for the first time.
        /// </summary>
//        public static DbResourceConfiguration Current = null;


        /// <summary>
        /// Database connection string to the resource data.
        /// 
        /// The string can either be a full connection string or an entry in the 
        /// ConnectionStrings section of web.config.
        /// <seealso>Class DbResource
        /// Compiling Your Applications with the Provider</seealso>
        /// </summary>
        public string ConnectionString { get; set; } = "*** ENTER A CONNECTION STRING OR connectionStrings ENTRY HERE ***";

        /// <summary>
        /// Database table name used in the database
        /// </summary>
        public string ResourceTableName { get; set; } = "Localizations";

        /// <summary>
        /// Database schema used in the database (if supported)
        /// </summary>
        public string ResourceTableSchema { get; set; } = "dbo";

        /// <summary>
        /// Full table name with schema, e.g. "dbo.Localizations"
        /// </summary>
        public string GetResourceTableNameWithSchema() => $"{ResourceTableSchema}.{ResourceTableName}";

        /// <summary>
        /// Path of an optionally generated strongly typed resource
        /// which is created when exporting to ResX resources.
        /// 
        /// Leave this value blank if you don't want a strongly typed resource class
        /// generated for you.
        /// 
        /// Otherwise format is: 
        /// ~/App_Code/Resources.cs
        /// </summary>
        public string StronglyTypedGlobalResource { get; set; } = "~/App_Code/Resources.cs";


        /// <summary>
        /// The namespace used for exporting and importing resources 
        /// </summary>
        public string ResourceBaseNamespace { get; set; } = "AppResources";


        /// <summary>
        /// Determines how what type of project we are working with
        /// </summary>
        public GlobalizationResxExportProjectTypes ResxExportProjectType { get; set; } = GlobalizationResxExportProjectTypes.Project;


        /// <summary>
        /// The base physical path used to read and write RESX resources for resource imports
        /// and exports. This path can either be a virtual path in Web apps or a physical disk
        /// path. Used only by the Web Admin form. All explicit API imports and exports
        /// can pass in the base path explicitly.
        /// </summary>
        public string ResxBaseFolder { get; set; } = "~/Properties/";

        /// <summary>
        /// Determines whether any resources that are not found are automatically
        /// added to the resource file.
        /// 
        /// Note only applies to the Invariant culture.
        /// </summary>
        public bool AddMissingResources { get; set; } = true;


        /// <summary>
        /// Default mechanism used to access resources in DbRes.T().           
        /// This setting is global and used by all resources running through
        /// the DbResourceManage/Provider.
        /// 
        /// This doesn't not affect Generated REsources which have their own 
        /// ResourceAccesssMode override that can be explicitly overridden.    
        /// </summary>
        public ResourceAccessMode ResourceAccessMode { get; set; } = ResourceAccessMode.DbResourceManager;

        /// <summary>
        /// Determines the location of the Localization form in a Web relative path.
        /// This form is popped up when clicking on Edit Resources in the 
        /// DbResourceControl
        /// </summary>        
        public string LocalizationFormWebPath { get; set; } = "~/LocalizationAdmin/";


        /// <summary>
        /// API key for Bing Translate API in the 
        /// Administration API.
        /// </summary>
        public string BingClientId { get; set; }

        /// <summary>
        /// Bing Secret Key for Bing Translate API Access
        /// </summary>
        public string BingClientSecret { get; set; }

        /// <summary>
        /// Google Translate API Key used to access Translate API.
        /// Note this is a for pay API!
        /// </summary>
        public string GoogleApiKey { get; set; }
        


        public List<IResourceSetValueConverter> ResourceSetValueConverters = new List<IResourceSetValueConverter>();


        /// <summary>
        /// Allows you to override the data provider used to access resources.
        /// Defaults to Sql Server. To override set this value during application
        /// startup - typical on DbResourceConfiguration.Current.DbResourceDataManagerType
        /// 
        /// This type instance is used to instantiate the actual provider.       
        /// </summary>
        [XmlIgnore]                
        [NonSerialized]
        public Type DbResourceDataManagerType = typeof(DbResourceSqlServerDataManager);

        


        /// <summary>
        /// Base constructor that doesn't do anything to the default values.
        /// </summary>
        public DbResourceConfiguration()
        {
            AddResourceSetValueConverter(new MarkdownResourceSetValueConverter());
        }

        public void AddResourceSetValueConverter(IResourceSetValueConverter converter)
        {
            ResourceSetValueConverters.Add(converter);
        }        


        /// <summary>
        /// Triggered when resources are reloaded. Can be used for e.g. cache invalidation.
        /// </summary>
        public event Action OnResourcesReloaded;

        /// <summary>
        /// This static method clears all resources from the loaded Resource Providers 
        /// and forces them to be reloaded the next time they are requested.
        /// 
        /// Use this method after you've edited resources in the database and you want 
        /// to refresh the UI to show the newly changed values.
        /// 
        /// This method works by internally tracking all the loaded ResourceProvider 
        /// instances and calling the IwwResourceProvider.ClearResourceCache() method 
        /// on each of the provider instances. This method is called by the Resource 
        /// Administration form when you explicitly click the Reload Resources button.
        /// <seealso>Class DbResourceConfiguration</seealso>
        /// </summary>
        public void ClearResourceCache()
        {
            OnResourcesReloaded?.Invoke();

            // clear any resource managers
            DbRes.ClearResources();
        }
    }

    /// <summary>
    /// Project types for Resx Exports. Either WebForms using 
    /// local and global resources files, or project
    /// </summary>
    public enum GlobalizationResxExportProjectTypes 
    {        
        /// <summary>
        /// Any .NET project other than WebForms that 
        /// uses a single directory (Properties) for 
        ///  Resx resources
        /// </summary>
        Project

    }

    public enum CodeGenerationLanguage
    {
        CSharp,
        Vb
    }
}
