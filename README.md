# JsonLocalizer

## This is a fork from [aodpi/Anvyl.JsonLocalizer](https://github.com/aodpi/Anvyl.JsonLocalizer)
 
![Build](https://github.com/amoraitis/Anvyl.JsonLocalizer/workflows/Build/badge.svg)
<a href="https://www.nuget.org/packages/AMoraitis.JsonLocalizer/" target="_blank"><img alt="Nuget" src="https://img.shields.io/nuget/v/AMoraitis.JsonLocalizer"></a>

Min suppoered versions:

- .NET Core 3.1
- .NET Standard 2.0

---
This package is an Implementation of `IStringLocalizer` for .net. It uses json files to store localized strings plus the `IDistributedCache` implementation in order to cache out the localized strings. The flow is pretty simple,

* whenever a localized string is requested it is firstly checked in the `IDistributedCache`
* If the value for the key is not found in the cache, it is then read from the json file for the respective `CultureInfo`
* When the value is retrieved from json file and it is valid, it's stored in the cache before returning it to the caller.

In this way the localized strings are cached only when requested and are physically stored in json files only.

## Getting started

In Startup.cs make sure to add the localizer factory and the IStringLocalizer service with it's factory to create objects.

```csharp
services.Configure<JsonLocalizerOptions>(Configuration.GetSection(nameof(JsonLocalizerOptions)));
services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();
services.AddTransient(serviceProvider =>
{
    var factory = serviceProvider.GetRequiredService<IStringLocalizerFactory>();
    return factory.Create(null);
});
```

## Why this fork?

This fork is modified to use localized json files in the format: {namespace}.{locale}.json, so you can have multiple namespaces in different json files.

Example for your monkey app:

```js
// Monkey.en-US.json
{
 "Label": "Monkey",
 "Banana": "Banana"
}
```

To get the "Monkey" text out of you localization resources, you can:

```cs
 // this will look into the Monkey file
 Assert.Equal("Monkey", _localizer["Monkey.Label"])
```
