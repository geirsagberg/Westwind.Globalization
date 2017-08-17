#region License

/*
 **************************************************************
 *  Author: Rick Strahl 
 *          © West Wind Technologies, 2008 - 2009
 *          http://www.west-wind.com/
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
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Westwind.Globalization.Core.DbResourceManager;
using Westwind.Globalization.Core.DbResourceSupportClasses;
using Westwind.Globalization.Core.Utilities;
using Westwind.Globalization.Core.Web.Extensions;
using Westwind.Utilities;

namespace Westwind.Globalization.Core.Web.Utilities
{
    /// <summary>
    ///     Http Handler that returns ASP.NET Local and Global Resources as JavaScript
    ///     objects. Supports both plain Resx Resources as well as DbResourceProvider
    ///     driven resources.
    ///     Objects are generated in the form of:
    ///     &lt;&lt;code lang="JavaScript"&gt;&gt;var localRes  = {
    ///     BackupFailed: "Backup was not completed",
    ///     Loading: "Loading"
    ///     );&lt;&lt;/code&gt;&gt;
    ///     where the resource key becomes the property name with a string value.
    ///     The handler is driven through query string variables determines which
    ///     resources are returned:
    ///     ResourceSet      -  Examples: "resources" (global), "admin/somepage.aspx" "default.aspx" (local)
    ///     LocaleId         -  Examples: "de-de","de",""  (empty=invariant)
    ///     ResourceType     -  Resx,ResDb
    ///     IncludeControls  -  if non-blank includes control values (. in name)
    ///     VarName          -  name of hte variable generated - if omitted localRes or globalRes is created.
    ///     ResourceMode -  Flag required to find Resx resources on disk 0 - Local 1 - global 2 - plain resx
    ///     Resources retrieved are aggregated for the locale Id (ie. de-de returns
    ///     de-de,de and invariant) whichever matches first.
    /// </summary>
    public class JavaScriptResourceHandler
    {
        private readonly DbResourceConfiguration configuration;
        private readonly IHostingEnvironment hostingEnvironment;

        public JavaScriptResourceHandler(IHostingEnvironment hostingEnvironment, DbResourceConfiguration configuration)
        {
            this.hostingEnvironment = hostingEnvironment;
            this.configuration = configuration;
        }

        public string GetJavaScript(string varname, string resourceSet, string localeId,
            string resourceType, string resourceMode)
        {
            // varname is embedded into script so validate to avoid script injection
            // it's gotta be a valid C# and valid JavaScript name
            var match = Regex.Match(varname, @"^[\w|\d|_|$|@|\.]*$");
            if (match.Length < 1 || match.Groups[0].Value != varname)
                throw new WestwindException("Invalid variable name passed.");

            if (string.IsNullOrEmpty(resourceSet))
                throw new WestwindException("Invalid ResourceSet specified.");

            // pick current UI Culture
            if (localeId == "auto")
                localeId = Thread.CurrentThread.CurrentUICulture.IetfLanguageTag;

            Dictionary<string, object> resDict = null;

            if (string.IsNullOrEmpty(resourceType) || resourceType == "auto")
		resourceType = "resdb";

            var basePath = hostingEnvironment.MapPath(configuration.ResxBaseFolder);
            var converter = new DbResXConverter(configuration, basePath);

            if (resourceType.ToLower() == "resdb")
            {
                // use existing/cached resource manager if previously used
                // so database is accessed only on first hit
                var resManager = DbRes.GetResourceManager(resourceSet);

                resDict = converter.GetResourcesNormalizedForLocale(resManager, localeId);

                //resDict = manager.GetResourceSetNormalizedForLocaleId(localeId, resourceSet);
                if (resDict == null || resDict.Keys.Count < 1)
                {
                    // try resx instead
                    var resxPath = converter.FormatResourceSetPath(resourceSet);
                    resDict = converter.GetResXResourcesNormalizedForLocale(resxPath, localeId);
                }
            }
            else // Resx Resources
            {
                resDict = converter.GetCompiledResourcesNormalizedForLocale(resourceSet,
                    configuration.ResourceBaseNamespace,
                    localeId);

                if (resDict == null)
                {
                    // check for .resx disk resources
                    var resxPath = converter.FormatResourceSetPath(resourceSet);
                    resDict = converter.GetResXResourcesNormalizedForLocale(resxPath, localeId);
                }
                else
                {
                    resDict = resDict.OrderBy(kv => kv.Key).ToDictionary(k => k.Key, v => v.Value);
                }
            }


            if (resourceMode == "0")
                resDict = resDict.Where(res => !res.Key.Contains('.') && res.Value is string)
                    .ToDictionary(dict => dict.Key, dict => dict.Value);
            else
                resDict = resDict.Where(res => res.Value is string)
                    .ToDictionary(dict => dict.Key, dict => dict.Value);

            var javaScript = SerializeResourceDictionary(resDict, varname);
            return javaScript;
        }

        public Dictionary<string, object> GetResourceSetFromCompiledResources(string resourceSet, string baseNamespace)
        {
            if (string.IsNullOrEmpty(baseNamespace))
                baseNamespace = configuration.ResourceBaseNamespace;

            var resourceSetName = baseNamespace + "." + resourceSet.Replace("/", ".").Replace("\\", ".");
            var type = ReflectionUtils.GetTypeFromName(resourceSetName);
            var resMan = new ResourceManager(resourceSetName, type.Assembly);
            var resDict = new Dictionary<string, object>();

            try
            {
                IDictionaryEnumerator enumerator;
                using (var resSet = resMan.GetResourceSet(Thread.CurrentThread.CurrentUICulture, true, true))
                {
                    enumerator = resSet.GetEnumerator();

                    while (enumerator.MoveNext())
                    {
                        var resItem = (DictionaryEntry) enumerator.Current;
                        resDict.Add((string) resItem.Key, resItem.Value);
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }

            return resDict;
        }

        /// <summary>
        ///     Generates the actual JavaScript object map string makes up the
        ///     handler's result content.
        /// </summary>
        /// <param name="resxDict"></param>
        /// <param name="varname"></param>
        /// <returns></returns>
        private static string SerializeResourceDictionary(Dictionary<string, object> resxDict, string varname)
        {
            var sb = new StringBuilder(2048);

            sb.Append(varname + " = {\r\n");

            var anonymousIdCounter = 0;
            foreach (var item in resxDict)
            {
                var value = item.Value as string;
                if (value == null)
                    continue; // only encode string values

                var key = item.Key;
                if (string.IsNullOrEmpty(item.Key))
                    key = "__id" + anonymousIdCounter++;

                key = key.Replace(".", "_");
                if (key.Contains(" "))
                    key = StringUtils.ToCamelCase(key);

                sb.Append("\t\"" + key + "\": ");
                sb.Append(EncodeJsString(value));
                sb.Append(",\r\n");
            }

            // add dbRes function
            sb.AppendFormat(
                "\t" + @"""dbRes"": function dbRes(resId) {{ return {0}[resId] || resId; }}      
}}
", varname);


            return sb.ToString();
        }

        /// <summary>
        ///     Encodes a string to be represented as a string literal. The format
        ///     is essentially a JSON string that is returned in double quotes.
        ///     The string returned includes outer quotes:
        ///     "Hello \"Rick\"!\r\nRock on"
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string EncodeJsString(string s)
        {
            if (s == null)
                return "null";

            var sb = new StringBuilder();
            sb.Append("\"");
            foreach (var c in s)
                switch (c)
                {
                    case '\"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        int i = c;
                        if (i < 32 || c == '<' || c == '>')
                            sb.AppendFormat("\\u{0:X04}", i);
                        else
                            sb.Append(c);
                        break;
                }
            sb.Append("\"");


            return sb.ToString();
        }

        /// <summary>
        ///     Returns a URL to the JavaScriptResourceHandler.axd handler that retrieves
        ///     normalized resources for a given resource set and localeId and creates
        ///     a JavaScript object with the name specified.
        ///     This function returns only the URL - you're responsible for embedding
        ///     the URL into the page as a script tag to actually load the resources.
        /// </summary>
        /// <param name="varName"></param>
        /// <param name="resourceSet"></param>
        /// <param name="localeId"></param>
        /// <param name="resourceType"></param>
        /// <returns></returns>
        public static string GetJavaScriptGlobalResourcesUrl(string varName, string resourceSet, string localeId = null,
            ResourceProviderTypes resourceType = ResourceProviderTypes.AutoDetect)
        {
            if (resourceType == ResourceProviderTypes.AutoDetect)
		resourceType = ResourceProviderTypes.DbResourceProvider;


            var sb = new StringBuilder(512);
            sb.Append("/JavaScriptResourceHandler.axd?");
            sb.AppendFormat("ResourceSet={0}&LocaleId={1}&VarName={2}&ResourceType={3}",
                resourceSet, localeId, varName,
                resourceType == ResourceProviderTypes.DbResourceProvider ? "resdb" : "resx");
            sb.Append("&ResourceMode=1");

            return sb.ToString();
        }


        /// <summary>
        ///     Returns a URL to the JavaScriptResourceHandler.axd handler that retrieves
        ///     normalized resources for a given resource set and localeId and creates
        ///     a JavaScript object with the name specified.
        ///     This version assumes the current UI Culture and auto-detects the
        ///     provider type (Resx or DbRes) currently active.
        /// </summary>
        /// <param name="varName"></param>
        /// <param name="resourceSet"></param>
        /// <returns></returns>
        public static string GetJavaScriptGlobalResourcesUrl(string varName, string resourceSet)
        {
            var localeId = CultureInfo.CurrentUICulture.IetfLanguageTag;
            return GetJavaScriptGlobalResourcesUrl(varName, resourceSet, localeId, ResourceProviderTypes.AutoDetect);
        }

        /// <summary>
        ///     Inserts local resources into the current page.
        /// </summary>
        /// <param name="control">A control (typically) page needed to embed into the page</param>
        /// <param name="resourceSet">Name of the resourceSet to load</param>
        /// <param name="localeId">The Locale for which to load resources. Normalized from most specific to Invariant</param>
        /// <param name="varName">Name of the variable generated</param>
        /// <param name="resourceType">Resx or DbResourceProvider (database)</param>
        /// <param name="includeControls">Determines whether control ids are included</param>
        public static string GetJavaScriptLocalResourcesUrl(string varName, string localeId, string resourceSet,
            ResourceProviderTypes resourceType, bool includeControls)
        {
            if (resourceType == ResourceProviderTypes.AutoDetect)
		resourceType = ResourceProviderTypes.DbResourceProvider;

            var sb = new StringBuilder(512);

            sb.Append("/JavaScriptResourceHandler.axd?");
            sb.AppendFormat("ResourceSet={0}&LocaleId={1}&VarName={2}&ResourceType={3}&ResourceMode=0",
                resourceSet, localeId, varName,
                resourceType == ResourceProviderTypes.DbResourceProvider ? "resdb" : "resx");
            if (includeControls)
                sb.Append("&IncludeControls=1");

            return sb.ToString();
        }


        /// <summary>
        ///     Returns a standard Resx resource based on it's . delimited resourceset name
        /// </summary>
        /// <param name="varName">The name of the JavaScript variable to create</param>
        /// <param name="resourceSet">
        ///     The name of the resource set
        ///     Example:
        ///     CodePasteMvc.Resources.Resources  (~/Resources/Resources.resx in CodePasteMvc project)
        /// </param>
        /// <param name="localeId">IETF locale id (2 or 4 en or en-US or empty)</param>
        /// <param name="resourceType">ResDb or ResX</param>
        /// <returns></returns>
        public static string GetJavaScriptResourcesUrl(string varName, string resourceSet,
            string localeId = null,
            ResourceProviderTypes resourceType = ResourceProviderTypes.AutoDetect)
        {
            if (localeId == null)
                localeId = CultureInfo.CurrentUICulture.IetfLanguageTag;

            if (resourceType == ResourceProviderTypes.AutoDetect)
		resourceType = ResourceProviderTypes.DbResourceProvider;

            var sb = new StringBuilder(512);
            sb.Append("/JavaScriptResourceHandler.axd?");
            sb.AppendFormat("ResourceSet={0}&LocaleId={1}&VarName={2}&ResourceType={3}",
                resourceSet, localeId, varName,
                resourceType == ResourceProviderTypes.DbResourceProvider ? "resdb" : "resx");
            sb.Append("&ResourceMode=1");

            return sb.ToString();
        }
    }


    /// <summary>
    ///     Determines the resource provider type used
    ///     to retrieve resources.
    ///     Note only applies to the stock ResX provider
    ///     or the DbResourceProviders of this assembly.
    ///     Other custom resource providers are not supported.
    /// </summary>
    public enum ResourceProviderTypes
    {
        Resx,
        DbResourceProvider,
        AutoDetect
    }
}