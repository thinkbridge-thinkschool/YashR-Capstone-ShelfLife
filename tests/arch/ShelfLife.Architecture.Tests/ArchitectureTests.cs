using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace ShelfLife.Architecture.Tests;

public sealed class ArchitectureTests
{
    private const string DomainNs = "ShelfLife.Catalog.Domain";
    private const string AppNs = "ShelfLife.Catalog.Application";
    private const string InfrastructureNs = "ShelfLife.Catalog.Infrastructure";

    [Fact]
    public void Domain_Should_Not_Reference_Application()
    {
        var result = Types.InNamespace(DomainNs)
            .Should().NotHaveDependencyOn(AppNs)
            .GetResult();
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Domain_Should_Not_Reference_Infrastructure()
    {
        var result = Types.InNamespace(DomainNs)
            .Should().NotHaveDependencyOn(InfrastructureNs)
            .GetResult();
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_Should_Not_Reference_Infrastructure()
    {
        var result = Types.InNamespace(AppNs)
            .Should().NotHaveDependencyOn(InfrastructureNs)
            .GetResult();
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void LendingDomain_Should_Not_Reference_CatalogInfrastructure()
    {
        var result = Types.InNamespace("ShelfLife.Lending.Domain")
            .Should().NotHaveDependencyOn(InfrastructureNs)
            .GetResult();
        result.IsSuccessful.Should().BeTrue();
    }
}
