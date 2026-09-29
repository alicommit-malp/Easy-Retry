# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

EasyRetry is a small C# NuGet package (`EasyRetry` on nuget.org, version 2.2.0) that retries a delegate
(`Func<Task<T>>`, `Func<Task>`, or `Action`) according to a `RetryOptions` policy. Two projects in the
solution: the library `src/EasyRetry` (targets `netstandard2.0;netstandard2.1`, nullable enabled, XML
docs generated) and an NUnit 4 test project `test/EasyRetryTest` (targets `net8.0`).

## Commands

```bash
dotnet build                                   # builds both projects; also produces the .nupkg
dotnet build --no-incremental -warnaserror     # what CI runs; XML-doc and nullable warnings fail it
dotnet test                                    # full suite, deterministic, no network, < 2 s
dotnet test --filter "FullyQualifiedName~RetryCancellationTests"          # one test class
dotnet test --filter "FullyQualifiedName~DelayBeforeFirstTry_AppliedOnce" # one test
dotnet pack src/EasyRetry/EasyRetry.csproj -c Release -o artifacts        # package with README
```

CI (`.github/workflows/ci.yml`) runs restore, build with `-warnaserror`, test, pack on push and PR to
`master`. Every public type and member needs a `/// <summary>` or the Release build fails.

Release: bump `PackageVersion` in `src/EasyRetry/EasyRetry.csproj`, add a changelog entry to README, then
push a tag `v<version>` (for example `v2.2.0`). The `publish` job runs only on `v*` tags, fails if the tag
does not equal `PackageVersion`, and pushes the `.nupkg` and `.snupkg` to nuget.org via Trusted
Publishing: `NuGet/login@v1` exchanges the job's GitHub OIDC token (`id-token: write`) for a one-hour API
key, so there is no long-lived NuGet key anywhere. Requirements outside the repo: a Trusted Publishing
policy on nuget.org (owner `alicommit-malp`, repository `Easy-Retry`, workflow file `ci.yml`, environment
`nuget` optional). The `user` input of `NuGet/login` is the nuget.org profile name that owns the package,
`AliTabryzy`, written directly in the workflow because a profile name is public.

## Architecture

- `src/EasyRetry/EasyRetry.cs` is the implementation. The two async overloads share one private
  `RetryAsyncCore<T>`; the `Func<Task>` overload adapts to it by returning a dummy value with
  `checkResult: false` so `RetryOnResult` never sees it. The `Action` overload has its own synchronous
  loop because blocking on a Task was the thing being removed. Both loops call the same helpers, and any
  change to retry semantics belongs in those helpers, not in the loops:
  - `Validate(options)` throws `ArgumentOutOfRangeException` / `ArgumentException` for bad options and
    returns a `Type[]` snapshot of `DoNotRetryOnTheseExceptionTypes` so mutation during retries is safe.
  - `ShouldRetry(ex, attempt, options, snapshot)` is the single decision point: attempts left, not a
    cancellation of the caller's own token, not an instance of a listed type (`IsInstanceOfType`, so
    derived types match), and the optional `options.ShouldRetry` predicate.
  - `TryPlanRetry(ex, attempt, options, snapshot, elapsed, out delay)` wraps `ShouldRetry` (skipped
    for result-based failures where `ex` is null), then `ResolveDelay`, then `WithinBudget` against
    `MaxTotalDuration`. The `DelayOverride` is therefore consulted before the budget check.
  - `GetDelay(failedAttempt, options)` is `internal static` (visible to the test project via
    `InternalsVisibleTo`) and computes Constant / Linear / Exponential backoff in double milliseconds,
    applies ±20% jitter, then clamps to `MaxDelay` and to `int.MaxValue` ms because `Task.Delay` and
    `WaitHandle.WaitOne` reject anything larger. `ResolveDelay` applies `DelayOverride` on top: never
    jittered, still capped, negative throws.
- Result-based retry (`RetryOnResult`) only runs for `Func<Task<T>>`. A bad result on the last attempt
  is returned, not thrown, and logs at Warning via `GiveUpOnResult`. `OnRetry` and `DelayOverride`
  receive a null exception for result failures.
- A `Stopwatch` starts at the top of every public method, before validation, so `MaxTotalDuration`
  includes `DelayBeforeFirstTry`. The budget never aborts an in-flight attempt.
- `src/EasyRetry/EasyRetryExtensions.cs` holds the attempt-aware overloads taking
  `Func<int, CancellationToken, Task<T>>` and friends. They are built only on `IEasyRetry` members via a
  closure counter, so they work for any implementation. Keep it that way; do not reach into `EasyRetry`.
- `RetryOptions.Constant/Linear/Exponential` presets set only the delay-related properties and do not
  validate. `EasyRetry.Default` is a shared no-logging instance.
- Delays sit outside the `try` so a delay failure is never treated as a failed attempt.
  `DelayBeforeFirstTry` runs once before the loop; the between-retry delay is computed in the catch of
  attempt N and slept at the top of attempt N+1, so the logged delay equals the slept delay even with
  jitter.
- Cancellation lives on `RetryOptions.CancellationToken`, not on the method signatures, so
  `IEasyRetry` keeps its three original members and hand-written implementers do not break. An
  `OperationCanceledException` is only treated as cancellation when that token is actually cancelled.
  One thrown while the token is not cancelled (an `HttpClient` timeout) is retried like any other
  exception. This is deliberate; do not add a blanket "never retry OCE" rule.
- The exception that finally propagates is the original instance, rethrown with `throw;`. `GiveUp`
  enriches `ex.Data` with `RetryDataKeys.Attempts` and `RetryDataKeys.Exceptions`. Do not wrap it in a
  new exception type; callers catch the original type.
- Logging goes through `ILogger<EasyRetry>` with structured templates and the exception parameter.
  The parameterless constructor uses `NullLogger`, so `_logger` is never null. `EnableLogging` still
  gates all logging for compatibility with 2.0.x.
- `RetryOptions.Attempts` means total invocations, not retries, and `Attempts = 0` is rejected.
  `DoNotRetryOnTheseExceptionTypes` stays a settable `List<Type>` for compatibility; do not make it
  read-only or `init`.

## Tests

All tests (148) are in-memory with invocation counters and 1-50 ms delays. Timing tests assert generous
bounds (for example under 300 ms where the old bug would take over 450 ms) rather than exact values.
`RecordingLogger.cs` captures `(LogLevel, message, exception)` tuples for the logging tests.
`RetryBackoffTests` and `DelayOverrideTests` call the internal `GetDelay` / `ResolveDelay` directly to
assert exact values. `AttemptAwareOverloadTests` include a hand-written `IEasyRetry` fake to prove the
extension methods depend only on the interface.

Test-side gotcha: a lambda whose body always throws binds to the `Func<Task>` overload, not `Action`,
because C# prefers the non-void delegate. Use an explicit `Action` local when testing the sync path.
