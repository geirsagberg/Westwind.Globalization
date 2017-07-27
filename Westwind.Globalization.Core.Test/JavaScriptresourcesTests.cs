using System;
using System.IO;
using Westwind.Globalization.Core.DbResourceSupportClasses;
using Westwind.Globalization.Core.Utilities;
using Xunit;

namespace Westwind.Globalization.Test
{
    public class JavaScriptresourcesTests
    {
        [Fact]
        public void GenerateResources()
        {
            var js = new JavaScriptResources(".\\", new DbResourceConfiguration());
            bool result = js.ExportJavaScriptResources(".\\JavascriptResources\\", "global.resources");
            Assert.True(result);
            Console.WriteLine(File.ReadAllText(".\\JavascriptResources\\" + "LocalizationForm.de.js"));
        }
    }
}
