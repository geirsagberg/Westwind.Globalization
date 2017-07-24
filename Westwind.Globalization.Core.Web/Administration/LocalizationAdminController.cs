using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Westwind.Globalization.Core.DbResourceDataManager;
using Westwind.Globalization.Core.DbResourceManager;
using Westwind.Globalization.Core.DbResourceSupportClasses;
using Westwind.Globalization.Core.Utilities;
using Westwind.Globalization.Core.Web.Utilities;

namespace Westwind.Globalization.Core.Web.Administration
{
    [Route("[controller]/api")]
    public class LocalizationAdminController : Controller
    {
        private const string StrResourceset = "LocalizationForm";
        private readonly DbResourceConfiguration configuration;
        private readonly IHostingEnvironment hostingEnvironment;
        private readonly JavaScriptResourceHandler javaScriptResourceHandler;
        private readonly JsonSerializerSettings jsonSerializerSettings;

        private readonly IDbResourceDataManager manager;

        public LocalizationAdminController(IDbResourceDataManager manager, IHostingEnvironment hostingEnvironment,
            DbResourceConfiguration configuration, JavaScriptResourceHandler javaScriptResourceHandler)
        {
            this.manager = manager;
            this.hostingEnvironment = hostingEnvironment;
            this.configuration = configuration;
            this.javaScriptResourceHandler = javaScriptResourceHandler;
            jsonSerializerSettings = JsonSerializerSettingsProvider.CreateSerializerSettings();
            jsonSerializerSettings.ContractResolver = new DefaultContractResolver();
        }

        [HttpGet("[action]")]
        public JsonResult GetResourceList(string resourceSet)
        {
            var ids = manager.GetAllResourceIds(resourceSet);
            if (ids == null)
                throw new WestwindException("Resource set loading failed: " +
                                            manager.ErrorMessage);

            return Json(ids, jsonSerializerSettings);
        }


        /// <summary>
        ///     Returns a shaped objects that can be displayed in an editable grid the grid view for locale ids
        ///     of resources.
        /// </summary>
        [HttpGet("[action]")]
        public JsonResult GetAllResourcesForResourceGrid(string resourceSet)
        {
            var items = manager.GetAllResources(resourceSet: resourceSet);

            if (items == null)
                throw new WestwindException(manager.ErrorMessage);

            // reorder and reshape the data
            var itemList = items
                .OrderBy(it => it.ResourceId + "_" + it.LocaleId)
                .Select(it => new BasicResourceItem
                {
                    ResourceId = it.ResourceId,
                    LocaleId = it.LocaleId,
                    ResourceSet = it.ResourceSet,
                    Value = it.Value as string
                }).ToList();

            var totalLocales = itemList.GroupBy(it => it.LocaleId).Select(it => it.Key).ToList();

            foreach (var item in itemList.GroupBy(it => it.ResourceId))
            {
                var resid = item.Key;
                var resItems = itemList.Where(it => it.ResourceId == resid).ToList();
                if (resItems.Count < totalLocales.Count)
                {
                    var basicResourceItems = from locale in totalLocales
                        where resItems.All(ri => ri.LocaleId != locale)
                        select new BasicResourceItem
                        {
                            ResourceId = resid,
                            LocaleId = locale,
                            ResourceSet = resourceSet
                        };
                    itemList.AddRange(basicResourceItems);
                }
            }
            itemList = itemList.OrderBy(it => it.ResourceId + "_" + it.LocaleId).ToList();

            var resultList = itemList.GroupBy(it => it.ResourceId)
                .Select(item => item.Key)
                .Select(resId => new
                {
                    ResourceId = resId,
                    Resources = itemList.Where(it => it.ResourceId == resId)
                        .OrderBy(it => it.LocaleId)
                })
                .Cast<object>()
                .ToList();

            // final projection
            var result = new
            {
                ResourceSet = resourceSet,
                Locales = totalLocales,
                Resources = resultList
            };

            return Json(result, jsonSerializerSettings);
        }

