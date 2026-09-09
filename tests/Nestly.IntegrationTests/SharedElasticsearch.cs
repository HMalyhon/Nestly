namespace Nestly.IntegrationTests;

/// <summary>Shares the one container across every test class in the assembly.</summary>
// Named for what it is rather than the usual "...Collection", which reads to the analysers as a
// collection type. Only the string in the attribute has to match what the tests declare.
[CollectionDefinition(Name)]
public sealed class SharedElasticsearch : ICollectionFixture<ElasticsearchFixture>
{
    public const string Name = "elasticsearch";
}
