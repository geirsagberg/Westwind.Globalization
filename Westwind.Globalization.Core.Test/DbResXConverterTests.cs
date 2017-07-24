using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Westwind.Globalization.Core.DbResourceDataManager;
using Westwind.Globalization.Core.DbResourceDataManager.DbResourceDataManagers;
using Westwind.Globalization.Core.DbResourceSupportClasses;
using Westwind.Globalization.Core.Utilities;
using Xunit;

namespace Westwind.Globalization.Test
{
    public class DbResXConverterTests
    {
	private DbResourceConfiguration configuration;


        public DbResXConverterTests()
        {
            //DbResourceConfiguration.Current.ConnectionString = "SqLiteLocalizations";
            //DbResourceConfiguration.Current.DbResourceDataManagerType = typeof (DbResourceSqLiteDataManager);
	    configuration = new DbResourceConfiguration
	    {
		ConnectionString = "SqLiteLocalizations",
		DbResourceDataManagerType = typeof(DbResourceSqLiteDataManager)
	    };
        }

        /// <summary>
        ///  convert Resx file to a resource dictionary
        /// </summary>
	[Fact]
        public void GetResXResourcesTest()
        {
            string path = @"c:\temp\resources";
	    DbResXConverter converter = new DbResXConverter(configuration, path);
            Dictionary<string, object> items = converter.GetResXResourcesNormalizedForLocale(@"C:\Temp\Westwind.Globalizations\Westwind.Globalization.Sample\LocalizationAdmin\App_LocalResources\LocalizationAdmin.aspx", "de-de");
            WriteResourceDictionary(items,"ResX Resources");
        }

	[Fact]
        public void GetDbResourcesTest()
        {
            // create manager based on configuration
	    var manager = DbResourceDataManager.CreateDbResourceDataManager(configuration);

            Dictionary<string,object> items = manager.GetResourceSetNormalizedForLocaleId("de-de", "Resources");

            WriteResourceDictionary(items, "DB Resources");            
        }

	[Fact]
        public void WriteResxFromDbResources()
        {
	    DbResXConverter converter = new DbResXConverter(configuration, @"c:\temp\resources");
	    Assert.True(converter.GenerateResXFiles(), converter.ErrorMessage);
        }

	[Fact]
        public void ImportResxResources()
        {
            bool result = false;
            //var manager = Activator.CreateInstance(DbResourceConfiguration.Current.DbResourceDataManagerType) as IDbResourceDataManager;
            //result = manager.CreateLocalizationTable("Localizations");
	    //Assert.True(result, manager.ErrorMessage);
            
            string physicalPath = Path.GetFullPath(@"..\..\..\Westwind.Globalization.Sample");
	    DbResXConverter converter = new DbResXConverter(configuration, physicalPath);
	    result = converter.ImportWinResources(physicalPath);

	    Assert.True(result, converter.ErrorMessage);
        }


        private void WriteResourceDictionary(Dictionary<string,object> items, string title)
        {
            Console.WriteLine("*** " + title);
            foreach (var item in items)
            {
                Console.WriteLine(item.Key + ": " + item.Value.ToString());
            }

            Dictionary<string,string> its = new Dictionary<string, string> { { "rick","strahl" }, { "frank", "hovell"} };

            Dictionary<string, string> sss = its.Where(dd => dd.Key.Contains('.')).ToDictionary( dd=> dd.Key, dd=> dd.Value);

        }


    }
}
