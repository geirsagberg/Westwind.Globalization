/*
 **************************************************************
 * DbReourceManager Class
 **************************************************************
 *  Author: Rick Strahl 
 *          (c) West Wind Technologies
 *          http://www.west-wind.com/
 * 
 * Created: 10/10/2006
 * 
 * based in part on code provided in:
 * ----------------------------------
 * .NET Internationalization
 *      Addison Wesley Books
 *      by Guy Smith Ferrier
 * 
 **************************************************************  
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Resources;
using Westwind.Globalization.Core.DbResourceSupportClasses;

namespace Westwind.Globalization.Core.DbResourceManager
{
    /// <summary>
    ///     This class provides a databased implementation of a ResourceManager.
    ///     A ResourceManager holds each of the InternalResourceSets for a given group
    ///     of resources. In ResX files a group is a file group wiht the same name
    ///     (ie. Resources.resx, Resources.en.resx, Resources.de.resx). In this
    ///     database driven provider the group is determined by the ResourceSet
    ///     and the LocaleId as stored in the database. This class is instantiated
    ///     and gets passed both of these values for identity.
    ///     An application can have many ResourceManagers - one for each localized
    ///     page and one for each global resource with each hold multiple resourcesets
    ///     for each of the locale's that are part of that resourceSet.
    ///     This class implements only the GetInternalResourceSet method to
    ///     provide the ResourceSet from a database. It also implements all the
    ///     base class constructors and captures only the BaseName which
    ///     is the name of the ResourceSet (ie. a global or local resource group)
    ///     Dependencies:
    ///     DbResourceDataManager for data access
    ///     DbResourceConfiguration which holds and reads config settings
    ///     DbResourceSet
    ///     DbResourceReader
    /// </summary>
    public class DbResourceManager : ResourceManager
    {
        // Duplicate the Resource Manager Constructors below
        // Key feature of these overrides is to set up the BaseName
        // which is the name of the resource set (either a local
        // or global resource. Each ResourceManager controls one set
        // of resources (global or local) and manages the ResourceSet
        // for each of cultures that are part of that ResourceSet

        /// <summary>
        ///     Critical Section lock used for loading/adding resource sets
        /// </summary>
        private static readonly object SyncLock = new object();

        private static readonly object AddSyncLock = new object();

        // InternalResourceSets contains a set of resources for each locale
        private readonly Dictionary<string, ResourceSet> internalResourceSets = new Dictionary<string, ResourceSet>();

        private readonly DbResourceConfiguration configuration;

        /// <summary>
        ///     Constructs a DbResourceManager object
        /// </summary>
        public DbResourceManager(DbResourceConfiguration configuration, string baseName) : this(configuration, baseName,
            Assembly.GetEntryAssembly())
        {
        }

        public DbResourceManager(DbResourceConfiguration configuration, Type resourceType) : this(configuration,
            resourceType.Name, resourceType.Assembly)
        {
        }

        /// <summary>
        ///     Constructs a DbResourceManager object. Match base constructors.
        ///     Core Configuration method that sets up the ResourceManager. For this
        ///     implementation we only need the baseName which is the ResourceSet id
        ///     (ie. the local or global resource set name) and the assembly name is
        ///     simply ignored.
        ///     This method essentially sets up the ResourceManager and holds all
        ///     of the culture specific resource sets for a single ResourceSet. With
        ///     ResX files each set is a file - in the database a ResourceSet is a group
        ///     with the same ResourceSet Id.
        /// </summary>
        public DbResourceManager(DbResourceConfiguration configuration, string baseName, Assembly assembly) : base(
            baseName, assembly)
        {
            AutoAddMissingEntries = configuration?.AddMissingResources ?? throw new ArgumentNullException(nameof(configuration));
            this.configuration = configuration;
        }

        /// <summary>
        ///     If true causes any entries that aren't found to be added
        /// </summary>
        public bool AutoAddMissingEntries { get; set; }

        public override Type ResourceSetType => typeof(DbResourceSet);


        /// <summary>
        ///     This is the only method that needs to be overridden as long as we
        ///     provide implementations for the ResourceSet/ResourceReader/ResourceWriter
        /// </summary>
        /// <param name="culture"></param>
        /// <param name="createIfNotExists"></param>
        /// <param name="tryParents"></param>
        /// <returns></returns>
        protected override ResourceSet InternalGetResourceSet(CultureInfo culture, bool createIfNotExists,
            bool tryParents)
        {
            var resourceSets = internalResourceSets;

            // retrieve cached instance - outside of lock for perf
            if (resourceSets.ContainsKey(culture.Name))
                return resourceSets[culture.Name];

            lock (SyncLock)
            {
                // have to check again to ensure still not existing
                if (resourceSets.ContainsKey(culture.Name))
                    return resourceSets[culture.Name];

                // Otherwise create a new instance, load it and return it
                var rs = new DbResourceSet(BaseName, culture, configuration);

                // Add the resource set to the cached set
                resourceSets.Add(culture.Name, rs);

                // And return an instance
                return rs;
            }
        }

        /// <summary>
        ///     Clears all resource sets and forces reloading
        ///     on next resource set retrieval. Effectively
        ///     this refreshes resources if the source has
        ///     changed. Required to see DB changes in the
        ///     live UI.
        /// </summary>
        public override void ReleaseAllResources()
        {
            base.ReleaseAllResources();
            internalResourceSets.Clear();
        }


        // GetObject implementations to retrieve values - not required but useful to see operation
        /// <summary>
        ///     Core worker method on the manager that returns resource. This
        ///     override returns the resource for the currently active UICulture
        ///     for this manager/resource set.
        ///     If resource is not found it returns null
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public override object GetObject(string name)
        {
            var value = base.GetObject(name);

            if (value == null && AutoAddMissingEntries)
                AddMissingResource(name, name);

            return value;
        }

        /// <summary>
        ///     Core worker method that returns a  resource value for a
        ///     given culture from the this resourcemanager/resourceset.
        ///     If resource is not found it returns the null
        /// </summary>
        /// <param name="name"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public override object GetObject(string name, CultureInfo culture)
        {
            var value = base.GetObject(name, culture);

            if (value == null && AutoAddMissingEntries)
                AddMissingResource(name, name);

            return value;
        }

        /// <summary>
        ///     Add a new resource to the base resource set
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void AddMissingResource(string name, string value, CultureInfo culture = null)
        {
            var manager = DbResourceDataManager.DbResourceDataManager.CreateDbResourceDataManager(configuration);

            var cultureName = string.Empty;
            if (culture != null)
                cultureName = culture.IetfLanguageTag;

            lock (AddSyncLock)
            {
                // double check if culture neutral version exists
                var item = manager.GetResourceObject(name, BaseName, cultureName) as string;
                if (item != null)
                    return;

                manager.AddResource(name, value, cultureName, BaseName, null);
            }
        }
    }
}