using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace Nestly.Search.Embedding;

/// <summary>
/// all-MiniLM-L6-v2 running in-process on the CPU.
/// </summary>
/// <remarks>
/// The alternatives were a Python sidecar or Elasticsearch's own ELSER, which needs a licence
/// above Basic and would make this demo expire. Thread-safe: <see cref="InferenceSession"/> and
/// the tokenizer are both safe for concurrent Run calls, so this is registered as a singleton.
/// </remarks>
internal sealed class OnnxListingEmbedder : IListingEmbedder, IDisposable
{
    private const string InputIds = "input_ids";
    private const string AttentionMask = "attention_mask";
    private const string TokenTypeIds = "token_type_ids";

    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;
    private readonly int _maxTokens;
    private readonly bool _needsTokenTypeIds;

    public OnnxListingEmbedder(IOptions<EmbeddingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var settings = options.Value;
        var directory = ModelLocation.Resolve(settings.ModelDirectory);
        var model = Path.Combine(directory, settings.ModelFile);
        var vocab = Path.Combine(directory, settings.VocabFile);

        if (!File.Exists(model) || !File.Exists(vocab))
        {
            throw new InvalidOperationException(
                $"The embedding model is missing from {directory}. Run: dotnet run --project tools/Nestly.ModelFetcher");
        }

        _session = new InferenceSession(model);

        using var vocabStream = File.OpenRead(vocab);

        _tokenizer = BertTokenizer.Create(vocabStream);
        _maxTokens = settings.MaxTokens;

        // Read from the graph rather than assumed: exports of this model disagree about whether
        // they take token_type_ids, and about whether the hidden states are called
        // last_hidden_state or output_0.
        var output = _session.OutputMetadata.Keys.First();

        _needsTokenTypeIds = _session.InputMetadata.ContainsKey(TokenTypeIds);

        Dimensions = _session.OutputMetadata[output].Dimensions[^1];
    }

    public int Dimensions { get; }

    public float[] Embed(string text) => Embed([text])[0];

    public IReadOnlyList<float[]> Embed(IReadOnlyList<string> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
        {
            return [];
        }

        var encoded = texts.Select(Encode).ToArray();
        var width = encoded.Max(ids => ids.Length);

        var inputIds = new DenseTensor<long>([texts.Count, width]);
        var mask = new DenseTensor<long>([texts.Count, width]);
        var tokenTypes = new DenseTensor<long>([texts.Count, width]);

        for (var row = 0; row < encoded.Length; row++)
        {
            for (var column = 0; column < encoded[row].Length; column++)
            {
                inputIds[row, column] = encoded[row][column];

                // Padding is zeroed and masked out, so a short description is not diluted by the
                // filler that squares off the batch.
                mask[row, column] = 1;
            }
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(InputIds, inputIds),
            NamedOnnxValue.CreateFromTensor(AttentionMask, mask),
        };

        if (_needsTokenTypeIds)
        {
            inputs.Add(NamedOnnxValue.CreateFromTensor(TokenTypeIds, tokenTypes));
        }

        using var results = _session.Run(inputs);

        return Pool(results[0].AsTensor<float>(), mask, texts.Count, width, Dimensions);
    }

    public void Dispose() => _session.Dispose();

    /// <summary>
    /// Mean pooling over the unmasked tokens, then L2 normalisation.
    /// </summary>
    // Both halves are required, not stylistic. The model emits one vector per token and
    // sentence-transformers defines the sentence vector as their mask-weighted mean; and the index
    // uses cosine similarity, which is a dot product only once the vectors are unit length.
    private static float[][] Pool(Tensor<float> hidden, DenseTensor<long> mask, int rows, int width, int dimensions)
    {
        var pooled = new float[rows][];

        for (var row = 0; row < rows; row++)
        {
            var vector = new float[dimensions];
            var tokens = 0;

            for (var column = 0; column < width; column++)
            {
                if (mask[row, column] == 0)
                {
                    continue;
                }

                tokens++;

                for (var dimension = 0; dimension < dimensions; dimension++)
                {
                    vector[dimension] += hidden[row, column, dimension];
                }
            }

            pooled[row] = Normalize(vector, Math.Max(tokens, 1));
        }

        return pooled;
    }

    private static float[] Normalize(float[] vector, int tokens)
    {
        double sumOfSquares = 0;

        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] /= tokens;
            sumOfSquares += vector[index] * (double)vector[index];
        }

        var length = Math.Sqrt(sumOfSquares);

        if (length == 0)
        {
            return vector;
        }

        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] = (float)(vector[index] / length);
        }

        return vector;
    }

    private long[] Encode(string text)
    {
        var ids = _tokenizer.EncodeToIds(text ?? string.Empty, _maxTokens, out _, out _);

        return [.. ids.Select(id => (long)id)];
    }
}