        [HttpGet("[action]")]
        public IActionResult GetResourceListHtml(string resourceSet)
        {
            var ids = manager.GetAllResourceIdListItems(resourceSet);
            if (ids == null)
                throw new WestwindException("Resource set loading failed: " +
                    manager.ErrorMessage);

            return Json(ids, jsonSerializerSettings);
        }

        /// <summary>
        ///     Returns a list of all ResourceSets
        /// </summary>
        [HttpGet("[action]")]
        public IEnumerable<string> GetResourceSets()
        {
            return manager.GetAllResourceSets(ResourceListingTypes.AllResources);
        }

        /// <summary>
        ///     Checks to see if the localization table exists
        /// </summary>
        [HttpGet("[action]")]
        public bool IsLocalizationTable()
        {
            return manager.IsLocalizationTable();
        }


        /// <summary>
        ///     Returns a list of the all the LocaleIds used in a given resource set
        /// </summary>
        [HttpGet("[action]")]
        public JsonResult GetAllLocaleIds(string resourceSet)
        {
            var ids = manager.GetAllLocaleIds(resourceSet);
            if (ids == null)
                throw new WestwindException("Locale Ids failed to load: " +
                                            manager.ErrorMessage);

            var list = new List<object>();

            foreach (var localeId in ids)
            {
                var ci = CultureInfo.GetCultureInfo(localeId.Trim());

                var language = "Invariant";
                if (!string.IsNullOrEmpty(localeId))
                    language = ci.DisplayName + " (" + ci.Name + ")";
                list.Add(new {LocaleId = localeId, Name = language});
            }

            return Json(list, jsonSerializerSettings);
        }


        /// <summary>
        ///     Returns a resource string based on  resourceId, resourceSet,cultureName
        /// </summary>
        [HttpGet("[action]")]
        public string GetResourceString(string resourceId, string resourceSet, string cultureName)
        {
            var value = manager.GetResourceString(resourceId,
                resourceSet, cultureName);


            if (value == null && !string.IsNullOrEmpty(manager.ErrorMessage))
                throw new ArgumentException(manager.ErrorMessage);

            return value;
        }

        /// <summary>
        ///     Returns all resources for a given Resource ID.
        /// </summary>
        [HttpGet("[action]")]
        public JsonResult GetResourceItems(string resourceId, string resourceSet)
        {
            var items = manager.GetResourceItems(resourceId, resourceSet, true).ToList();
            if (items == null)
                throw new InvalidOperationException(manager.ErrorMessage);

            var itemList = new List<ResourceItemEx>();

            // strip file data for size
            for (var i = 0; i < items.Count; i++)
            {
                var item = new ResourceItemEx(items[i]);
                item.BinFile = null;
                item.TextFile = null;
                itemList.Add(item);
            }

            return Json(itemList, jsonSerializerSettings);
        }

        /// <summary>
        ///     Returns an individual ResourceIdtem for a resource ID and specific culture.
        ///     pass resourceId, resourceSet, cultureName in an object map.
        /// </summary>
        [HttpGet("[action]")]
        public JsonResult GetResourceItem(string resourceId, string resourceSet, string cultureName)
        {
            var item = manager.GetResourceItem(resourceId, resourceSet, cultureName);
            if (item == null)
                throw new ArgumentException(manager.ErrorMessage);

            var itemEx = new ResourceItemEx(item);
            itemEx.ResourceList = GetResourceStringsInternal(resourceId, resourceSet).ToList();

            return Json(itemEx, jsonSerializerSettings);
        }

        /// <summary>
        ///     Gets all resources for a given ResourceId for all cultures from
        ///     a resource set.
        /// </summary>
        /// <param name="resourceId"></param>
        /// <param name="resourceSet"></param>
        /// <returns>Returns an array of Key/Value objects to the client</returns>
        [HttpGet("[action]")]
        public JsonResult GetResourceStrings(string resourceId, string resourceSet)
        {
            var resourceStrings = GetResourceStringsInternal(resourceId, resourceSet);
            return Json(resourceStrings, jsonSerializerSettings);
        }

