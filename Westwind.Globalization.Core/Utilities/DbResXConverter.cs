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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading;
using System.Xml;
using Westwind.Globalization.Core.DbResourceSupportClasses;
using Westwind.Utilities;

namespace Westwind.Globalization.Core.Utilities
{
    /// <summary>
    ///     This class can be used to export resources from the database to ASP.NET
    ///     compatible resources (Resx). This class takes all the resources in
    ///     the database and creates RESX files that match these resources.
    ///     Please note that it will overrwrite any existing resource files
    ///     if they already exist, so please use this class with care if
    ///     you have existing ResX resources.
    ///     Note this class is primarily ASP.NET specific in that it looks at
    ///     ASP.NET specific directory structures for ResX imports and strongly
    ///     typed resource creation.
    /// </summary>
    public class DbResXConverter
    {
        public const string ResXDocumentTemplate =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<root>
  <xsd:schema id=""root"" xmlns="""" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata"">
    <xsd:import namespace=""http://www.w3.org/XML/1998/namespace"" />
    <xsd:element name=""root"" msdata:IsDataSet=""true"">
      <xsd:complexType>
        <xsd:choice maxOccurs=""unbounded"">
          <xsd:element name=""metadata"">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" />
              </xsd:sequence>
              <xsd:attribute name=""name"" use=""required"" type=""xsd:string"" />
              <xsd:attribute name=""type"" type=""xsd:string"" />
              <xsd:attribute name=""mimetype"" type=""xsd:string"" />
              <xsd:attribute ref=""xml:space"" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name=""assembly"">
            <xsd:complexType>
              <xsd:attribute name=""alias"" type=""xsd:string"" />
              <xsd:attribute name=""name"" type=""xsd:string"" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name=""data"">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""1"" />
                <xsd:element name=""comment"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""2"" />
              </xsd:sequence>
              <xsd:attribute name=""name"" type=""xsd:string"" use=""required"" msdata:Ordinal=""1"" />
              <xsd:attribute name=""type"" type=""xsd:string"" msdata:Ordinal=""3"" />
              <xsd:attribute name=""mimetype"" type=""xsd:string"" msdata:Ordinal=""4"" />
              <xsd:attribute ref=""xml:space"" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name=""resheader"">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""1"" />
              </xsd:sequence>
              <xsd:attribute name=""name"" type=""xsd:string"" use=""required"" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name=""resmimetype"">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name=""version"">
    <value>2.0</value>
  </resheader>
  <resheader name=""reader"">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name=""writer"">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
</root>";

        private readonly DbResourceConfiguration configuration;

        /// <summary>
        ///     Pass in the base physical path for the project.
        ///     For Web Projects this will be the Web root dir for
        ///     non-Web projects this will be the project base path
        ///     as a string.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="basePhysicalPath">
        ///     Optional - allows specifying the virtual path where the resources are loaded and saved to.
        ///     If not specified AppDomain.CurrentDomain.BaseDirectory is used instead.
        /// </param>
        public DbResXConverter(DbResourceConfiguration configuration, string basePhysicalPath = "")
        {
            this.configuration = configuration;
            if (string.IsNullOrEmpty(basePhysicalPath))
                basePhysicalPath = AppDomain.CurrentDomain.BaseDirectory;

            BasePhysicalPath = basePhysicalPath;
        }

        /// <summary>
        ///     The physical path of the Web application. This path serves as
        ///     the root path to write resources to.
        ///     Example: c:\projects\MyWebApp
        /// </summary>
        public string BasePhysicalPath { get; set; }


        /// <summary>
        ///     Error message if an operation fails
        /// </summary>
        public string ErrorMessage { get; set; } = "";

        /// <summary>
        ///     Generates Resx Files for standard non-Web Resource files
        ///     based on the BasePhysicalPath
        /// </summary>
        public bool GenerateResXFiles(IEnumerable<string> resourceSets = null,
            bool generateStronglyTypedClasses = false)
        {
            var data = DbResourceDataManager.DbResourceDataManager.CreateDbResourceDataManager(configuration);

            // Retrieve all resources for a ResourceSet for all cultures
            // The data is ordered by ResourceSet, LocaleId and resource ID as each
            // ResourceSet or Locale changes a new file is written
            var resources = data.GetAllResources(applyValueConverters: true);

            if (resourceSets != null)
                resources = resources.Where(rs => resourceSets.Any(rs1 => rs1 == rs.ResourceSet))
                    .ToList();

            if (resources == null)
                return false;

            var lastSet = "";
            var lastLocale = "@!";

            //// Load the document schema
            var doc = new XmlDocument();
            doc.LoadXml(ResXDocumentTemplate);

            XmlWriter xWriter = null;
            var xmlSettings = new XmlWriterSettings();

            //// Make sure we use fragment syntax so there's no validation
            //// otherwise loading the original string will fail
            xmlSettings.ConformanceLevel = ConformanceLevel.Document;
            xmlSettings.IndentChars = "   ";
            xmlSettings.Indent = true;

            foreach (var res in resources)
            {
                res.LocaleId = res.LocaleId.ToLower();
                var stringValue = res.Value as string;

                // Create a new output file if the resource set or locale changes
                if (res.ResourceSet != lastSet || res.LocaleId != lastLocale)
                {
                    if (xWriter != null)
                    {
                        xWriter.WriteEndElement();
                        xWriter.Close();
                    }

                    var localizedExtension = ".resx";
                    if (res.LocaleId != "")
                        localizedExtension = "." + res.LocaleId + ".resx";

                    var fullFileName = FormatResourceSetPath(res.ResourceSet) + localizedExtension;

                    var writer = new XmlTextWriter(fullFileName, Encoding.UTF8);
                    writer.Indentation = 3;
                    writer.IndentChar = ' ';
                    writer.Formatting = Formatting.Indented;
                    xWriter = writer;

                    xWriter.WriteProcessingInstruction("xml", "version='1.0' encoding='UTF-8'");
                    xWriter.WriteStartElement("root");

                    // Write out the schema
                    doc.DocumentElement.ChildNodes[0].WriteTo(xWriter);

                    // Write out the leading resheader elements
                    var Nodes = doc.DocumentElement.SelectNodes("resheader");
                    foreach (XmlNode Node in Nodes)
                        Node.WriteTo(xWriter);

                    lastSet = res.ResourceSet;
                    lastLocale = res.LocaleId;
                }

                if (string.IsNullOrEmpty(res.Type)) // plain string value
                {
                    //<data name="LinkButton1Resource1.Text" xml:space="preserve">
                    //    <value>LinkButton</value>
                    //</data>
                    xWriter.WriteStartElement("data");
                    xWriter.WriteAttributeString("name", res.ResourceId);
                    xWriter.WriteAttributeString("xml", "space", null, "preserve");
                    xWriter.WriteElementString("value", stringValue);
                    if (!string.IsNullOrEmpty(res.Comment))
                        xWriter.WriteElementString("comment", res.Comment);
                    xWriter.WriteEndElement(); // data
                }
                // File Resources get written to disk
                else if (res.Type == "FileResource")
                {
                    var ResourceFilePath = FormatResourceSetPath(res.ResourceSet);
                    var ResourcePath = new FileInfo(ResourceFilePath).DirectoryName;

                    if (stringValue.IndexOf("System.String") > -1)
                    {
                        var Tokens = stringValue.Split(';');
                        var Encode = Encoding.Default;
                        try
                        {
                            if (Tokens.Length == 3)
                                Encode = Encoding.GetEncoding(Tokens[2]);

                            // Write out the file to disk
                            var file = Path.Combine(ResourcePath, res.FileName);
                            File.Delete(file);
                            File.WriteAllText(file, res.TextFile, Encode);
                        }
                        catch
                        {
                        }
                    }
                    else
                    {
                        var file = Path.Combine(ResourcePath, res.FileName);
                        File.Delete(file); // overwrite doesn't appear to work so explicitly delete
                        File.WriteAllBytes(file, res.BinFile);
                    }

                    //<data name="Scratch" type="System.Resources.ResXFileRef, System.Windows.Forms">
                    //  <value>Scratch.txt;System.String, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089;Windows-1252</value>
                    //</data>
                    xWriter.WriteStartElement("data");
                    xWriter.WriteAttributeString("name", res.ResourceId);
                    xWriter.WriteAttributeString("type", "System.Resources.ResXFileRef, System.Windows.Forms");

                    // values are already formatted in the database
                    xWriter.WriteElementString("value", stringValue);
                    if (!string.IsNullOrEmpty(res.Comment))
                        xWriter.WriteElementString("comment", res.Comment);

                    xWriter.WriteEndElement(); // data
                }
            } // foreach dr

            if (xWriter != null)
            {
                xWriter.WriteEndElement();
                //xWriter.WriteRaw("\r\n</root>");
                xWriter.Close();
            }

            return true;
        }

        /// <summary>
        ///     Returns the path the resource file withouth the resx and localeId extension
        /// </summary>
        public string FormatResourceSetPath(string resourceSet)
        {
            string path;

            if (string.IsNullOrEmpty(BasePhysicalPath) &&
                configuration.ResxExportProjectType == GlobalizationResxExportProjectTypes.Project)
            {
                path = configuration.ResxBaseFolder;
                if (path.StartsWith("~"))
                    path = path.Replace("~", BasePhysicalPath);
            }
            else
            {
                path = BasePhysicalPath;
            }

            resourceSet = Path.Combine(path, resourceSet);

            var fi = new FileInfo(resourceSet);
            if (!fi.Directory.Exists)
                fi.Directory.Create();

            return resourceSet;
        }

        /// <summary>
        ///     Imports Resources recursively from a non-Web project
        /// </summary>
        /// <param name="basePhysicalPath">The physical path to the directory</param>
        /// <returns></returns>
        public bool ImportWinResources(string basePhysicalPath)
        {
            if (basePhysicalPath == null)
                basePhysicalPath = BasePhysicalPath;

            // basePhysicalPath = basePhysicalPath.ToLower();
            basePhysicalPath = basePhysicalPath.EnsureEndsWithDirectorySeparator();

            // We need to create a Web relative path (ie. admin/myresources.resx)
            var relPath = basePhysicalPath.Replace(BasePhysicalPath, "");

            // Import the base path first
            ImportDirectoryResources(basePhysicalPath, relPath);

            // Recurse into child folders
            string[] directories;
            try
            {
                directories = Directory.GetDirectories(basePhysicalPath);
            }
            catch
            {
                return false;
            }

            foreach (var dirString in directories)
            {
                var directory = new DirectoryInfo(dirString);

                var dir = directory.Name;

                if (dir == "" ||
                    "|bin|obj|.git|.svn|_svn|app_code|app_themes|app_data|migrations|node_modules|bower_components|"
                        .Contains("|" + dir.ToLower() + "|"))
                    continue;

                ImportWinResources(Path.Combine(basePhysicalPath, dir));
            }

            return true;
        }

        /// <summary>
        ///     Imports all resources from a given directory. This method works for any resources.
        ///     When using LocalResources, make sure to provide an app relative path as the second
        ///     parameter if the resources live in non root folder. So if you have resources in off
        ///     an Admin folder use "admin/" as the parameter. Otherwise for web root resources or
        ///     global or assembly level assemblies pass string.Empty or null.
        /// </summary>
        /// <param name="path">Physical Path for the Resources</param>
        /// <param name="relativePath">Optional - relative path prefix for Web App_LocalResources (ie. admin/)</param>
        /// <returns></returns>
        public bool ImportDirectoryResources(string path, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                relativePath = "";

            var Files = Directory.GetFiles(path, "*.resx");

            foreach (var CurFile in Files)
            {
                var file = CurFile; //.ToLower();

                //string[] tokens = file.Replace(".resx","").Split('.');                
                var tokens = Path.GetFileName(file).Replace(".resx", "").Split('.');


                // ResName: admin/default.aspx or default.aspx or resources (global or assembly resources)
                var localeId = "";
                var resName = relativePath + Path.GetFileNameWithoutExtension(tokens[0]);


                if (resName.Contains("App_LocalResources/"))
                    resName = resName.Replace("App_LocalResources/", "");
                else if (resName.Contains("App_GlobalResources/"))
                    resName = resName.Replace("App_GlobalResources/", "");


                if (tokens.Length > 1)
                {
                    var extension = tokens[1];
                    if ("aspx|ascx|master|sitemap|".Contains(extension.ToLower() + "|"))
                        resName += "." + extension;
                    else
                        localeId = extension;
                }
                if (tokens.Length > 2)
                    localeId = tokens[2];

                ImportResourceFile(file, resName, localeId);
            }

            return true;
        }

        /// <summary>
        ///     Imports an individual ResX Resource file into the database
        /// </summary>
        /// <param name="fileName">Full path to the the ResX file</param>
        /// <param name="resourceSetName">
        ///     Name of the file or for local resources the app relative path plus filename
        ///     (admin/default.aspx or default.aspx)
        /// </param>
        /// <param name="localeId">Locale Id of the file to import. Use "" for Invariant</param>
        /// <returns></returns>
        public bool ImportResourceFile(string fileName, string resourceSetName, string localeId)
        {
            var filePath = Path.GetDirectoryName(fileName).EnsureEndsWithDirectorySeparator();

            var data = DbResourceDataManager.DbResourceDataManager.CreateDbResourceDataManager(configuration);

            // clear out resources first
            data.DeleteResourceSet(resourceSetName, localeId);

            var dom = new XmlDocument();

            try
            {
                dom.Load(fileName);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return false;
            }

            var nodes = dom.DocumentElement.SelectNodes("data");

            foreach (XmlNode Node in nodes)
            {
                string Value; // = Node.ChildNodes[0].InnerText;

                var valueNodes = Node.SelectNodes("value");
                if (valueNodes.Count == 1)
                    Value = valueNodes[0].InnerText;
                else
                    Value = Node.InnerText;

                var Name = Node.Attributes["name"].Value;
                string Type = null;
                if (Node.Attributes["type"] != null)
                    Type = Node.Attributes["type"].Value;

                string Comment = null;
                var commentNode = Node.SelectSingleNode("comment");
                if (commentNode != null)
                    Comment = commentNode.InnerText;


                if (string.IsNullOrEmpty(Type))
                {
                    if (data.UpdateOrAddResource(Name, Value, localeId, resourceSetName, Comment) == -1)
                    {
                        ErrorMessage = data.ErrorMessage;
                        return false;
                    }
                }
                else
                {
                    // File based resources are formatted: filename;full type name
                    var tokens = Value.Split(';');
                    if (tokens.Length > 0)
                    {
                        var ResFileName = filePath + tokens[0];
                        if (File.Exists(ResFileName))
                            // DataManager knows about file resources and can figure type info
                            if (data.UpdateOrAddResource(Name, ResFileName, localeId, resourceSetName, Comment, true) ==
                                -1)
                            {
                                ErrorMessage = data.ErrorMessage;
                                return false;
                            }
                    }
                }
            }

            return true;
        }

        /// <summary>
        ///     Gets a specific List of resources as a list of ResxItems.
        ///     This list only retrieves items for a specific locale. No
        ///     resource normalization occurs.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        internal List<ResxItem> GetResXResources(string fileName)
        {
            var filePath = Path.GetDirectoryName(fileName).EnsureEndsWithDirectorySeparator();

            var dom = new XmlDocument();

            try
            {
                dom.Load(fileName);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return null;
            }

            var resxItems = new List<ResxItem>();

            var nodes = dom.DocumentElement.SelectNodes("data");

            foreach (XmlNode Node in nodes)
            {
                string Value = null;

                var valueNodes = Node.SelectNodes("value");
                if (valueNodes.Count == 1)
                    Value = valueNodes[0].InnerText;
                else
                    Value = Node.InnerText;

                var Name = Node.Attributes["name"].Value;
                string Type = null;
                if (Node.Attributes["type"] != null)
                    Type = Node.Attributes["type"].Value;

                var resxItem = new ResxItem {Name = Name, Type = Type, Value = Value};
                resxItems.Add(resxItem);
            }
            return resxItems;
        }

        /// <summary>
        ///     Returns all resources for a given locale normalized down the hierarchy for
        ///     a given resource file. The resource file should be specified without the
        ///     .resx and locale identifier extensions.
        /// </summary>
        /// <param name="baseFile">The base Resource file without .resx and locale extensions</param>
        /// <param name="LocaleId"></param>
        /// <returns>Dictionary of resource keys and values</returns>
        public Dictionary<string, object> GetResXResourcesNormalizedForLocale(string baseFile, string LocaleId)
        {
            string LocaleId1 = null;
            if (LocaleId.Contains('-'))
                LocaleId1 = LocaleId.Split('-')[0];

            var localeRes = new List<ResxItem>();
            var locale1Res = new List<ResxItem>();
            List<ResxItem> invariantRes = null;

            if (!string.IsNullOrEmpty(LocaleId))
            {
                localeRes = GetResXResources(baseFile + "." + LocaleId + ".resx");
                if (localeRes == null)
                    localeRes = new List<ResxItem>();
            }
            if (!string.IsNullOrEmpty(LocaleId1))
            {
                locale1Res = GetResXResources(baseFile + "." + LocaleId1 + ".resx");
                if (locale1Res == null)
                    locale1Res = new List<ResxItem>();
            }

            invariantRes = GetResXResources(baseFile + ".resx");
            if (invariantRes == null)
                invariantRes = new List<ResxItem>();

            var items =
                from loc in localeRes
                    .Concat(from loc1 in locale1Res select loc1)
                    .Concat(from invariant in invariantRes select invariant)
                    .OrderBy(loc => loc.Name)
                select loc;

            var resxDict = new Dictionary<string, object>();
            var lastName = "@#XX";
            foreach (var item in items)
            {
                if (lastName == item.Name)
                    continue;
                lastName = item.Name;

                resxDict.Add(item.Name, item.Value);
            }

            return resxDict;
        }

        /// <summary>
        ///     Returns resources for a given resource set in a specific locale
        /// </summary>
        /// <param name="resourceSet"></param>
        /// <param name="baseNamespace"></param>
        /// <param name="localeId"></param>
        /// <returns></returns>
        public Dictionary<string, object> GetCompiledResourcesNormalizedForLocale(string resourceSet,
            string baseNamespace, string localeId)
        {
            if (string.IsNullOrEmpty(baseNamespace))
                baseNamespace = configuration.ResourceBaseNamespace;

            var resourceSetName = baseNamespace + "." + resourceSet.Replace("/", ".").Replace("\\", ".");
            var type = ReflectionUtils.GetTypeFromName(resourceSetName);
            if (type == null)
                return null;

            var resMan = new ResourceManager(resourceSetName, type.Assembly);
            if (resMan == null)
                return null;

            return GetResourcesNormalizedForLocale(resMan, localeId);
        }


        public Dictionary<string, object> GetResourcesNormalizedForLocale(ResourceManager resourceManager,
            string localeId)
        {
            var resDict = new Dictionary<string, object>();

            var culture = Thread.CurrentThread.CurrentUICulture;
            if (localeId == null)
                culture = CultureInfo.CurrentUICulture;
            else if (localeId == string.Empty)
                culture = CultureInfo.InvariantCulture;
            else if (culture.IetfLanguageTag != localeId)
                culture = CultureInfo.GetCultureInfoByIetfLanguageTag(localeId);

            try
            {
                IDictionaryEnumerator enumerator;
                var resSet = resourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true);
                enumerator = resSet.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var resItem = (DictionaryEntry) enumerator.Current;
                    resDict.Add((string) resItem.Key, null);
                }
                var keys = resDict.Keys.ToList();
                foreach (var key in keys)
                    resDict[key] = resourceManager.GetObject(key, culture);
            }
            catch (Exception)
            {
                return null;
            }

            return resDict;
        }
    }

    [DebuggerDisplay(@" Name: {Name}, Value={Value} ")]
    public class ResxItem
    {
        public string LocaleId = string.Empty;
        public string Name = string.Empty;
        public string Type = string.Empty;
        public object Value;
    }

    public enum wwResourceExportLanguages
    {
        CSharp,
        VB
    }
}