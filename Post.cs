namespace VectorSearchExample;

/// <summary>
/// Title will have a value if it's a Question but not if it's an Answer
/// ParentId will have a value if's an Answer but not if it's a Question
/// </summary>
public sealed record Post(int Id, int? ParentId, PostType Type, string? Title, string BodyHtml);