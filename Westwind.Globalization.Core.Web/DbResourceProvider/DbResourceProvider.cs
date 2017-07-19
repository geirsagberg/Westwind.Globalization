/*
 **************************************************************
 * DbReourceManager Class
 **************************************************************
 *  Author: Rick Strahl 
 *          (c) West Wind Technologies
 *          http://www.west-wind.com/
 * 
 * Created: 10/10/2006
 * Updated: 3/10/2008 
 **************************************************************  
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using Westwind.Globalization.Core.DbResourceManager;
using Westwind.Globalization.Core.DbResourceSupportClasses;

namespace Westwind.Globalization.Core.Web.DbResourceProvider
{
    /// <summary>
    /// The DbResourceProvider class is an ASP.NET Resource Provider implementation
    /// that retrieves its resources from a database. It works in conjunction with a
    /// DbResourceManager object and so uses standard .NET Resource mechanisms to 
    /// retrieve its data. The provider should be fairly efficient and other than
    /// initial load time standard .NET resource caching is used to hold resource sets
    /// in memory.
    /// 
    /// The Resource Provider class provides the base interface for accessing resources.
    /// This provider interface handles loading resources, caching them (using standard
    /// Resource Manager functionality) and allowing access to resources via GetObject.
    /// 
    /// This provider supports global and local resources, explicit expressions
    /// as well as implicit expressions (IImplicitResourceProvider).
    /// 
    /// There's also a design time implementation to provide Generate LocalResources
    /// support from ASP.NET Web Form designer.
    /// </summary>
    public class DbResourceProvider : IWestWindResourceProvider
    {
        private readonly DbResourceConfiguration configuration;

        /// <summary>
        /// 
        /// </summary>
        string _className;

        static object _SyncLock = new object();

        /// <summary>
        /// Flag that can be read to see if the resource provider is loaded
        /// </summary>
        public static bool ProviderLoaded = false;


        /// <summary>
        /// Default constructor - only captures the parameter values
        /// </summary>
        /// <param name="virtualPath"></param>
        /// <param name="classname"></param>
        /// <param name="configuration"></param>
        public DbResourceProvider(string virtualPath, string classname, DbResourceConfiguration configuration)
        {
            this.configuration = configuration;
            lock (_SyncLock)
            {
                if (!ProviderLoaded)
                    ProviderLoaded = true;

                //  _virtualPath = virtualPath;
                _className = classname;
                DbResourceConfiguration.LoadedProviders.Add(this);
            }
        }

        /// <summary>
        /// IResourceProvider interface - required to provide an instance to an
        /// ResourceManager object.
        /// 
        /// Note that the resource manager is not tied to a specific culture by
        /// default. The Provider uses the UiCulture without explicitly passing
        /// culture info.
        /// </summary>
        public DbResourceManager.DbResourceManager ResourceManager
        {
            get
            {
                if (_ResourceManager == null)
                {
                    DbResourceManager.DbResourceManager manager = new DbResourceManager.DbResourceManager(configuration, _className);
                    manager.IgnoreCase = true;
                    _ResourceManager = manager;                    
                }
                return _ResourceManager;
            }
        }
        private DbResourceManager.DbResourceManager _ResourceManager = null;


        /// <summary>
        /// Releases all resources and forces resources to be reloaded
        /// from storage on the next GetResourceSet
        /// </summary>
        public void ClearResourceCache()
        {
            ResourceManager.ReleaseAllResources(); 
        }



        /// <summary>
        /// Required instance of the ResourceReader for this provider. Part of
        /// the IResourceProvider interface. The reader is responsible for feeding
        /// the Resource data from a ResourceSet. The interface basically walks
        /// an enumerable interface by ResourceId.
        /// </summary>
        public IResourceReader ResourceReader
        {
            get
            {
                if (_ResourceReader == null)
                    _ResourceReader = new DbResourceReader(_className, CultureInfo.InvariantCulture, configuration);

                return _ResourceReader;
            }
        }
        private DbResourceReader _ResourceReader = null;
       


    }

}
