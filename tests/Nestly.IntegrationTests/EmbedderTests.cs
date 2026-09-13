using Microsoft.Extensions.DependencyInjection;
using Nestly.Search.Embedding;
using Nestly.Search.Indexing;

namespace Nestly.IntegrationTests;

[Collection(SharedElasticsearch.Name)]
public sealed class EmbedderTests(ElasticsearchFixture fixture)
{
    private IListingEmbedder Embedder => fixture.Services.GetRequiredService<IListingEmbedder>();

    [Fact]
    public void Embedder_Dimensions_MatchTheWidthTheIndexMapsForTheVector()
    {
        // The mapping's dims and the model's output width are declared in different files and
        // nothing made them agree: swapping in a model of another size would index cleanly and
        // then fail every kNN query at search time.
        RequiresVectors();

        // Act
        var dimensions = Embedder.Dimensions;

        // Assert
        Assert.Equal(ListingIndex.VectorDimensions, dimensions);
    }

    [Fact]
    public void Embed_ABatch_MatchesEmbeddingEachTextOnItsOwn()
    {
        // Padding squares off a batch, so a mask applied a row out would leave batched vectors
        // subtly different from single ones -- and only the seeder embeds in batches, so the
        // index would disagree with the query path rather than fail.
        RequiresVectors();

        string[] texts = ["Sunny studio near Prospect Park", "Loft", string.Empty];

        // Act
        var batched = Embedder.Embed(texts);

        // Assert
        for (var index = 0; index < texts.Length; index++)
        {
            Assert.Equal(Embedder.Embed(texts[index]), batched[index]);
        }
    }

    private void RequiresVectors()
    {
        Assert.SkipUnless(fixture.HasVectors, "the ONNX model is not present; run tools/Nestly.ModelFetcher");
    }
}
