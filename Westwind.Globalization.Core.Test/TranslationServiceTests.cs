﻿using System;
using Westwind.Globalization.Core.DbResourceSupportClasses;
using Westwind.Globalization.Core.Utilities;
using Xunit;

namespace Westwind.Globalization.Test
{
    /// <summary>
    /// Summary description for TranslationServiceTests
    /// </summary>
    public class TranslationServiceTests
    {
        private readonly DbResourceConfiguration configuration;

        public TranslationServiceTests()
        {
            configuration = new DbResourceConfiguration();
        }

        [Fact]
        public void TranslateGoogleTest()
        {
            TranslationServices service = new TranslationServices(configuration);

            string q = null;

            q = "Where are you?";
            string result = service.TranslateGoogle(q, "en", "de");
            Console.WriteLine(q);
            Console.WriteLine(result);
            Console.WriteLine();

            Assert.False(string.IsNullOrEmpty(result), service.ErrorMessage);


            string result2 = service.TranslateGoogle(result, "de", "en");
            Console.WriteLine(result);
            Console.WriteLine(result2);

            Assert.False(string.IsNullOrEmpty(result2), service.ErrorMessage);


            q = "Here's some text \"in quotes\" that needs to encode properly";
            string result3 = service.TranslateGoogle(q, "en", "de");
            Console.WriteLine(q);
            Console.WriteLine(result3);

            Assert.False(string.IsNullOrEmpty(result3), service.ErrorMessage);


            q =
                "Here's some text \"in quotes\" that needs to encode properly Really, where do I go, what do I do, how do I do it and when can it be done, who said it, where is it and whatever happened to Jim, what happened to Helmut when he came home I thought he might have been dead";

            string result4 = service.TranslateGoogle(q, "en", "de");
            Console.WriteLine(q);
            Console.WriteLine(result4);

            Assert.False(string.IsNullOrEmpty(result4), service.ErrorMessage);

        }


        [Fact]
        public void BingTest()
        {
            TranslationServices service = new TranslationServices(configuration);

            // use app.config clientid and clientsecret
            string token = service.GetBingAuthToken();
            Assert.NotNull(token);

            string result = service.TranslateBing("Life is great and one is spoiled when it goes on and on and on", "en",
                "de", token);
            Console.WriteLine(result);

            string result2 = service.TranslateBing(result, "de", "en", token);
            Console.WriteLine(result2);

            string result3 = service.TranslateBing("Here's some text \"in quotes\" that needs to encode properly", "en",
                "de", token);
            Console.WriteLine(result3);

            string ttext =
                "Here's some text \"in quotes\" that needs to encode properly Really, where do I go, what do I do, how do I do it and when can it be done, who said it, where is it and whatever happened to Jim, what happened to Helmut when he came home I thought he might have been dead";
            Console.WriteLine(ttext);
            string result4 = service.TranslateBing(ttext, "en", "de", token);

            Console.WriteLine(result4);
        }



        //[Fact]
        //public void JsonEncodeDecode()
        //{
        //    string orig = "Hier sind einige Text in \"Anführungszeichen\", die ordnungsgemäß";
        //    string encoded = WebUtils.EncodeJsString(orig);
        //    Console.WriteLine(encoded);
        //    Console.WriteLine(WebUtils.DecodeJsString(encoded));
        //}
    }
}
