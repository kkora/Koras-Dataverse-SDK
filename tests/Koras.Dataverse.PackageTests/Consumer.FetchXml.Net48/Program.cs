using System;
using Koras.Dataverse.FetchXml;

namespace KorasPackageTests.FetchXmlNet48
{
    // Deliberately written in C# 7.3-era syntax: this is what a Dataverse plug-in
    // project on .NET Framework compiles with by default.
    internal static class Program
    {
        internal static int Main()
        {
            FetchXmlQuery query = FetchXml.For("account")
                .Attributes("name", "revenue")
                .Where(f => f.Eq("statecode", 0).Like("name", "Contoso%"))
                .Link("contact", "contactid", "primarycontactid", l => l.Alias("pc").Attributes("fullname"))
                .OrderBy("name")
                .Top(10)
                .Build();

            if (string.IsNullOrEmpty(query.Xml) || !query.Xml.Contains("<fetch"))
            {
                Console.Error.WriteLine("FetchXml output looks wrong: " + query.Xml);
                return 1;
            }

            Console.WriteLine("net48 FetchXml consumer OK (" + query.Xml.Length + " chars)");
            return 0;
        }
    }
}