        private IEnumerable<ResourceString> GetResourceStringsInternal(string resourceId, string resourceSet)
        {
            var resources = manager.GetResourceStrings(resourceId, resourceSet, true);

            if (resources == null)
                throw new WestwindException(manager.ErrorMessage);

            // transform into an array
            var resourceStrings = resources.Select(kv => new ResourceString
            {
                LocaleId = kv.Key,
                Value = kv.Value
            });
            return resourceStrings;
        }


        /// <summary>
        ///     Adds or updates a resource. Pass value, resourceId,resourceSet,localeId,comment
        ///     as a map.
        /// </summary>
        [HttpPost("[action]")]
        public bool UpdateResourceString([FromBody] UpdateResourceStringParams parm)
        {
            var value = parm.Value;
            var resourceId = parm.ResourceId;
            var resourceSet = parm.ResourceSet;
            var localeId = parm.LocaleId;
            var comment = parm.Comment;

            var item = manager.GetResourceItem(resourceId, resourceSet, localeId);
            if (item == null)
                item = new ResourceItem
                {
                    ResourceId = resourceId,
                    LocaleId = localeId,
                    ResourceSet = resourceSet,
                    Comment = comment
                };

            if (string.IsNullOrEmpty(value))
                return manager.DeleteResource(resourceId, resourceSet, localeId);

            item.Value = value;
            item.Type = null;
            item.FileName = null;
            item.BinFile = null;
            item.TextFile = null;

            if (manager.UpdateOrAddResource(item) < 0)
                return false;

            return true;
        }

        /// <summary>
        ///     Updates just a comment for an individual resourceId. Pass resourceId, resourceSet and localeId
        ///     in a map.
        /// </summary>
        [HttpPost("[action]")]
        public bool UpdateComment([FromBody] UpdateCommentParams parm)
        {
            var comment = parm.Comment;
            var resourceId = parm.ResourceId;
            var resourceSet = parm.ResourceSet;
            var localeId = parm.LocaleId;

            var item = manager.GetResourceItem(resourceId, resourceSet, localeId);
            if (item == null)
                return false;
            item.Comment = comment;

            if (manager.UpdateOrAddResource(item) < 0)
                return false;

            return true;
        }

        /// <summary>
        ///     Updates or Adds a resource if it doesn't exist
        /// </summary>
        [HttpPost("[action]")]
        public bool UpdateResource([FromBody] ResourceItem resource)
        {
            if (resource == null)
                throw new ArgumentException("NoResourcePassedToAddOrUpdate");

            if (resource.Value == null)
                return manager.DeleteResource(resource.ResourceId, resource.ResourceSet,
                    resource.LocaleId);

            var result = manager.UpdateOrAddResource(resource);
            if (result == -1)
                throw new InvalidOperationException(manager.ErrorMessage);

            return true;
        }


        /// <summary>
        ///     Updates or adds a binary file resource based on form variables.
        ///     ResourceId,ResourceSet,LocaleId and a single file upload.
        /// </summary>
        [HttpPost("[action]")]
        public bool UploadResource()
        {
            throw new NotImplementedException();

//            if (Request.Files.Count < 1)
//                return false;
//
//            var file = Request.Files[0];
//            var resourceId = Request.Form["ResourceId"];
//            var resourceSet = Request.Form["ResourceSet"];
//            var localeId = Request.Form["LocaleId"];
//
//            if (string.IsNullOrEmpty(resourceId) || string.IsNullOrEmpty(resourceSet))
//                throw new WestwindException("Resourceset or ResourceId are not provided for upload.");
//
//            var item = Manager.GetResourceItem(resourceId, resourceSet, localeId);
//            if (item == null)
//            {
//                item = new ResourceItem()
//                {
//                    ResourceId = resourceId,
//                    ResourceSet = resourceSet,
//                    LocaleId = localeId,
//                    ValueType = (int)ValueTypes.Binary
//                };
//            }
//
//            using (var ms = new MemoryStream())
//            {
//                file.InputStream.CopyTo(ms);
//                file.InputStream.Close();
//                ms.Flush();
//
//                if (DbResourceDataManager.SetFileDataOnResourceItem(item, ms.ToArray(), file.FileName) == null)
//                    return false;
//
//                int res = Manager.UpdateOrAddResource(item);
//            }
//
//            return true;
        }


