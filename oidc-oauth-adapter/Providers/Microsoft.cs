using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Net.Http;

namespace OpenID.Connect.Adapter.OAuth.Providers
{
    class Microsoft : Provider
    {
        public Microsoft(RequestDelegate next, IHttpClientFactory httpClientFactory, IMemoryCache memoryCache)
            : base(next, httpClientFactory, memoryCache,
                  key: "microsoft",
                  authorizationEndpoint: "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
                  tokenEndpoint: "https://login.microsoftonline.com/common/oauth2/v2.0/token",
                  userinfoEndpoint: "https://graph.microsoft.com/oidc/userinfo")
        {
        }

        protected override User HandleIdToken(JsonWebToken idToken)
        {
            return new User { Sub = idToken.Subject, Email = idToken.GetClaim("email")?.Value, Name = idToken.GetClaim("name").Value };
        }
    }
}
