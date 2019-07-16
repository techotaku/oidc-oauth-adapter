namespace Microsoft.AspNetCore.Builder
{
    public static class Extensions
    {
        public static IApplicationBuilder UseMicrosoft(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<OpenID.Connect.Adapter.OAuth.Providers.Microsoft>();
        }
    }
}