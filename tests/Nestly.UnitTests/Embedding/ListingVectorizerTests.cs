using Nestly.Domain;
using Nestly.Search.Embedding;
using Nestly.Seeder.Embedding;

namespace Nestly.UnitTests.Embedding;

public sealed class ListingVectorizerTests
{
    [Fact]
    public void Apply_AnyListings_AttachesAVectorToEach()
    {
        // Arrange
        var embedder = new RecordingEmbedder();
        var vectorizer = new ListingVectorizer(embedder, batchSize: 2);

        // Act
        var vectorized = vectorizer.Apply(Listings(5)).ToArray();

        // Assert
        Assert.Equal(5, vectorized.Length);
        Assert.All(vectorized, listing => Assert.NotNull(listing.DescriptionVector));
        Assert.Equal(5, vectorizer.Embedded);
    }

    [Fact]
    public void Apply_SeveralBatches_KeepsEachVectorWithItsOwnListing()
    {
        // Off-by-one inside the batch loop would pair the right count of vectors with the wrong
        // documents, which no total would ever reveal.

        // Arrange
        var vectorizer = new ListingVectorizer(new RecordingEmbedder(), batchSize: 2);

        // Act
        var vectorized = vectorizer.Apply(Listings(5)).ToArray();

        // Assert
        Assert.All(vectorized, listing =>
            Assert.Equal((float)listing.Description.Length, listing.DescriptionVector![0]));
    }

    [Fact]
    public void Apply_MoreListingsThanTheBatchSize_EmbedsInBatches()
    {
        // A batch is several times faster per item than the same texts one at a time.

        // Arrange
        var embedder = new RecordingEmbedder();
        var vectorizer = new ListingVectorizer(embedder, batchSize: 2);

        // Act
        _ = vectorizer.Apply(Listings(5)).ToArray();

        // Assert -- 2, 2, 1.
        Assert.Equal([2, 2, 1], embedder.BatchSizes);
    }

    [Fact]
    public void Apply_NotYetEnumerated_EmbedsNothingUntilPulled()
    {
        // BulkAll pulls from this stream as it fills each request; embedding everything up front
        // would hold five thousand vectors in memory and delay the first document.

        // Arrange
        var embedder = new RecordingEmbedder();
        var vectorizer = new ListingVectorizer(embedder, batchSize: 2);

        // Act
        var stream = vectorizer.Apply(Listings(6));
        var beforeEnumerating = embedder.BatchSizes.Count;
        _ = stream.Take(1).ToArray();

        // Assert
        Assert.Equal(0, beforeEnumerating);
        Assert.Equal([2], embedder.BatchSizes);
    }

    [Fact]
    public void Apply_NullSource_ThrowsAtTheCallRatherThanTheFirstItem()
    {
        // Arrange
        var vectorizer = new ListingVectorizer(new RecordingEmbedder(), batchSize: 2);

        // Act + Assert -- not deferred to the first MoveNext, which is somewhere else by then.
        Assert.Throws<ArgumentNullException>(() => vectorizer.Apply(null!));
    }

    [Fact]
    public void Apply_EmptySource_EmbedsNothing()
    {
        // Arrange
        var embedder = new RecordingEmbedder();
        var vectorizer = new ListingVectorizer(embedder, batchSize: 2);

        // Act
        var vectorized = vectorizer.Apply([]).ToArray();

        // Assert
        Assert.Empty(vectorized);
        Assert.Empty(embedder.BatchSizes);
        Assert.Equal(0, vectorizer.Embedded);
    }

    private static IEnumerable<Listing> Listings(int count) =>
        Enumerable.Range(1, count).Select(index => new Listing
        {
            Id = index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Title = $"Listing {index}",

            // Length varies per listing, so the fake can return a vector that identifies its input.
            Description = new string('x', index),
            Neighborhood = "Bushwick",
            Borough = "Brooklyn",
            Location = new GeoPoint(40.7, -73.9),
            PropertyType = "Entire rental unit",
            RoomType = "Entire home/apt",
            PricePerNight = 100,
            MonthlyRent = 3000,
            Bedrooms = 1,
            Bathrooms = 1m,
            Accommodates = 2,
            Amenities = ["Wifi"],
            MinimumNights = 30,
        });

    /// <summary>Returns the input's length as its vector, so a mispaired batch is visible.</summary>
    private sealed class RecordingEmbedder : IListingEmbedder
    {
        public List<int> BatchSizes { get; } = [];

        public int Dimensions => 1;

        public float[] Embed(string text) => [text.Length];

        public IReadOnlyList<float[]> Embed(IReadOnlyList<string> texts)
        {
            BatchSizes.Add(texts.Count);

            return [.. texts.Select(text => new[] { (float)text.Length })];
        }
    }
}
