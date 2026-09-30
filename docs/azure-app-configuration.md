# Azure App Configuration

**Short answer: it is already covered, and there is no Azure-specific code in this library.** Azure
App Configuration is a *configuration source*, not a flag API. It feeds
`Microsoft.FeatureManagement`, and `OpenApiFeatureFlags.FeatureManagement` reads flags through
`IFeatureManager`. So the integration you need is the one in
[the FeatureManagement adapter](../src/OpenApiFeatureFlags.FeatureManagement), and it works with
Azure App Configuration, a local `appsettings.json`, or anything else that populates that section.

```
Azure App Configuration  ->  IConfiguration  ->  IFeatureManager  ->  IFeatureFlagSource  ->  the document
   (the store)              (the provider)     (Microsoft)          (this library)
```

The whole point of decision D8 is that these are two independent axes: the document engine is one, the
flag source is the other. Azure is a *store behind* the flag source, so it needs no adapter of its own.

---

## Wiring it up

```shell
dotnet add package Microsoft.Extensions.Configuration.AzureAppConfiguration
dotnet add package Microsoft.Azure.AppConfiguration.AspNetCore   # only for dynamic refresh
dotnet add package Azure.Identity                                 # only for DefaultAzureCredential
dotnet add package OpenApiFeatureFlags.Swashbuckle
dotnet add package OpenApiFeatureFlags.FeatureManagement
```

```csharp
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// 1. The store. No secret: DefaultAzureCredential uses the app's managed identity.
builder.Configuration.AddAzureAppConfiguration(options =>
{
    options
        .Connect(
            new Uri(builder.Configuration["Azure:AppConfiguration:Endpoint"]!),
            new DefaultAzureCredential())

        // 2. WITHOUT THIS LINE NO FLAGS ARE SELECTED AT ALL. See "Nothing is documented" below.
        .UseFeatureFlags(flags =>
        {
            // Which environment's flags. One label per environment is the usual arrangement.
            flags.Label = builder.Environment.EnvironmentName;

            // Re-read the store periodically. Swashbuckle regenerates the document on every
            // request, so a flip shows up on the next document generation after this elapses.
            flags.SetRefreshInterval(TimeSpan.FromSeconds(30));
        });
});

// 3. Serve refreshed flags without a restart.
builder.Services.AddAzureAppConfiguration();

// 4. Point this library at IFeatureManager, and register the filters.
builder.Services.AddOpenApiFeatureFlagsWithFeatureManagement();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "1.0" });
    options.AddOpenApiFeatureFlagFilters();
});

var app = builder.Build();

app.UseAzureAppConfiguration();   // refreshes the configuration per request
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();
```

The store's endpoint arrives as `Azure__AppConfiguration__Endpoint`, which is exactly the app setting
recorded in [`BACKLOG.md`](../BACKLOG.md) Appendix A.2 — so the consumer's existing infrastructure
needs no change.

### Restricting which flags are pulled

```csharp
options.UseFeatureFlags(flags =>
{
    flags.Select("MyApp/*", "Production", tagFilters: null);   // key filter, label filter, tags
});
```

`FeatureFlagOptions.Select(keyFilter, labelFilter, tagFilters)` narrows the set, which matters when
the store serves several applications.

---

## The four ways this can bite you

These are the reason a flag source is configured deliberately rather than by copying a snippet.

### 1. Nothing is documented any more

`AddAzureAppConfiguration(...)` **without `UseFeatureFlags()`** selects no feature flags. Its
`Select` for ordinary key-values does not pick up `Microsoft.AppConfiguration/FeatureFlag` settings.
`IFeatureManager` therefore knows nothing, every flag reads as disabled, and **every gated element
disappears** — with no error, because "the flag is missing" and "the flag is off" are the same answer
to a fail-closed library.

Same symptom, same silence, for a typo in the attribute (`"NewCheckout"` vs `"Newcheckout"`) or a
`Label` that does not match the environment. Global configuration questions are answered by the log
line, which tells you which flags were evaluated and how many elements were hidden:

```
OpenApiFeatureFlags finished the document in mode Remove: 12 gated element(s) hidden, 0 published, flags evaluated [NewCheckout].
```