        /// <summary>
        ///     Delete an individual resource. Pass resourceId, resourceSet and localeId
        ///     as a map. If localeId is null all the resources are deleted.
        /// </summary>
        [HttpPost("[action]")]
        public bool DeleteResource([FromBody] DeleteResourceParams parm)
        {
#if OnlineDemo        
        throw new WestwindException("Feature disabled");
#endif
            if (!manager.DeleteResource(parm.ResourceId, parm.ResourceSet, parm.LocaleId))
                throw new WestwindException("Resource update failed: " +
                                            manager.ErrorMessage);

            return true;
        }

        /// <summary>
        ///     Renames a resource key to a new name.
        /// </summary>
        [HttpPost("[action]")]
        public bool RenameResource([FromBody] RenameResourceParams parms)
        {
#if OnlineDemo
        throw new WestwindException("Feature disabled");
#endif

            if (!manager.RenameResource(parms.ResourceId, parms.NewResourceId, parms.ResourceSet))
                throw new WestwindException("Invalid resource ID");

            return true;
        }

        /// <summary>
        ///     Renames all resource keys that match a property (ie. lblName.Text, lblName.ToolTip)
        ///     at once. This is useful if you decide to rename a meta:resourcekey in the ASP.NET
        ///     markup.
        /// </summary>
        [HttpPost("[action]")]
        public bool RenameResourceProperty([FromBody] RenameResourcePropertyParams input)
        {
            if (!manager.RenameResourceProperty(input.Property, input.NewProperty, input.ResourceSet))
                throw new WestwindException("Invalid resource ID");

            return true;
        }

        [HttpGet("[action]")]
        public string Translate(string text, string from, string to, string service)
        {
            service = service.ToLower();

            var translate = new TranslationServices(configuration);
            translate.TimeoutSeconds = 10;

            string result = null;
            if (service == "google")
                result = translate.TranslateGoogle(text, from, to);
            else if (service == "bing")
                if (string.IsNullOrEmpty(configuration.BingClientId))
                    result = ""; // don't do anything -  just return blank 
                else
                    result = translate.TranslateBing(text, from, to);

            if (result == null)
                result = translate.ErrorMessage;

            return result;
        }


        /// <summary>
        ///     Deletes an entire resource set.
        /// </summary>
        [HttpPost("[action]")]
        public bool DeleteResourceSet(string resourceSet)
        {
#if OnlineDemo
        throw new WestwindException("Feature disabled");
#endif

            if (!manager.DeleteResourceSet(resourceSet))
                throw new WestwindException(manager.ErrorMessage);

            return true;
        }


        /// <summary>
        ///     Renames a resource set to a new name.
        /// </summary>
        [HttpPost("[action]")]
        public bool RenameResourceSet(string oldResourceSet, string newResourceSet)
        {
#if OnlineDemo
        throw new WestwindException("Feature disabled");
#endif
            if (!manager.RenameResourceSet(oldResourceSet, newResourceSet))
                throw new WestwindException(manager.ErrorMessage);

            return true;
        }


        /// <summary>
        ///     Clears the resource cache. Works only if using one of the Westwind
        ///     ASP.NET resource providers or managers.
        /// </summary>
        [HttpPost("[action]")]
        public void ReloadResources()
        {
            //Westwind.Globalization.Tools.wwWebUtils.RestartWebApplication();
            DbResourceConfiguration.ClearResourceCache(); // resource provider
            DbRes.ClearResources(); // resource manager
        }


        /// <summary>
        ///     Backs up the resource table into a new table with the same name + _backup
        /// </summary>
        [HttpPost("[action]")]
        public bool Backup()
        {
#if OnlineDemo
            throw new WestwindException("Feature disabled");
#endif
            return manager.CreateBackupTable(null);
        }


