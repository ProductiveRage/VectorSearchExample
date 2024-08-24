using System.Net.Http.Json;

namespace VectorSearchExample;

public sealed class StructChatEmbeddingVectoriser
{
    private readonly HttpClient _httpClient;
    public StructChatEmbeddingVectoriser(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>
    /// This container uses the e5-small-v2 model, which has 384 dimensions
    /// </summary>
    public int Dimensions { get; } = 384;

    /// <summary>
    /// The e5 model is trained to expect a particular prefix be added to the source data - which this method does
    /// </summary>
    public async Task<ReadOnlyMemory<float>> CalculateForDocument(string value, CancellationToken cancellationToken = default) =>
        await Calculate("passage: ", value, cancellationToken);

    /// <summary>
    /// The e5 model is trained to expect a particular prefix be added to query text - which this method does
    /// </summary>
    public async Task<ReadOnlyMemory<float>> CalculateForQuery(string value, CancellationToken cancellationToken = default) =>
        await Calculate("query: ", value, cancellationToken);

    private async Task<ReadOnlyMemory<float>> Calculate(string prefix, string value, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "http://127.0.0.1:8080/embed",
            new { prefix, text = value },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<Response>(cancellationToken: cancellationToken))!.Embedding;
    }

    private sealed record Response(ReadOnlyMemory<float> Embedding);
}