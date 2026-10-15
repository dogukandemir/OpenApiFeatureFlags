# Flag sources

OpenApiFeatureFlags never reads configuration itself. It asks one interface, `IFeatureFlagSource`,
whether a flag is on, and every answer it gets is "on", "off", or "the store could not be read"
([fail-closed](security.md)).

```csharp
public interface IFeatureFlagSource
{
    bool IsEnabled(string flagName);
}
```

Three registrations ship with the family; the fourth shape is a source you write yourself.

## The shipped sources

| Package | Registration | Reads |
| --- | --- | --- |
| `OpenApiFeatureFlags.FeatureManagement` | `AddOpenApiFeatureFlagsWithFeatureManagement()`, or `.AddFeatureManagementFlagSource()` when chaining | Microsoft's `IFeatureManager`, so every configuration source that feeds it works, Azure App Configuration included |
| `OpenApiFeatureFlags.OpenFeature` | `AddOpenApiFeatureFlagsWithOpenFeature(...)`, or `.AddOpenFeatureFlagSource()` when chaining | the CNCF OpenFeature `IFeatureClient`, so every OpenFeature provider works |
| core | `AddOpenApiFeatureFlags(new MyFlagSource(...))` | your own implementation |

`AddOpenApiFeatureFlagsWithFeatureManagement()` is the one-call form: it registers Microsoft's
feature management and points the library at it in the same line.

## Your own source

Implement one method and register it:

```csharp
public sealed class MyFlagSource(IMyToggleStore store) : IFeatureFlagSource
{
    public bool IsEnabled(string flagName) => store.IsOn(flagName);
}

builder.Services.AddOpenApiFeatureFlags(new MyFlagSource(store));
```

Two attributes on one target are ANDed, so the interface is only ever asked about a single flag name
at a time.

## Async-only stores

**The interface is synchronous, on purpose.** Document generation happens inside the document
engine's own synchronous pipeline (Swashbuckle's filters, the built-in engine's transformers, NSwag's
processors, and the document writer they all end in), and none of those extension points has an async
variant to await on. An async interface would have to be blocked on one level lower anyway.

The shipped adapters bridge the async providers they wrap by blocking once per evaluation:

```csharp
// OpenApiFeatureFlags.FeatureManagement, FeatureManagerFlagSource
public bool IsEnabled(string flagName) => _featureManager.IsEnabledAsync(flagName).GetAwaiter().GetResult();
```

That call is safe in the document pipeline: ASP.NET Core installs no synchronization context, so
there is nothing to deadlock on, and the same pattern already appears in the ecosystem's own
Swashbuckle filters. It is still a blocking call per gated element per document request, so a slow or
remote store turns into latency on every document request.

If your store is async-only, pick one of these three:

1. **Cache in the source.** Keep the last known value per flag in a concurrent dictionary, refresh it
   from a background service (`BackgroundService`) on an interval, and answer `IsEnabled` from the
   cache. The document path then never waits on the network.
2. **Use a snapshot provider.** Azure App Configuration's `IFeatureManager` keeps a locally refreshed
   snapshot on its own schedule, which is why `OpenApiFeatureFlags.FeatureManagement` is the
   lower-effort path for that store ([azure-app-configuration.md](azure-app-configuration.md)).
3. **Block deliberately.** `.GetAwaiter().GetResult()` inside your own `IFeatureFlagSource`, exactly
   as the shipped adapters do. Correct, and the right choice only when the store is local and fast.

One property is worth keeping in mind when you add a cache: the library itself keeps no cache across
requests, because a cached verdict is a stale verdict the moment a flag flips. A background-refreshed
source keeps that promise and removes the per-request cost, as long as the refresh interval is short
enough for your process.

## When the store cannot be read

A source that throws, or whose flag cannot be evaluated, is [fail-closed](security.md): the element
is hidden. And if a document contains gated elements and no flag could be read at all, the library
throws `FeatureFlagSourceUnavailableException` instead of publishing a document that silently lost
released endpoints; `CanaryEnabled = false` turns that exception off.

The behaviour to aim for in a source is therefore: throw on a transient failure rather than return
`false`. A `false` for an unreachable store is indistinguishable from a flag that is genuinely off,
which is the failure mode that publishes a gutted document.