        /// <summary>
        ///     Creates a new localization table as defined int he configuration if it doesn't
        ///     exist. If the table exists an error is returned.
        /// </summary>
        [HttpPost("[action]")]
        public bool CreateTable()
        {
#if OnlineDemo
        throw new WestwindException("Feature disabled");
#endif

            if (!manager.CreateLocalizationTable(null))
                throw new WestwindException("Localization table not created" + Environment.NewLine +
                                            manager.ErrorMessage);
            return true;
        }


        /// <summary>
        ///     Determines whether a locale is an RTL language
        /// </summary>
        [HttpGet("[action]")]
        public bool IsRtl(string localeId)
        {
            try
            {
                var li = localeId;
                if (string.IsNullOrEmpty(localeId))
                    li = CultureInfo.InstalledUICulture.IetfLanguageTag;

                var ci = CultureInfo.GetCultureInfoByIetfLanguageTag(localeId);
                return ci.TextInfo.IsRightToLeft;
            }
            catch
            {
            }

            return false;
        }


        /// <summary>
        ///     Creates .NET strongly typed class from the resources. Pass:
        ///     fileName, namespace, classType, resourceSets as a map.
        /// </summary>
        /// <remarks>
        ///     Requires that the application has rights to write output into
        ///     the location specified by the filename.
        /// </remarks>
        [HttpPost("[action]")]
        public bool CreateClass([FromBody] CreateClassParams parms)
        {
#if OnlineDemo
            throw new WestwindException("Feature disabled");
#endif
//            throw new NotImplementedException();

            // { filename: "~/properties/resources.cs, nameSpace: "WebApp1", resourceSets: ["rs1","rs2"],classType: "DbRes|Resx"]
            var filename = parms.FileName;
            var nameSpace = parms.NameSpace;
            var classType = parms.ClassType;

            var resourceSets = parms.ResourceSets;


            var strongTypes =
                new StronglyTypedResources(hostingEnvironment.ContentRootPath, configuration);

            if (string.IsNullOrEmpty(filename))
                filename = Path.Combine(hostingEnvironment.ContentRootPath, configuration.StronglyTypedGlobalResource);

            else if (filename.StartsWith("~"))
                filename = Path.Combine(hostingEnvironment.ContentRootPath, filename);

            if (string.IsNullOrEmpty(nameSpace))
                nameSpace = configuration.ResourceBaseNamespace;


            if (!string.IsNullOrEmpty(strongTypes.ErrorMessage))
                throw new WestwindException("Strongly typed global resources failed: " + strongTypes.ErrorMessage);

            if (classType != "Resx")
            {
                strongTypes.CreateClassFromAllDatabaseResources(nameSpace, filename, resourceSets);
            }
            else
            {
                var outputBasePath = filename;

                if (resourceSets == null || resourceSets.Length < 1)
                    resourceSets = GetResourceSets().ToArray();

                foreach (var resource in resourceSets)
                {
                    var file = Path.Combine(outputBasePath, resource + ".resx");

                    if (!hostingEnvironment.ContentRootFileProvider.GetFileInfo(file).Exists)
                        continue;

                    var str = new StronglyTypedResources(null, configuration);

                    str.CreateResxDesignerClassFromResxFile(file, resource,
                        configuration.ResourceBaseNamespace, false);
                }
            }

            return true;
        }