Twelve hidden from one flag is the signature of a store that answered nothing useful.
`FeatureManagementOptions.IgnoreMissingFeatures` decides whether a missing *feature* is silently
treated as disabled or throws — check which way yours is set before assuming this is a bug.

### 2. A partially rolled-out flag is hidden entirely

Azure App Configuration flags can carry a `Targeting` filter (percentage, users, groups). This library
asks a document-level question — "is this feature on?" — and a document is not tenant-scoped
(decision D7). No `TargetingContext` is supplied, so targeting evaluates as not applicable and the
element stays hidden.

That is deliberate: a 50 % rollout has no single correct answer for a published document. Early-access
merchants simply get no public docs for that surface until the flag is on for everyone.

### 3. A missing feature filter *does* fail loudly

If a flag references a filter that is not registered, `IFeatureManager` throws rather than answering.
The library treats that as not-enabled (fail closed, D5) and logs a warning per flag:

```
OpenApiFeatureFlags could not evaluate the flag {FlagName}; treating it as disabled so the document fails closed (D5).
```

If **every** read in a document fails, the canary from decision D6 aborts instead of publishing a
document that is quietly missing released endpoints:

```
OpenApiFeatureFlags.FeatureFlagSourceUnavailableException
```

That is the difference between "the store told us the flags are off" and "the store did not answer".
Only the second one is dangerous, and only the second one throws.

### 4. Build-time document generation contacts the store

The offline export path (`swagger tofile`, or `OpenApiGenerateDocumentsOnBuild`, which the
[sample](../samples/OpenApiFeatureFlags.Sample) uses) **builds the host**, so `AddAzureAppConfiguration`
runs and the store is contacted during the build. Two consequences:

- CI needs credentials for the store, or the export must fall back to a local toggle JSON. Appendix A.2
  in [`BACKLOG.md`](../BACKLOG.md) records how a real application already does this with
  `az appconfig --auth-mode login`.
- A flag flip changes the exported artifact, so the export belongs in a pipeline that re-runs when
  flags change — not in a one-off artifact you keep forever.

If the store is unreachable at build time, every read fails and the canary fails the build. That is
D6 doing its job: better a red pipeline than a published document missing released endpoints.

---

## Verified API reference

From `Microsoft.Extensions.Configuration.AzureAppConfiguration` 8.6.0 and
`Microsoft.FeatureManagement` 4.8.0:

| Member | Notes |
|---|---|
| `AzureAppConfigurationOptions.Connect(Uri, TokenCredential)` | Keyless. The `string` overload takes a connection string. |
| `AzureAppConfigurationOptions.UseFeatureFlags(Action<FeatureFlagOptions>)` | **Required.** Selects feature flags. |
| `AzureAppConfigurationOptions.ConfigureRefresh(Action<AzureAppConfigurationRefreshOptions>)` | Non-flag key-values. |
| `FeatureFlagOptions.Label` | Environment / stage selection. |
| `FeatureFlagOptions.Select(keyFilter, labelFilter, tagFilters)` | Narrow the pulled set. |
| `FeatureFlagOptions.CacheExpirationInterval` / `SetRefreshInterval(TimeSpan)` | How often to re-read. |
| `AzureAppConfigurationRefreshOptions.RegisterAll()` / `SetRefreshInterval(TimeSpan)` | Refresh wiring. |
| `IConfigurationRefresher.TryRefreshAsync(CancellationToken)` | Refresh on demand. |
| `AddFeatureManagement(IServiceCollection)` | Needs `IConfiguration` registered, which an ASP.NET host always has. |
| `FeatureManagementOptions.IgnoreMissingFeatures` / `IgnoreMissingFeatureFilters` | Whether a missing feature or filter is silent or throws. |

---

## Runtime gating is still your job

This library only shapes the **document**. If the same flag must also stop the endpoint from being
served, that is a separate mechanism — `[FeatureGate]` from `Microsoft.FeatureManagement.AspNetCore`,
`IPolicyEvaluator`, or whatever the application already uses:

```csharp
[OpenApiFeatureFlag("NewCheckout")]   // shapes the document
[FeatureGate("NewCheckout")]          // gates the request
[HttpPost("checkout")]
public IActionResult Checkout() => Ok();
```

Two attributes, two jobs, and neither one silently does the other (decision D2).
