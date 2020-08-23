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

Let's say there is a Http Task which you need to retry in case it fails 

```c#
private async Task Task_NetworkBound()
{
    await new HttpClient().GetStringAsync("https://dotnetfoundation.org");
}
```
In order to retry it after 5 seconds you just need to do as follows

```c#
await Task_NetworkBound().Retry();
```

Or you can use the retry options to customize the behaviour of the retry algorithm as follows 

```c#
await Task_NetworkBound().Retry(new RetryOptions()
    {
        Attempts = 3,
        DelayBetweenRetries = TimeSpan.FromSeconds(3),
        DelayBeforeFirstTry = TimeSpan.FromSeconds(2),
        EnableLogging = true,
        DoNotRetryOnTheseExceptions = new List<Exception>()
        {
            new NullReferenceException()
        }
    }
);
```
#### [NuGet](https://www.nuget.org/packages/EasyRetry) Installation
#### [GitHub](https://github.com/alicommit-malp/Easy-Retry) Source Code 
