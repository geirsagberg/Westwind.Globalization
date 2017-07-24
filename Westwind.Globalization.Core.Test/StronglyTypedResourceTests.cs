
using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Westwind.Globalization.Core.Utilities;
using Xunit;
using Westwind.Globalization.Core.DbResourceSupportClasses;

namespace Westwind.Globalization.Test
{
    /// <summary>
    /// Summary description for DbResXConverterTests
    /// </summary>
    public class StronglyTypedResourceTests
    {
        private readonly DbResourceConfiguration configuration;

        public StronglyTypedResourceTests()
        {
            //DbResourceConfiguration.Current.ConnectionString = "SqLiteLocalizations";
            //DbResourceConfiguration.Current.DbResourceDataManagerType = typeof (DbResourceSqLiteDataManager);
            configuration = new DbResourceConfiguration();
        }

        [Fact]
        public void GenerateStronglyTypedResourceClassFilteredTest()
        {
            var str = new StronglyTypedResources(@"c:\temp", configuration);
            var res = str.CreateClassFromAllDatabaseResources("ResourceExport", @"resources.cs", new string[] { "Resources" });

            Console.WriteLine(res);
        }


        [Fact]
        public void GenerateStronglyTypedResourceResxDesignerFilteredTest()
        {
            var str = new StronglyTypedResources(@"c:\temp", configuration);
            var res = str.CreateResxDesignerClassesFromAllDatabaseResources("ResourceExport", @"c:\temp\resourceTest", new string[] { "Resources" });

            Console.WriteLine(res);
        }

        [Fact]
        public void GenerateStronglyTypedResourceResxDesignerAllResourcesTest()
        {

            var str = new StronglyTypedResources(@"c:\temp\resourceTest", configuration);
            var res = str.CreateResxDesignerClassesFromAllDatabaseResources("ResourceExport", @"c:\temp\resourceTest");

            Console.WriteLine(res);
        }

        [Fact]
        public void GenerateStronglyTypedDesignerClassFromResxFile()
        {
            string filename = @"c:\temp\resourceTest\LocalizationForm.resx";
            string designerFile = Path.ChangeExtension(filename, "designer.cs");
            if (File.Exists(designerFile))
                File.Delete(designerFile);

            var str = new StronglyTypedResources(@"c:\temp\resourceTest", configuration);
            str.CreateResxDesignerClassFromResxFile(filename, "LocalizationAdmin", "Westwind.Globalization.Sample");

            Assert.True(File.Exists(designerFile));

        }
    }
}
