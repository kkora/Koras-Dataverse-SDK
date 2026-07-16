using System.Reflection;
using Koras.Dataverse;
using Koras.Dataverse.FetchXml;
using NetArchTest.Rules;

namespace Koras.Dataverse.ArchitectureTests;

public class ArchitectureTests
{
    private static readonly Assembly Abstractions = typeof(IDataverseClient).Assembly;
    private static readonly Assembly FetchXmlAssembly = typeof(FetchXmlQuery).Assembly;
    private static readonly Assembly Core = typeof(DataverseClient).Assembly;

    [Fact]
    public void Abstractions_do_not_depend_on_azure_or_extensions()
    {
        TestResult result = Types.InAssembly(Abstractions)
            .ShouldNot()
            .HaveDependencyOnAny("Azure", "Microsoft.Extensions", "Microsoft.Identity")
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Fact]
    public void FetchXml_package_is_dependency_free()
    {
        // Beyond the BCL, the FetchXml assembly must reference nothing.
        IEnumerable<string> references = FetchXmlAssembly.GetReferencedAssemblies().Select(a => a.Name!);
        Assert.All(references, name =>
            Assert.True(
                name.StartsWith("System", StringComparison.Ordinal) || name == "netstandard" || name == "mscorlib",
                $"Unexpected reference: {name}"));
    }

    [Fact]
    public void Abstractions_only_reference_fetchxml_and_the_bcl()
    {
        IEnumerable<string> references = Abstractions.GetReferencedAssemblies().Select(a => a.Name!);
        Assert.All(references, name =>
            Assert.True(
                name.StartsWith("System", StringComparison.Ordinal) || name == "netstandard" || name == "Koras.Dataverse.FetchXml",
                $"Unexpected reference: {name}"));
    }

    [Fact]
    public void Core_does_not_depend_on_organization_service_assemblies()
    {
        TestResult result = Types.InAssembly(Core)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.Xrm", "Microsoft.PowerPlatform", "Microsoft.Crm")
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Fact]
    public void Public_classes_are_sealed_abstract_or_static()
    {
        foreach (Assembly assembly in new[] { Abstractions, FetchXmlAssembly, Core })
        {
            IEnumerable<Type> offenders = assembly.GetExportedTypes()
                .Where(t => t is { IsClass: true, IsSealed: false, IsAbstract: false })
                .Where(t => t != typeof(Errors.DataverseException)); // deliberately inheritable

            Assert.Empty(offenders);
        }
    }

    [Fact]
    public void Public_async_methods_use_the_async_suffix()
    {
        foreach (Assembly assembly in new[] { Abstractions, Core })
        {
            var offenders = assembly.GetExportedTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(m => typeof(Task).IsAssignableFrom(m.ReturnType) || m.ReturnType.Name.StartsWith("ValueTask", StringComparison.Ordinal))
                .Where(m => !m.Name.EndsWith("Async", StringComparison.Ordinal))
                .Where(m => m.Name is not ("DisposeAsync" or "GetAsyncEnumerator"))
                .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
                .ToList();

            Assert.Empty(offenders);
        }
    }

    [Fact]
    public void Public_api_methods_with_io_accept_cancellation_tokens()
    {
        var offenders = typeof(IDataverseClient).GetMethods()
            .Where(m => typeof(Task).IsAssignableFrom(m.ReturnType) || m.ReturnType.Name.StartsWith("IAsyncEnumerable", StringComparison.Ordinal))
            .Where(m => m.GetParameters().All(p => p.ParameterType != typeof(CancellationToken)))
            .Select(m => m.Name)
            .ToList();

        Assert.Empty(offenders);
    }

    private static string Failing(TestResult result) =>
        result.IsSuccessful ? string.Empty : "Violations: " + string.Join(", ", result.FailingTypes.Select(t => t.FullName));
}
