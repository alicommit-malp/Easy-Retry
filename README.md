# Async/Await easy retry in c#

In Asynchronous programming in some cases we need to retry a method if it fails. [Easy-Retry](https://www.nuget.org/packages/EasyRetry) can provide this functionality with ease :)

Targets `netstandard2.0` and `netstandard2.1`, so it runs on .NET Framework 4.6.1+, .NET Core 2.0+ and .NET 5+. The only dependency is `Microsoft.Extensions.Logging.Abstractions`.

#### [NuGet](https://www.nuget.org/packages/EasyRetry) Installation
#### [GitHub](https://github.com/alicommit-malp/Easy-Retry) Source Code

```
.Net CLI
dotnet add package EasyRetry

Package Manager
Install-Package EasyRetry

```

## Quick start

Let's say there is a HTTP Task which you need to retry in case it fails

```c#
private async Task Task_NetworkBound()
{
    await new HttpClient().GetStringAsync("https://dotnetfoundation.org");
}
```

In order to retry it once, after 5 seconds, you just need to do as follows

```c#
//With DI
services.AddSingleton<IEasyRetry, EasyRetry>();
...
await _easyRetry.Retry(async () => await Task_NetworkBound());

//Without DI
await new EasyRetry().Retry(async () => await Task_NetworkBound());
```

The `Func<Task<T>>` overload returns the value of the first successful attempt

```c#
var html = await _easyRetry.Retry(() => new HttpClient().GetStringAsync("https://dotnetfoundation.org"));
```

## Options

Every `Retry` overload takes an optional `RetryOptions`. When omitted, the defaults below apply.

| Property | Type | Default | Meaning |
|---|---|---|---|
| `Attempts` | `int` | `2` | Total number of invocations including the first (`2` = one retry). Must be `>= 1`. |
| `DelayBetweenRetries` | `TimeSpan` | `5 s` | Base delay between attempts. Must be `>= 0`. |
| `DelayBeforeFirstTry` | `TimeSpan` | `0` | Waited once, before the first attempt only. |
| `Backoff` | `RetryBackoff` | `Constant` | `Constant`, `Linear` or `Exponential`. Delay before retry `n` (n = failures so far): Constant = base; Linear = base x n; Exponential = base x 2^(n-1). |
| `MaxDelay` | `TimeSpan?` | `null` | Upper bound for the computed delay. |
| `UseJitter` | `bool` | `false` | Multiplies each delay by a random factor in `[0.8, 1.2]`. |
| `DoNotRetryOnTheseExceptionTypes` | `List<Type>` | empty | Exceptions of these types, or types derived from them, are not retried and propagate immediately. |
| `ShouldRetry` | `Func<Exception, int, bool>?` | `null` | Called with the exception and the 1-based attempt that just failed. Return `false` to stop retrying. |
| `CancellationToken` | `CancellationToken` | `default` | Cancels the between-retry delay and stops retrying (see Cancellation). |
| `EnableLogging` | `bool` | `false` | Log each failure through the injected `ILogger<EasyRetry>` (see Logging). |
| `RetryOnResult` | `Func<object?, bool>?` | `null` | Called with the result of a successful `Func<Task<T>>` invocation. Return `true` to treat the attempt as failed. After the last attempt the last result is returned, not thrown. Ignored by the overloads without a result. |
| `MaxTotalDuration` | `TimeSpan?` | `null` | Overall time budget measured from the call. If the next delay would exceed it, retrying stops and the last exception is rethrown (or the last result returned). Never aborts an attempt in progress. |
| `DelayOverride` | `Func<Exception?, int, TimeSpan?>?` | `null` | Called with the exception (null for a result-based failure) and the failed attempt. A non-null return replaces the computed delay for that retry. Jitter is not applied to it; `MaxDelay` still caps it. |
| `OnRetry` | `Action<Exception?, int, TimeSpan>?` | `null` | Called before each wait with the exception (null for a result-based failure), the failed attempt and the delay about to be waited. Not called on the final failure. If it throws, retrying stops and that exception propagates. |

A fuller example

```c#
await _easyRetry.Retry(async () => await Task_NetworkBound()
    , new RetryOptions()
    {
        Attempts = 5,
        DelayBetweenRetries = TimeSpan.FromSeconds(1),
        Backoff = RetryBackoff.Exponential,   // 1s, 2s, 4s, 8s ...
        MaxDelay = TimeSpan.FromSeconds(5),   // ... capped at 5s
        UseJitter = true,
        MaxTotalDuration = TimeSpan.FromSeconds(30),
        DoNotRetryOnTheseExceptionTypes = new List<Type>()
        {
            typeof(ArgumentException)         // also covers ArgumentNullException etc.
        },
        ShouldRetry = (ex, attempt) => ex is HttpRequestException,
        OnRetry = (ex, attempt, delay) => Console.WriteLine($"attempt {attempt} failed, waiting {delay}"),
        CancellationToken = cancellationToken,
        EnableLogging = true
    });
```

Invalid options are rejected before the first attempt: `ArgumentNullException` for a null delegate, `ArgumentOutOfRangeException` for `Attempts < 1` or any negative `TimeSpan`, and `ArgumentException` if `DoNotRetryOnTheseExceptionTypes` is null.

## Retrying on a result

Not every failure is an exception. `RetryOnResult` inspects the value a successful `Func<Task<T>>` produced and, if it returns `true`, treats that attempt as failed and schedules the next one exactly like an exception would. It receives the result as `object?`, so use pattern matching to get at the real type. The `Task` and `Action` overloads have no result and ignore it.

```c#
var response = await _easyRetry.Retry(() => client.GetAsync(url), new RetryOptions
{
    Attempts = 4,
    DelayBetweenRetries = TimeSpan.FromSeconds(2),
    RetryOnResult = r => r is HttpResponseMessage m && m.StatusCode == HttpStatusCode.ServiceUnavailable
});
```

If every attempt is rejected, the last response is returned rather than an exception being thrown, so check it as you normally would.

## Attempt number and cancellation token in your delegate

The static class `EasyRetryExtensions` (same namespace, nothing extra to import) adds overloads whose delegate receives the 1-based attempt number and the `CancellationToken` from the options: `Retry<T>(Func<int, CancellationToken, Task<T>>)`, `Retry(Func<int, CancellationToken, Task>)` and `Retry(Action<int, CancellationToken>)`. They are extension methods on `IEasyRetry`, so they work with any implementation, including your own test doubles.

```c#
var response = await _easyRetry.Retry((attempt, token) =>
    client.GetAsync(attempt >= 2 ? fallbackUrl : primaryUrl, token),
    new RetryOptions { Attempts = 3, CancellationToken = cancellationToken });
```

## Honouring Retry-After

When a service tells you how long to back off, return that from `DelayOverride` and it replaces the computed delay for that retry (still capped by `MaxDelay`, no jitter applied). Returning `null` keeps the normal delay, so one lambda handles both cases.

```c#
class RateLimitedException : Exception { public TimeSpan RetryAfter { get; init; } }

await _easyRetry.Retry(() => CallRateLimitedApi(), new RetryOptions
{
    Attempts = 5,
    DelayOverride = (ex, attempt) => (ex as RateLimitedException)?.RetryAfter
});
```

## Cancellation

Pass a `CancellationToken` through `RetryOptions.CancellationToken`. When the token is cancelled, any pending delay between attempts is aborted and an `OperationCanceledException` thrown by your delegate is no longer retried but propagates straight away. Cancellation is only recognised when the token in the options is actually cancelled: an `OperationCanceledException` or `TaskCanceledException` raised while that token is *not* cancelled (for example an `HttpClient` timeout) is treated like any other failure and retried. If you want timeouts to stop the retry loop, add `typeof(OperationCanceledException)` to `DoNotRetryOnTheseExceptionTypes` or handle it in `ShouldRetry`.

## Synchronous usage

There is an `Action` overload for code that is not async

```c#
new EasyRetry().Retry(() => File.Copy(source, destination), new RetryOptions()
{
    Attempts = 3,
    DelayBetweenRetries = TimeSpan.FromMilliseconds(500)
});
```

It blocks the calling thread while waiting between attempts, so avoid it on UI threads or inside async code; use the `Task` overloads there.

## Inspecting failures

After `Attempts` failures the original last exception is rethrown unchanged, so existing `catch` blocks keep working. The number of invocations and every attempt's exception are attached to `Exception.Data` under the keys in `RetryDataKeys`

```c#
try
{
    await _easyRetry.Retry(async () => await Task_NetworkBound(), new RetryOptions { Attempts = 3 });
}
catch (HttpRequestException ex)
{
    var attempts = (int)ex.Data[RetryDataKeys.Attempts];                            // 3
    var all = (IReadOnlyList<Exception>)ex.Data[RetryDataKeys.Exceptions];         // one per attempt, in order
}
```

## Logging

Construct `EasyRetry` with an `ILogger<EasyRetry>` (the DI registration above does this for you) and set `EnableLogging = true` in the options. Each failed attempt that will be retried is logged at `Warning`, the final failure at `Error`, both with the exception attached. Without a logger, or with `EnableLogging = false`, nothing is logged.

```c#
var easyRetry = new EasyRetry(loggerFactory.CreateLogger<EasyRetry>());
await easyRetry.Retry(async () => await Task_NetworkBound(), new RetryOptions { EnableLogging = true });
```

For metrics or tracing use `OnRetry` instead of, or alongside, logging. It runs once before each wait with the exception (null when `RetryOnResult` rejected the result), the attempt that failed and the delay about to be waited.

```c#
var options = new RetryOptions { Attempts = 5 };
options.OnRetry = (ex, attempt, delay) => retryCounter.Add(1);
await _easyRetry.Retry(() => Task_NetworkBound(), options);
```

## Presets

Three factories cover the common delay shapes: `RetryOptions.Constant(int attempts, TimeSpan delay)`, `RetryOptions.Linear(int attempts, TimeSpan baseDelay)` and `RetryOptions.Exponential(int attempts, TimeSpan baseDelay, TimeSpan? maxDelay = null, bool useJitter = true)`. They only set the delay-related properties; the result is an ordinary `RetryOptions`, so set anything else on it afterwards.

```c#
var o = RetryOptions.Exponential(5, TimeSpan.FromSeconds(1));
o.EnableLogging = true;
```

For scripts and one-off code there is `EasyRetry.Default`, a shared instance without logging

```c#
await EasyRetry.Default.Retry(() => DoThing());
```

## Changelog

### 2.2.0

All additive; no behaviour change for existing code.

- Result-based retry via `RetryOnResult`.
- `MaxTotalDuration` time budget.
- Attempt- and token-aware extension overloads in `EasyRetryExtensions`.
- `OnRetry` hook for metrics and tracing.
- `DelayOverride` for server-provided delays such as Retry-After.
- `RetryOptions.Constant` / `Linear` / `Exponential` presets.
- `EasyRetry.Default` shared instance.

### 2.1.0

Everything is source compatible; existing calls compile and behave the same unless noted.

- `DelayBeforeFirstTry` is now applied once, before the first attempt. Previously it was waited before every attempt.
- `DoNotRetryOnTheseExceptionTypes` now also matches derived exception types.
- Invalid options (`Attempts < 1`, negative delays, null delegate or type list) now throw before the first attempt instead of misbehaving.
- Targets `netstandard2.0` / `netstandard2.1` instead of `netcoreapp3.1`.
- New options: `Backoff`, `MaxDelay`, `UseJitter`, `ShouldRetry`, `CancellationToken`.
- The rethrown exception carries `RetryDataKeys.Attempts` and `RetryDataKeys.Exceptions` in `Exception.Data`.

## When to use something else

EasyRetry is deliberately a retry loop, not a resilience framework. If you need circuit breakers, bulkheads, fallbacks, composed pipelines or `HttpClient` handler integration, use Polly or Microsoft.Extensions.Resilience instead.

## License

MIT, see [LICENSE](LICENSE).
