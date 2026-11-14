using System.Reflection;
using SpeakMate.Application;
using SpeakMate.Domain;
using SpeakMate.Infrastructure;

namespace SpeakMate.UnitTests.Architecture;

/// <summary>
/// Guards the clean-architecture dependency rule: Domain ← Application ← Infrastructure ← Api.
/// A failure here means a layer started depending on something it must not know about.
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(DomainAssembly).Assembly;
    private static readonly Assembly Application = typeof(ApplicationAssembly).Assembly;
    private static readonly Assembly Infrastructure = typeof(InfrastructureAssembly).Assembly;

    private static string[] ReferencedSpeakMateAssemblies(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => name.StartsWith("SpeakMate.", StringComparison.Ordinal))
            .ToArray();

    [Fact]
    public void Domain_DependsOnNoOtherProjectLayer() =>
        Assert.Empty(ReferencedSpeakMateAssemblies(Domain));

    [Fact]
    public void Domain_DoesNotDependOnEntityFrameworkOrAspNet()
    {
        var forbidden = Domain.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                     || n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));

        Assert.Empty(forbidden);
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrApi()
    {
        var references = ReferencedSpeakMateAssemblies(Application);

        Assert.DoesNotContain(Infrastructure.GetName().Name, references);
        Assert.DoesNotContain("SpeakMate.Api", references);
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnApi() =>
        Assert.DoesNotContain("SpeakMate.Api", ReferencedSpeakMateAssemblies(Infrastructure));
}
