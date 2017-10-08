using System;
using Westwind.Globalization.Core.DbResourceSupportClasses;
using Westwind.Globalization.Core.Utilities;
using Xunit;

namespace Westwind.Globalization.Test
{
    /// <summary>
    ///     Summary description for DbResXConverterTests
    /// </summary>
    public class StronglyTypedResourceTests
    {
        public StronglyTypedResourceTests()
        {
            configuration = new DbResourceConfiguration();
        }

        private readonly DbResourceConfiguration configuration;

        [Fact]
        public void GenerateStronglyTypedDesignerClassFromResxFile()
        {
            var str = new StronglyTypedResources(@"c:\temp", configuration);
            var res = str.CreateClassFromAllDatabaseResources("ResourceExport", @"resources.cs", new[] {"Resources"});

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
        public void GenerateStronglyTypedResourceResxDesignerFilteredTest()
        {
            var str = new StronglyTypedResources(@"c:\temp", configuration);
            var res = str.CreateResxDesignerClassesFromAllDatabaseResources("ResourceExport", @"c:\temp\resourceTest",
                new[] {"Resources"});

            Console.WriteLine(res);
        }
    }
}