using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Analysis;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

namespace Nestly.Search.Indexing;

/// <summary>
/// The <c>listings</c> index definition: analysis chain and field mappings, written in code so
/// the schema is reviewable in a diff and identical on every machine that runs the seeder.
/// </summary>
/// <remarks>
/// Nothing here relies on dynamic mapping. <c>dynamic: strict</c> means a document carrying an
/// unmapped field is rejected outright, which turns a renamed property into an immediate error
/// instead of a field that quietly stops being searchable.
/// </remarks>
public static class ListingIndex
{
    /// <summary>Dimensions of all-MiniLM-L6-v2 output. The embedder and the mapping must agree.</summary>
    public const int VectorDimensions = 384;

    private const string TextAnalyzer = "nestly_text";
    private const string SearchAnalyzer = "nestly_text_search";

    /// <summary>
    /// Applied at search time only, so the list can be edited and the index reloaded without a
    /// reindex -- index-time synonyms would bake the current list into every stored document.
    /// The graph variant is what makes the multi-word expansions work: "ues" has to become three
    /// tokens in one position, which the plain synonym filter cannot represent.
    /// </summary>
    private static readonly string[] Synonyms =
    [
        "apt, apartment, flat",
        "condo, condominium",
        "ac, air conditioning",
        "ues => upper east side",
        "uws => upper west side",
        "les => lower east side",
        "bk => brooklyn",
        "bx => bronx",
    ];

    public static CreateIndexRequest CreateRequest(IndexName index) => new(index)
    {
        Settings = Settings(),
        Mappings = Mappings(),
    };

    private static IndexSettings Settings() => new()
    {
        // One shard because the dataset is 5,000 documents. Splitting it would add a
        // scatter-gather for nothing and make scores less stable, since term frequencies are
        // computed per shard.
        NumberOfShards = 1,

        // Zero replicas keeps a single-node cluster green instead of permanently yellow with an
        // unassignable replica. A real deployment would raise this.
        NumberOfReplicas = 0,

        Analysis = new IndexSettingsAnalysis
        {
            TokenFilters = new TokenFilters
            {
                { "english_stop", new StopTokenFilter { Stopwords = StopWordLanguage.English } },
                { "english_stemmer", new StemmerTokenFilter { Language = "english" } },
                { "nestly_synonyms", new SynonymGraphTokenFilter { Synonyms = Synonyms } },
            },
            Analyzers = new Analyzers
            {
                {
                    TextAnalyzer,
                    new CustomAnalyzer
                    {
                        Tokenizer = "standard",
                        Filter = ["lowercase", "english_stop", "english_stemmer"],
                    }
                },
                {
                    SearchAnalyzer,
                    new CustomAnalyzer
                    {
                        Tokenizer = "standard",

                        // Synonyms sit before the stemmer so their output is stemmed too:
                        // "condominium" from an expansion reduces to the same stem as the
                        // indexed term, and the match survives.
                        Filter = ["lowercase", "nestly_synonyms", "english_stop", "english_stemmer"],
                    }
                },
            },
        },
    };

    private static TypeMapping Mappings()
    {
        var properties = new Properties();

        properties.Add(ListingFields.Id, new KeywordProperty());

        properties.Add(ListingFields.Title, new TextProperty
        {
            Analyzer = TextAnalyzer,
            SearchAnalyzer = SearchAnalyzer,
            Fields = new Properties
            {
                // search_as_you_type builds shingled sub-fields at index time, which is what
                // lets a half-typed word match without a prefix query walking the term
                // dictionary on every keystroke. It keeps the standard analyzer rather than
                // inheriting the parent's: stemming a half-typed word truncates the prefix the
                // shingles exist to match.
                { "sayt", new SearchAsYouTypeProperty() },
                { "keyword", new KeywordProperty { IgnoreAbove = 256 } },
            },
        });

        properties.Add(ListingFields.Description, new TextProperty
        {
            Analyzer = TextAnalyzer,
            SearchAnalyzer = SearchAnalyzer,
        });

        properties.Add(ListingFields.DescriptionVector, new DenseVectorProperty
        {
            Dims = VectorDimensions,
            Index = true,

            // The embedder L2-normalizes its output, so cosine and dot product rank identically
            // here. Cosine is named anyway, because it stays correct if the embedder ever stops
            // normalizing.
            Similarity = DenseVectorSimilarity.Cosine,

            // Elasticsearch 9 defaults 384-dimension vectors to bbq_hnsw, which stores one bit
            // per dimension and rescores the top candidates to recover accuracy. That trade is
            // made for indexes whose raw vectors will not fit in memory; 5,000 x 384 floats is
            // 7 MB, so there is nothing to save and no reason to accept approximate distances in
            // the one part of the demo whose whole point is showing semantic recall.
            IndexOptions = new DenseVectorIndexOptions { Type = DenseVectorIndexOptionsType.Hnsw },
        });

        properties.Add(ListingFields.Neighborhood, new KeywordProperty
        {
            // Keyword for exact filtering and facet counts, with two sub-fields off the same
            // source value: analyzed text so "east village" matches in free-text search, and a
            // completion field for the autocomplete endpoint. One value, three access patterns,
            // no duplicated source data.
            Fields = new Properties
            {
                { "text", new TextProperty { Analyzer = TextAnalyzer, SearchAnalyzer = SearchAnalyzer } },
                { "suggest", new CompletionProperty() },
            },
        });

        properties.Add(ListingFields.Borough, new KeywordProperty());
        properties.Add(ListingFields.PropertyType, new KeywordProperty());
        properties.Add(ListingFields.RoomType, new KeywordProperty());
        properties.Add(ListingFields.Amenities, new KeywordProperty());

        properties.Add(ListingFields.Location, new GeoPointProperty());

        properties.Add(ListingFields.PricePerNight, new IntegerNumberProperty());
        properties.Add(ListingFields.MonthlyRent, new IntegerNumberProperty());

        // Narrow numeric types on purpose: bedrooms never reaches 255 and review scores carry
        // one decimal place, so byte and half_float cost a fraction of the doc values a double
        // would and lose nothing the UI can display.
        properties.Add(ListingFields.Bedrooms, new ByteNumberProperty());
        properties.Add(ListingFields.Accommodates, new ByteNumberProperty());
        properties.Add(ListingFields.Bathrooms, new HalfFloatNumberProperty());
        properties.Add(ListingFields.ReviewScore, new HalfFloatNumberProperty());
        properties.Add(ListingFields.MinimumNights, new ShortNumberProperty());

        properties.Add(ListingFields.LastReviewedAt, new DateProperty());

        return new TypeMapping
        {
            Dynamic = DynamicMapping.Strict,
            Properties = properties,
        };
    }
}
