using System.Runtime.CompilerServices;
using System.Xml;

namespace VectorSearchExample;

public static class StackOverflowDataDumpReader
{
    public static async IAsyncEnumerable<Post> Read(Stream input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = XmlReader.Create(input, new XmlReaderSettings { Async = true });
        
        if ((await reader.MoveToContentAsync() != XmlNodeType.Element) || (reader.Name != "posts"))
        {
            throw new Exception("Content not of expected format - no 'posts' content found");
        }
        
        while (await reader.ReadAsync())
        {
            if ((reader.NodeType != XmlNodeType.Element) || (reader.Name != "row"))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var postType = (PostType)ReadIntAttribute(reader, "PostTypeId");
            if ((postType != PostType.Question) && (postType != PostType.Answer))
            {
                // Data dumps may contain other post types, but we only want Questions and Answers
                // (see https://meta.stackexchange.com/a/99267/1103031)
                continue;
            }

            yield return new Post(
                ReadIntAttribute(reader, "Id"),
                postType == PostType.Answer ? ReadIntAttribute(reader, "ParentId") : null,
                postType,
                reader.GetAttribute("Title")!,
                reader.GetAttribute("Body")!);
        }
    }

    private static int ReadIntAttribute(XmlReader reader, string name) => int.Parse(reader.GetAttribute(name)!);
}