using System.Text;
using HtmlAgilityPack;

namespace VectorSearchExample;

public static class HtmlHelpers
{
    public static string ToPlainText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var builder = new StringBuilder();
        foreach (var node in doc.DocumentNode.SelectNodes("//text()"))
        {
            builder.Append(node.InnerText);
        }
        return builder.ToString();
    }
}