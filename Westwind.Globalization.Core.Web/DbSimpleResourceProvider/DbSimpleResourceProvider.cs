#region License
/*
 **************************************************************
 *  Author: Rick Strahl 
 *          � West Wind Technologies, 2009-2012
 *          http://www.west-wind.com/
 * 
 * Created: 02/10/2009
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

/**************************************************************************************************************
 * This file contains a simplified ASP.NET Resource Provider that doesn't create a custom Resource Manager.
 * This implementation is much simpler than the full resource provider, but it's also not as integrated as
 * the full implementation. You can use this provider safely to serve resources, but for resource
 * editing and Visual Studio integration preferrably use the full provider.
 * 
 * This class shows how the Provider model works a little more clearly because this class is
 * self contained with the exception of the data access code and you can use this as a starting
 * point to build a custom provider. There are no ResourceReaders/Writers just a nested collection 
 * of resources.
 * 
 * This class uses DbResourceDataManager to retrieve and write resources in exactly two
 * places of the code. If you prefer you can replace these two locations with your own custom
 * Resource implementation. They are marked with:
 * 
 * // DEPENDENCY HERE
 * 
 * However, I would still recommend going with the full resource manager based implementation
 * because it works in any .NET application, not just ASP.NET. But a full resource manager
 * based implementation is much more complicated to create.
**************************************************************************************************************/


using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using Westwind.Globalization.Core.DbResourceSupportClasses;

namespace Westwind.Globalization.Core.Web.DbSimpleResourceProvider
{

    /// <summary>
    /// Implementation of a very simple database Resource Provider. This implementation
    /// is self contained and doesn't use a custom ResourceManager. Instead it
    /// talks directly to the data resoure business layer (DbResourceDataManager).
    /// 
    /// Dependencies:
    /// DbResourceDataManager
    /// DbResourceConfiguration
    /// 
    /// You can replace those depencies (marked below in code) with your own data access
    /// management. The two dependcies manage all data access as well as configuration 
    /// management via web.config configuration section. It's easy to remove these
    /// and instead use custom data access code of your choice.
    /// </summary>
    [DebuggerDisplay("ResourceSet: {_ResourceSetName}")]
    public class DbSimpleResourceProvider : IWestWindResourceProvider //, IImplicitResourceProvider
    {
        private readonly DbResourceConfiguration configuration;

        /// <summary>
        /// Keep track of the 'className' passed by ASP.NET
        /// which is the ResourceSetId in the database.
        /// </summary>
        private string _ResourceSetName;

        /// <summary>
        /// Cache for each culture of this ResourceSet. Once
        /// loaded we just cache the resources.
        /// </summary>
        private IDictionary _resourceCache;


        public static bool ProviderLoaded = false;

        /// <summary>
        /// Critical section for loading Resource Cache safely
        /// </summary>
        private static object _SyncLock = new object();


        /// <summary>
        /// </summary>
        /// <param name="virtualPath">The virtual path to the Web application</param>
        /// <param name="resourceSet">Name of the resource set to load</param>
        /// <param name="configuration"></param>
        public DbSimpleResourceProvider(string virtualPath, string resourceSet, DbResourceConfiguration configuration)
        {
            this.configuration = configuration;
            lock (_SyncLock)
            {
                ProviderLoaded = true;
                _ResourceSetName = resourceSet;
                DbResourceConfiguration.LoadedProviders.Add(this);
            }
        }

        /// <summary>
        /// Manages caching of the Resource Sets. Once loaded the values are loaded from the 
        /// cache only.
        /// </summary>
        /// <param name="cultureName"></param>
        /// <returns></returns>
        private IDictionary GetResourceCache(string cultureName)
        {
            if (cultureName == null)
                cultureName = "";
             
            if (_resourceCache == null)
                _resourceCache = new ListDictionary();

            IDictionary resources = _resourceCache[cultureName] as IDictionary;
            if (resources == null)
            {
                // DEPENDENCY HERE (#1): Using DbResourceDataManager to retrieve resources

                // Use datamanager to retrieve the resource keys from the database
                var data = DbResourceDataManager.DbResourceDataManager.CreateDbResourceDataManager(configuration);                                 

                lock (_SyncLock)
                {
                    if (resources == null)
                    {
                        if (_resourceCache.Contains(cultureName))
                            resources = _resourceCache[cultureName] as IDictionary;
                        else
                        {
                            resources = data.GetResourceSet(cultureName as string, _ResourceSetName);
                            _resourceCache[cultureName] = resources;
                        }
                    }
                }
            }

            return resources;
        }

        /// <summary>
        /// Clears out the resource cache which forces all resources to be reloaded from
        /// the database.
        /// 
        /// This is never actually called as far as I can tell
        /// </summary>
        public void ClearResourceCache()
        {
            lock (_SyncLock)
            {
                _resourceCache.Clear();
            }
        }


        /// <summary>
        /// The Resource Reader is used parse over the resource collection
        /// that the ResourceSet contains. It's basically an IEnumarable interface
        /// implementation and it's what's used to retrieve the actual keys
        /// </summary>
        public IResourceReader ResourceReader  // IResourceProvider.ResourceReader
        {
            get
            {
                if (_ResourceReader != null)
                    return _ResourceReader as IResourceReader;

                _ResourceReader = new DbSimpleResourceReader(GetResourceCache(null));
                return _ResourceReader as IResourceReader;
            }
        }
        private DbSimpleResourceReader _ResourceReader = null;




    }
}