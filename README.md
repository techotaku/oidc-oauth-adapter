# oidc-oauth-adapter

An adapter, helps legacy OAuth system connect to OpenID Connect service.

## Security Warning

To achieve design goals, this adapter **HAVE TO** store some sensitive information temporarily.  
You **MUST** deploy your private *adapter*, instead of use a public *adapter* service or untrusted third party *adapter* service.

## API
+ `/{provider}/token` Proxy to provider's token endpoint. *Adapter* will parse `email`, `name` and `sub` from `id_token`, then store them temporarily, under the key `access_token`.
+ `/{provider}/userinfo` 
+ `/{provider}/authorize` **Not recommended.** Redirect to provider's authorize endpoint. You **SHOULD** use your provider's authorize endpoint **DIRECTLY** (to hide the *adapter*).

## Providers
+ [Microsoft](https://docs.microsoft.com/en-us/azure/active-directory/develop/v2-oauth2-auth-code-flow)