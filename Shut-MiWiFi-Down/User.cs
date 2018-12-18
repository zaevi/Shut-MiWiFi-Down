﻿using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ShutMiWiFiDown
{
    public class User
    {
        const string UrlHome = "/cgi-bin/luci/web/home";
        const string UrlLoginPost = "/cgi-bin/luci/api/xqsystem/login";

        public string Password { get; set; }

        public string UrlHost { get; set; }

        private CookieContainer _cookies;
        public CookieContainer Cookies => _cookies;

        protected HttpClient Client = null;

        protected User() { }

        public static User Create(string password, string host, CookieContainer cookies = null)
        {
            cookies = cookies ?? new CookieContainer();
            var handler = new HttpClientHandler() { CookieContainer = cookies };
            var user = new User() { Password = password, UrlHost = host };
            user.Client = new HttpClient(handler) { BaseAddress = new Uri(user.UrlHost) };
            user._cookies = cookies;
            return user;
        }

        public async Task LoginAsync()
        {
            var doc = await GetDocumentAsync(UrlHome);
            var script = doc.DocumentNode.SelectSingleNode(@"//script[19]").InnerText;

            var key = Regex.Match(script, @"key: '(.*)'").Groups[1].Value;
            var mac = Regex.Match(script, @"deviceId = '(.*)'").Groups[1].Value;

            var type = 0;
            var time = (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000000;
            var random = (int)(new Random().NextDouble() * 10000);

            var nonce = string.Join("_", type, mac, time, random);

            var sha1 = System.Security.Cryptography.SHA1.Create();
            var hash = sha1.ComputeHash(Encoding.ASCII.GetBytes(nonce).Concat(sha1.ComputeHash(Encoding.ASCII.GetBytes(Password + key))).ToArray());
            var pass = string.Join("", hash.Select(c => c.ToString("x")));

            var postData = BuildPostData(
                "username", "admin",
                "password", pass,
                "logtype", "2",
                "nonce", nonce);

            var response = await Client.PostAsync(UrlLoginPost, postData);

            if (response.StatusCode != HttpStatusCode.OK)
                throw new HttpRequestException("POST response code: " + (int)response.StatusCode);
        }

        #region [HttpClient Extensions]
        protected async Task<HttpResponseMessage> GetAsync(string requestUri)
        {
            var response = await Client.GetAsync(requestUri);
            if (response.IsSuccessStatusCode)
                return response;
            throw new HttpRequestException("GET response code: " + (int)response.StatusCode);
        }

        protected async Task<HtmlDocument> GetDocumentAsync(string requestUri)
        {
            var response = await GetAsync(requestUri);
            var str = await response.Content.ReadAsStringAsync();
            var document = new HtmlDocument();
            document.LoadHtml(str);
            return document;
        }

        protected static FormUrlEncodedContent BuildPostData(string key1, string value1, params string[] keyValues)
        {
            var collection = new List<KeyValuePair<string, string>>();
            collection.Add(new KeyValuePair<string, string>(key1, value1));
            for (int i = 0; i < keyValues.Length; i += 2)
                collection.Add(new KeyValuePair<string, string>(keyValues[i], keyValues[i + 1]));
            return new FormUrlEncodedContent(collection);
        }
        #endregion

    }
}
