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
using System.Net;
using System.Text;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Westwind.Globalization.Core.DbResourceSupportClasses;
using Westwind.Utilities;

namespace Westwind.Globalization.Core.Utilities
{
    /// <summary>
    /// Provides basic translation features via several Web interfaces
    /// 
    /// NOTE: These services may change their format or otherwise fail.
    /// </summary>
    public class TranslationServices
    {
        private readonly DbResourceConfiguration configuration;

        public TranslationServices(DbResourceConfiguration configuration)
        {
            this.configuration = configuration;
        }

        /// <summary>
        /// Error message set when an error occurs in the translation service
        /// </summary>
        public string ErrorMessage { get; set; } = "";

        /// <summary>
        /// Timeout for how long to wait for a translation
        /// </summary>
        public int TimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Translates a string into another language using Google's translate API JSON calls.
        /// <seealso>Class TranslationServices</seealso>
        /// </summary>
        /// <param name="Text">Text to translate. Should be a single word or sentence.</param>
        /// <param name="FromCulture">
        /// Two letter culture (en of en-us, fr of fr-ca, de of de-ch)
        /// </param>
        /// <param name="ToCulture">
        /// Two letter culture (as for FromCulture)
        /// </param>
        /// <param name="googleApiKey">Google Api key - if not specified it's read from the configuration</param>
        public string TranslateGoogle(string text, string fromCulture, string toCulture, string googleApiKey = null)
        {
            fromCulture = fromCulture.ToLower();
            toCulture = toCulture.ToLower();

            if (!string.IsNullOrEmpty(googleApiKey))
            {
                googleApiKey = configuration.GoogleApiKey;
                if (!string.IsNullOrEmpty(googleApiKey))

                    return TranslateGoogleApi(text, fromCulture, toCulture, googleApiKey);
            }

            // normalize the culture in case something like en-us was passed 
            // retrieve only en since Google doesn't support sub-locales
            string[] tokens = fromCulture.Split('-');
            if (tokens.Length > 1)
                fromCulture = tokens[0];

            // normalize ToCulture
            tokens = toCulture.Split('-');
            if (tokens.Length > 1)
                toCulture = tokens[0];

            string format = "https://translate.googleapis.com/translate_a/single?client=gtx&sl={1}&tl={2}&dt=t&q={0}";

            string url = string.Format(format,
                text, fromCulture, toCulture);

            // Retrieve Translation with HTTP GET call
            string jsonString;
            try
            {
                WebClient web = new WebClient();
                web.Headers.Add(HttpRequestHeader.AcceptCharset, "UTF-8");

                // Make sure we have response encoding to UTF-8
                web.Encoding = Encoding.UTF8;
                jsonString = web.DownloadString(url);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Connection failed: " +
                               ex.GetBaseException().Message;
                return null;
            }


            // format:
            //[[["Hallo grausame Welt","Hello Cruel world",,,0]],,"en"]
            //[ [ ["Hallo grausame Welt","Hello Cruel world",,,0]],,"en"]
            dynamic json = JArray.Parse(jsonString);
            string result = json[0][0][0];

            if (string.IsNullOrEmpty(result))
            {
                ErrorMessage = "Invalid search result";
                return null;
            }

            result = WebUtility.HtmlDecode(result);
            return result;
        }



        /// <summary>
        /// Translates a string into another language using Google's translate API JSON calls.
        /// <seealso>Class TranslationServices</seealso>
        /// </summary>
        /// <param name="Text">Text to translate. Should be a single word or sentence.</param>
        /// <param name="FromCulture">
        /// Two letter culture (en of en-us, fr of fr-ca, de of de-ch)
        /// </param>
        /// <param name="ToCulture">
        /// Two letter culture (as for FromCulture)
        /// </param>
        /// <param name="googleApiKey">Google Api key - if not specified it's read from the configuration</param>
        public string TranslateGoogleApi(string text, string fromCulture, string toCulture, string googleApiKey = null)
        {

            if (string.IsNullOrEmpty(googleApiKey))
                googleApiKey = configuration.GoogleApiKey;

            fromCulture = fromCulture.ToLower();
            toCulture = toCulture.ToLower();

            // normalize the culture in case something like en-us was passed 
            // retrieve only en since Google doesn't support sub-locales
            string[] tokens = fromCulture.Split('-');
            if (tokens.Length > 1)
                fromCulture = tokens[0];

            // normalize ToCulture
            tokens = toCulture.Split('-');
            if (tokens.Length > 1)
                toCulture = tokens[0];

            string format = "https://www.googleapis.com/language/translate/v2?key={3}&source={1}&target={2}&q={0}";

            string url = string.Format(format,
                text, fromCulture, toCulture, googleApiKey);

            // Retrieve Translation with HTTP GET call
            string jsonString;
            try
            {
                WebClient web = new WebClient();
                web.Headers.Add(HttpRequestHeader.AcceptCharset, "UTF-8");

                // Make sure we have response encoding to UTF-8
                web.Encoding = Encoding.UTF8;
                jsonString = web.DownloadString(url);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Connection failed" + ": " +
                               ex.GetBaseException().Message;
                return null;
            }


            // format:
            //{
            //   "data": {
            //       "translations": [
            //       {
            //          "translatedText": "Wo bist du"
            //   }
            //  ]
            // }
            //}
            dynamic json = JValue.Parse(jsonString);
            string result = json.data.translations[0].translatedText;

            if (string.IsNullOrEmpty(result))
            {
                ErrorMessage = "Invalid search result";
                return null;
            }

            result = WebUtility.HtmlDecode(result);
            return result;
        }


