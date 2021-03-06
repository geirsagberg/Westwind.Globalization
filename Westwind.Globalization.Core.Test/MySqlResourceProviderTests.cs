﻿using System;
using Westwind.Globalization.Core.DbResourceManager;
using Westwind.Globalization.Core.DbResourceSupportClasses;
using Xunit;

namespace Westwind.Globalization.Test
{
    public class DbResTests
    {
        public DbResTests()
        {
            DbRes.Initialize(new DbResourceConfiguration());
        }

        [Fact]
        public void DbResSimpleValues()
        {
            Console.WriteLine(DbRes.T("Today", "CommonPhrases", "de-de"));
            Console.WriteLine(DbRes.T("Yesterday", "CommonPhrases", "de-de"));
            Console.WriteLine(DbRes.T("Save", "CommonPhrases", "de-de"));

            Console.WriteLine(DbRes.T("Today", "CommonPhrases", "en-US"));
            Console.WriteLine(DbRes.T("Yesterday", "CommonPhrases", "en-US"));
            Console.WriteLine(DbRes.T("Save", "CommonPhrases", "en-US"));
        }
    }
}
