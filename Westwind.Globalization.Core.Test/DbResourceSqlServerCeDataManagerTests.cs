using System;
using System.Collections;
using System.Collections.Generic;
using Westwind.Globalization.Core.DbResourceDataManager;
using Westwind.Globalization.Core.DbResourceDataManager.DbResourceDataManagers;
using Westwind.Globalization.Core.DbResourceSupportClasses;
using Westwind.Utilities.Data;
using Xunit;


namespace Westwind.Globalization.Test
{
    public class DbResourceSqlServerCeDataManagerTests
    {
        private DbResourceConfiguration configuration;

        public DbResourceSqlServerCeDataManagerTests()
        {
            configuration = new DbResourceConfiguration();
        }

        private DbResourceSqlServerCeDataManager GetManager()
        {
            var manager = new DbResourceSqlServerCeDataManager(configuration);
            manager.Configuration.ConnectionString = "SqlServerCeLocalizations";
            //manager.Configuration.ResourceTableName = "Localizations";
            return manager;
        }


        [Fact]
        public void CreateTable()
        {
            var manager = GetManager();

            bool result = manager.CreateLocalizationTable();

            // no assertions as table can exist - to test explicitly remove the table
            if (result)
                Console.WriteLine("Table created.");
            else
                Console.WriteLine(manager.ErrorMessage);
        }

        [Fact]
        public void GetAllResources()
        {
            var manager = GetManager();

            var items = manager.GetAllResources(false);
            Assert.NotNull(items);
            Assert.True(items.Count > 0);

            ShowResources(items);
        }

        [Fact]
        public void GetResourceSet()
        {
            var manager = GetManager();

            var items = manager.GetResourceSet("de", "Resources");
            Assert.NotNull(items);
            Assert.True(items.Count > 0);

            ShowResources(items);
        }

        [Fact]
        public void GetResourceSetNormalizedForLocaleId()
        {
            var manager = GetManager();

            var items = manager.GetResourceSetNormalizedForLocaleId("de", "Resources");
            Assert.NotNull(items);
            Assert.True(items.Count > 0);

            ShowResources(items);
        }

        [Fact]
        public void GetAllResourceIds()
        {
            var manager = GetManager();

            var items = manager.GetAllResourceIds("Resources");
            Assert.NotNull(items);
            Assert.True(items.Count > 0);
        }


        [Fact]
        public void GetAllResourceIdsForHtmlDisplay()
        {
            var manager = GetManager();
            var items = manager.GetAllResourceIdListItems("Resources");

            Assert.NotNull(items);
            Assert.True(items.Count > 0);

            foreach (var item in items)
            {
                Console.WriteLine(item.Value + ": " + item.Text + " " + (item.Selected ? "* " : ""));
            }
        }

        [Fact]
        public void GetAllResourceSets()
        {
            var manager = GetManager();

            var items = manager.GetAllResourceSets(ResourceListingTypes.AllResources);
            Assert.NotNull(items);
            Assert.True(items.Count > 0);

            foreach (var item in items)
            {
                Console.WriteLine(item);
            }

            items = manager.GetAllResourceSets(ResourceListingTypes.LocalResourcesOnly);
            Assert.NotNull(items);

            Console.WriteLine("--- Local ---");
            foreach (var item in items)
            {
                Console.WriteLine(item);
            }

            items = manager.GetAllResourceSets(ResourceListingTypes.GlobalResourcesOnly);
            Assert.NotNull(items);

            Console.WriteLine("--- Global ---");
            foreach (var item in items)
            {
                Console.WriteLine(item);
            }
        }

        [Fact]
        public void GetAllLocaleIds()
        {
            var manager = GetManager();

            var items = manager.GetAllLocaleIds("Resources");
            Assert.NotNull(items);
            Assert.True(items.Count > 0);

            foreach (var localeId in items)
            {
                Console.WriteLine(":" + localeId);
            }

        }

        [Fact]
        public void GetAllResourcesForCulture()
        {
            var manager = GetManager();

            var items = manager.GetAllResourcesForCulture("Resources", "de");
            Assert.NotNull(items);
            Assert.True(items.Count > 0);

            foreach (var localeId in items)
            {
                Console.WriteLine(":" + localeId);
            }
        }

        [Fact]
        public void GetResourceString()
        {
            var manager = GetManager();

            var item = manager.GetResourceString("Today", "Resources", "de");

            Assert.NotNull(item);
            Assert.True(item == "Heute");
        }

        [Fact]
        public void GetResourceItem()
        {
            var manager = GetManager();
            var item = manager.GetResourceItem("Today", "Resources", "de");

            Assert.NotNull(item);
            Assert.True(item.Value.ToString() == "Heute");
        }

        [Fact]
        public void GetResourceObject()
        {
            var manager = GetManager();

            // this method allows retrieving non-string values as their
            // underlying type - demo data doesn't include any binary data.
            var item = manager.GetResourceObject("Today", "Resources", "de");

            Assert.NotNull(item);
            Assert.True(item.ToString() == "Heute");
        }

        [Fact]
        public void GetResourceStrings()
        {
            var manager = GetManager();

            var items = manager.GetResourceStrings("Today", "Resources");

            Assert.NotNull(items);
            Assert.True(items.Count > 0);

            ShowResources(items);

        }



        [Fact]
        public void UpdateResourceString()
        {
            var manager = GetManager();

            string updated = "Heute Updated";
            int count = manager.UpdateOrAddResource("Today", updated, "de", "Resources");

            Assert.False(count == -1, manager.ErrorMessage);
            string check = manager.GetResourceString("Today", "Resources", "de");

            Assert.Equal(check, updated);
            Console.WriteLine(check);

            manager.UpdateOrAddResource("Today", "Heute", "de", "Resources", null);
        }



        [Fact]
        public void AddAndDeleteResourceString()
        {
            var manager = GetManager();

            string resourceId = "NewlyAddedTest";
            string text = "Newly Added Test";

            int count = manager.AddResource(resourceId, text, "de", "Resources");

            Assert.False(count == -1, manager.ErrorMessage);
            string check = manager.GetResourceString(resourceId, "Resources", "de");

            Assert.Equal(check, text);
            Console.WriteLine(check);

            bool result = manager.DeleteResource(resourceId, resourceSet: "Resources", cultureName: "de");
            Assert.True(result, manager.ErrorMessage);

            check = manager.GetResourceString(resourceId, "Resources", "de");
            Assert.Null(check);
        }




        private void ShowResources(IDictionary items)
        {
            foreach (DictionaryEntry resource in items)
            {
                Console.WriteLine(resource.Key + ": " + resource.Value);
            }
        }
        private void ShowResources(IEnumerable<ResourceItem> items)
        {
            foreach (var resource in items)
            {
                Console.WriteLine(resource.ResourceId + " - " + resource.LocaleId + ": " + resource.Value);
            }
        }
    }
}
