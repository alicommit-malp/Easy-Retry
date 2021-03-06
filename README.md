# Async/Await easy retry in c#

In Asynchronous programming in some cases we need to retry a method if it fails. [Easy-Retry](https://www.nuget.org/packages/EasyRetry) can provide this functionality with ease :) 


#### [NuGet](https://www.nuget.org/packages/EasyRetry) Installation
#### [GitHub](https://github.com/alicommit-malp/Easy-Retry) Source Code 

```
.Net CLI
dotnet add package EasyRetry

Package Manager
Install-Package EasyRetry

```

## Usage

Let's say there is a HTTP Task which you need to retry in case it fails 

```c#
private async Task Task_NetworkBound()
{
    await new HttpClient().GetStringAsync("https://dotnetfoundation.org");
}
```
In order to retry it after 5 seconds you just need to do as follows

```c#
//With DI
await _easyRetry.Retry(async () => await Task_NetworkBound());

//Without DI
await new EasyRetry().Retry(async () => await Task_NetworkBound());
```

Or you can use the retry options to customize the behavior of the retry algorithm as follows 

```c#
await _easyRetry.Retry(async () => await Task_NetworkBound()
    , new RetryOptions()
    {
        Attempts = 3,
        DelayBetweenRetries = TimeSpan.FromSeconds(3),
        DelayBeforeFirstTry = TimeSpan.FromSeconds(2),
        EnableLogging = true,
        DoNotRetryOnTheseExceptionTypes = new List<Type>()
        {
            typeof(NullReferenceException)
        }
    });
```
#### [NuGet](https://www.nuget.org/packages/EasyRetry) Installation
#### [GitHub](https://github.com/alicommit-malp/Easy-Retry) Source Code 
