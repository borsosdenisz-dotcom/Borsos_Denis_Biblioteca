using Microsoft.Extensions.VectorData;

namespace Lab1_MVC.Rag;

public class DocumentChunk
{
    [VectorStoreKey]
    public string Id { get; set; } = "";

    [VectorStoreData(IsIndexed = true)]
    public string Source { get; set; } = "";

    [VectorStoreData]
    public string Title { get; set; } = "";

    [VectorStoreData]
    public string Text { get; set; } = "";

    [VectorStoreVector(768, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public string Embedding { get; set; } = "";
}

// Ce intoarce tool-ul MCP: fragmentul, sursa si scorul hibrid de relevanta.
public record DocumentHit(string Source, string Title, string Text, double Score);