        /// <summary>
        ///     Export resources from database to Resx files.
        /// </summary>
        [HttpPost("[action]")]
        public bool ExportResxResources([FromBody] dynamic parms)
        {
#if OnlineDemo
            throw new WestwindException("Feature disabled");
#endif
            // Post:  {outputBasePath: "~\Properties", resourceSets: ["rs1","rs2"] }
            string outputBasePath = parms["outputBasePath"];

            string[] resourceSets = null;
            var t = parms["resourceSets"] as JArray;
            if (t != null)
            {
                resourceSets = t.ToObject<string[]>();
                if (resourceSets != null && resourceSets.Length == 1 && string.IsNullOrEmpty(resourceSets[0]))
                    resourceSets = null;
            }

            if (string.IsNullOrEmpty(outputBasePath))
                outputBasePath = configuration.ResxBaseFolder;
            if (outputBasePath.StartsWith("~"))
                outputBasePath = hostingEnvironment.MapPath(outputBasePath);

            var exporter = new DbResXConverter(configuration, outputBasePath);

            // if resourceSets is null all resources are generated
            if (!exporter.GenerateResXFiles(resourceSets))
                throw new WestwindException("Resource generation failed");

            return true;
        }

        /// <summary>
        ///     Import resources from Resx files into database
        /// </summary>
        [HttpPost("[action]")]
        public bool ImportResxResources(string inputBasePath = null)
        {
#if OnlineDemo
            throw new WestwindException("Feature disabled");
#endif

            if (string.IsNullOrEmpty(inputBasePath))
                inputBasePath = configuration.ResxBaseFolder;

            if (inputBasePath.Contains("~"))
                inputBasePath = hostingEnvironment.MapPath(inputBasePath);

            var converter = new DbResXConverter(configuration, inputBasePath);

            var res = false;
            res = converter.ImportWinResources(inputBasePath);

            if (!res)
                throw new WestwindException("Resource import failed");

            return true;
        }


        /// <summary>
        ///     Returns configuration information so the UI can display this info on the configuration
        ///     page.
        /// </summary>
        [HttpGet("[action]")]
        public JsonResult GetLocalizationInfo()
        {
            return Json(new
            {
                ProviderFactory = "No provider configured",
                configuration.ConnectionString,
                configuration.ResourceTableName,
                configuration.ResourceTableSchema,
                DbResourceProviderType = configuration.DbResourceDataManagerType.Name,
                configuration.ResxExportProjectType,
                configuration.ResxBaseFolder,
                configuration.ResourceBaseNamespace,
                configuration.StronglyTypedGlobalResource,
                configuration.GoogleApiKey,
                configuration.BingClientId,
                configuration.BingClientSecret,
                configuration.AddMissingResources
            }, jsonSerializerSettings);
        }

        [HttpGet("~/[controller]/[action]")]
        public IActionResult JavaScriptResources(JavaScriptResourcesParams args)
        {
            var javascript = javaScriptResourceHandler.GetJavaScript(args.VarName, args.ResourceSet, args.LocaleId,
                args.ResourceType, args.ResourceMode);
            return Content(javascript, "text/javascript", Encoding.UTF8);
        }

        public class DeleteResourceParams
        {
            public string ResourceId { get; set; }
            public string ResourceSet { get; set; }
            public string LocaleId { get; set; }
        }

        public class CreateClassParams
        {
            public string FileName { get; set; }
            public string NameSpace { get; set; }
            public string ClassType { get; set; }
            public string[] ResourceSets { get; set; }
        }

        public class UpdateCommentParams
        {
            public string ResourceId { get; set; }
            public string ResourceSet { get; set; }
            public string LocaleId { get; set; }
            public string Comment { get; set; }
        }

        public class UpdateResourceStringParams
        {
            public string Value { get; set; }
            public string ResourceId { get; set; }
            public string ResourceSet { get; set; }
            public string LocaleId { get; set; }
            public string Comment { get; set; }
        }

        public class JavaScriptResourcesParams
        {
            public string VarName { get; set; }
            public string ResourceSet { get; set; }
            public string LocaleId { get; set; }
            public string ResourceType { get; set; }
            public string ResourceMode { get; set; }
        }

        public class RenameResourcePropertyParams
        {
            public string ResourceSet { get; set; }
            public string Property { get; set; }
            public string NewProperty { get; set; }
        }

        public class RenameResourceParams
        {
            public string ResourceSet { get; set; }
            public string NewResourceId { get; set; }
            public string ResourceId { get; set; }
        }
    }
}