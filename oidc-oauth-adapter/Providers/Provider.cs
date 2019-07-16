using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.JsonWebTokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace OpenID.Connect.Adapter.OAuth.Providers
{
    abstract class Provider
    {
        protected readonly string Key;
        private readonly string _prefixAuthorization;
        private readonly string _prefixToken;
        private readonly string _prefixUserinfo;

        protected readonly string AuthorizationEndpoint;
        protected readonly string TokenEndpoint;
        protected readonly string UserinfoEndpoint;        

        private readonly RequestDelegate _next;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly HttpClient _httpClient;

        private static readonly JsonSerializerSettings settings = new JsonSerializerSettings()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public Provider(RequestDelegate next, IHttpClientFactory httpClientFactory, IMemoryCache memoryCache,
            string key, string authorizationEndpoint, string tokenEndpoint, string userinfoEndpoint)
        {
            Key = key;
            AuthorizationEndpoint = authorizationEndpoint;
            TokenEndpoint = tokenEndpoint;
            UserinfoEndpoint = userinfoEndpoint;

            var prefix = "/" + Key;
            _prefixAuthorization = prefix + "/authorize";
            _prefixToken = prefix + "/token";
            _prefixUserinfo = prefix + "/userinfo";

            _next = next;
            _httpClientFactory = httpClientFactory;
            _memoryCache = memoryCache;

            _httpClient = _httpClientFactory.CreateClient();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.Equals(_prefixAuthorization, StringComparison.InvariantCultureIgnoreCase))
            {
                AuthorizeHandler(context);
            }
            else if (context.Request.Path.Equals(_prefixToken, StringComparison.InvariantCultureIgnoreCase))
            {
                await TokenHandler(context);
            }
            else if (context.Request.Path.Equals(_prefixUserinfo, StringComparison.InvariantCultureIgnoreCase))
            {
                await UserinfoHandler(context);
            }
            else
            {
                await _next(context);
            }            
        }

        protected virtual void AuthorizeHandler(HttpContext context)
        {
            context.Response.Redirect($"{AuthorizationEndpoint}?{context.Request.QueryString}", true);
        }

        protected virtual async Task TokenHandler(HttpContext context)
        {
            var targetRequestMessage = CreateTargetMessage(context, new Uri(TokenEndpoint));

            using (var responseMessage = await _httpClient.SendAsync(targetRequestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted))
            {
                context.Response.StatusCode = (int)responseMessage.StatusCode;
                CopyFromTargetResponseHeaders(context, responseMessage);

                var content = await responseMessage.Content.ReadAsByteArrayAsync();
                var stringContent = Encoding.UTF8.GetString(content);
                var tokens = JObject.Parse(stringContent);
                var accessToken = tokens.SelectToken("access_token") as JValue;
                var idToken = tokens.SelectToken("id_token") as JValue;
                if (accessToken != null && idToken != null)
                {
                    var key = accessToken.Value as string;
                    if (!string.IsNullOrEmpty(key))
                    {
                        var jwt = new JsonWebToken(idToken.Value as string);
                        if (jwt != null)
                        {
                            var user = HandleIdToken(jwt);
                            if (user != null)
                            {
                                _memoryCache.Set(key, user, jwt.ValidTo);
                            }
                        }
                    }
                }

                await context.Response.Body.WriteAsync(content);
            }
        }

        protected abstract User HandleIdToken(JsonWebToken idToken);

        protected virtual async Task UserinfoHandler(HttpContext context)
        {
            if (context.Request.Headers.ContainsKey("Authorization"))
            {
                var key = context.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", string.Empty);
                var user = _memoryCache.Get<User>(key);
                if (user != null)
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(user, settings));
                    return;
                }
            }

            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await context.Response.WriteAsync("{}");
        }

        private static HttpRequestMessage CreateTargetMessage(HttpContext context, Uri targetUri)
        {
            var requestMessage = new HttpRequestMessage();
            CopyFromOriginalRequestContentAndHeaders(context, requestMessage);

            requestMessage.RequestUri = targetUri;
            requestMessage.Headers.Host = targetUri.Host;
            requestMessage.Method = GetMethod(context.Request.Method);

            return requestMessage;
        }

        private static void CopyFromOriginalRequestContentAndHeaders(HttpContext context, HttpRequestMessage requestMessage)
        {
            var requestMethod = context.Request.Method;

            if (!HttpMethods.IsGet(requestMethod) &&
              !HttpMethods.IsHead(requestMethod) &&
              !HttpMethods.IsDelete(requestMethod) &&
              !HttpMethods.IsTrace(requestMethod))
            {
                var streamContent = new StreamContent(context.Request.Body);
                requestMessage.Content = streamContent;
            }

            foreach (var header in context.Request.Headers)
            {
                requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        private static void CopyFromTargetResponseHeaders(HttpContext context, HttpResponseMessage responseMessage)
        {
            foreach (var header in responseMessage.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            foreach (var header in responseMessage.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
            context.Response.Headers.Remove("transfer-encoding");
        }

        private static HttpMethod GetMethod(string method)
        {
            if (HttpMethods.IsDelete(method)) return HttpMethod.Delete;
            if (HttpMethods.IsGet(method)) return HttpMethod.Get;
            if (HttpMethods.IsHead(method)) return HttpMethod.Head;
            if (HttpMethods.IsOptions(method)) return HttpMethod.Options;
            if (HttpMethods.IsPost(method)) return HttpMethod.Post;
            if (HttpMethods.IsPut(method)) return HttpMethod.Put;
            if (HttpMethods.IsTrace(method)) return HttpMethod.Trace;
            return new HttpMethod(method);
        }

    }
}