        /// <summary>
        /// Uses the Bing API service to perform translation
        /// Bing can translate up to 1000 characters. 
        /// 
        /// Requires that you provide a CLientId and ClientSecret
        /// or set the configuration values for these two.
        /// 
        /// More info on setup:
        /// http://weblog.west-wind.com/posts/2013/Jun/06/Setting-up-and-using-Bing-Translate-API-Service-for-Machine-Translation
        /// </summary>
        /// <param name="text">Text to translate</param>
        /// <param name="fromCulture">Two letter culture name</param>
        /// <param name="toCulture">Two letter culture name</param>
        /// <param name="accessToken">Pass an access token retrieved with GetBingAuthToken.
        /// If not passed the default keys from .config file are used if any</param>
        /// <returns></returns>
        public string TranslateBing(string text, string fromCulture, string toCulture,
            string accessToken = null)
        {
            string serviceUrl = "http://api.microsofttranslator.com/V2/Http.svc/Translate";

            if (accessToken == null)
            {
                accessToken = GetBingAuthToken();
                if (accessToken == null)
                    return null;
            }

            string res;

            try
            {
                var web = new WebClient();
                web.Headers.Add("Authorization", "Bearer " + accessToken);
                string ct = "text/plain";
                string postData = string.Format("?text={0}&from={1}&to={2}&contentType={3}",
                    StringUtils.UrlEncode(text),
                    fromCulture, toCulture,
                    StringUtils.UrlEncode(ct));

                web.Encoding = Encoding.UTF8;
                res = web.DownloadString(serviceUrl + postData);
            }
            catch (Exception e)
            {
                ErrorMessage = e.GetBaseException().Message;
                return null;
            }

            // result is a single XML Element fragment
            var doc = new XmlDocument();
            doc.LoadXml(res);
            return doc.DocumentElement.InnerText;
        }

        /// <summary>
        /// Retrieves an oAuth authentication token to be used on the translate
        /// API request. The result string needs to be passed as a bearer token
        /// to the translate API.
        /// 
        /// You can find client ID and Secret (or register a new one) at:
        /// https://datamarket.azure.com/developer/applications/
        /// </summary>
        /// <param name="clientId">The client ID of your application</param>
        /// <param name="clientSecret">The client secret or password</param>
        /// <returns></returns>
        public string GetBingAuthToken(string clientId = null, string clientSecret = null)
        {
            string authBaseUrl = "https://datamarket.accesscontrol.windows.net/v2/OAuth2-13";

            if (string.IsNullOrEmpty(clientId))
                clientId = configuration.BingClientId;
            if (string.IsNullOrEmpty(clientSecret))
                clientSecret = configuration.BingClientSecret;

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                ErrorMessage = "Client Id and Client Secret must be provided";
                return null;
            }

            var postData = string.Format("grant_type=client_credentials&client_id={0}" +
                                         "&client_secret={1}" +
                                         "&scope=http://api.microsofttranslator.com",
                StringUtils.UrlEncode(clientId),
                StringUtils.UrlEncode(clientSecret));

            // POST Auth data to the oauth API
            string res, token;

            try
            {
                var web = new WebClient();
                web.Encoding = Encoding.UTF8;
                res = web.UploadString(authBaseUrl, postData);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.GetBaseException().Message;
                return null;
            }


            var auth = JsonConvert.DeserializeObject(res, typeof (BingAuth)) as BingAuth;
            if (auth == null)
                return null;

            token = auth.access_token;

            return token;
        }

        private class BingAuth
        {
            public string token_type { get; set; }
            public string access_token { get; set; }
        }

    }
}
