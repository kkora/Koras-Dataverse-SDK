# Koras.Dataverse.FetchXml

A dependency-free fluent FetchXML builder for Microsoft Dataverse with strict, injection-safe
encoding of names and values.

```csharp
var query = FetchXml.For("account")
    .Attributes("name", "revenue")
    .Where(f => f.Eq("statecode", 0).Like("name", "Contoso%"))
    .Link("contact", from: "primarycontactid", to: "contactid",
          l => l.Alias("pc").Attributes("fullname"))
    .OrderBy("name")
    .Top(50)
    .Build();

string xml = query.Xml; // ready for the Web API, the Organization Service, or a plug-in
```

Targets `netstandard2.0`, so it also works inside Dataverse plug-in assemblies. Execute the
resulting queries with the [`Koras.Dataverse`](https://www.nuget.org/packages/Koras.Dataverse)
client or any other Dataverse API surface.

- **License:** MIT · **Publisher:** Koras Technologies
