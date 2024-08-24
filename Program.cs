// This requires there to be a local Postgres database responding on port 5432, with "postgres" database, username, and password that has the PgVector extension enabled.
// Using the "ankane/pgvector" docker container makes this simple:
//
//   docker run --name postgres-with-pgvector -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d ankane/pgvector
//
// .. and it requires a vectoriser endpoint to be running at http://127.0.0.1:8080/embed that will take prefix and text values and return response with an embedding
// property that is an array of values. Cloning the repository from https://github.com/struct-chat/embedding and then building and running it will provide this:
//
//    docker build -t struct-chat.
//    docker run -p 8080:8080 struct-chat
//
// It also requires a file "workplace stackexchange Posts.xml" to be in the solution root that you can download by creating an account at https://workplace.stackexchange.com
// and then going to your profile page, settings, data dump access and downloading a compressed file and extracting "workplace stackexchange Posts.xml" from it.

using System.Text.RegularExpressions;
using VectorSearchExample;

var options = new
{
    SourceDataFilePath = "workplace stackexchange Posts.xml",
    SiteDomain = "https://workplace.stackexchange.com",
    Queries = new[]
    {
        "what is the best office chair?",
        "how can i train my parrot to swear for comedic effect?"
    }
};

using var httpClient = new HttpClient();
var vectoriser = new StructChatEmbeddingVectoriser(httpClient);

var connectionString = "Server=localhost;Port=5432;User Id=postgres;Password=postgres;Database=postgres;";
await using var dataStore = await PostgresPostDataStore.Get(connectionString, vectoriser.Dimensions);

// Ensure that the Postgres database is populated (with text Post data and embedding vectors)
await ImportData(dataStore, vectoriser, options.SourceDataFilePath);

// Run the test queries against it
foreach (var query in options.Queries)
{
    await ShowBestMatchesFor(dataStore, vectoriser, options.SiteDomain, query);
}

Console.WriteLine("Done! Press [Enter] to terminate..");
Console.ReadLine();

static async Task ShowBestMatchesFor(PostgresPostDataStore dataStore, StructChatEmbeddingVectoriser vectoriser, string siteDomain, string query, int maxNumberOfResults = 3)
{
    Console.WriteLine("===================================----------------------------");
    Console.WriteLine("QUERY: " + query);
    Console.WriteLine("===================================----------------------------");
    Console.WriteLine();

    var queryEmbedding = await vectoriser.CalculateForQuery(query);
    await foreach (var (post, distance) in dataStore.SearchForAnswers(queryEmbedding, maxNumberOfResults))
    {
        var url = siteDomain + "/questions/";
        if (post.ParentId is null)
        {
            url += post.Id;
        }
        else
        {
            url += $"{post.ParentId}/a/{post.Id}";
        }

        var bodyPlainText = Regex.Replace(HtmlHelpers.ToPlainText(post.BodyHtml), @"\s+", " ").Trim();
        Console.WriteLine($"POST {post.Id} Distance: {distance}");
        if (post.Title is not null)
        {
            Console.WriteLine(post.Title);
        }
        Console.WriteLine(url);
        Console.WriteLine(bodyPlainText[0..Math.Min(80, bodyPlainText.Length)] + "..");
        Console.WriteLine();
    }
}

static async Task ImportData(PostgresPostDataStore dataStore, StructChatEmbeddingVectoriser vectoriser, string sourceDataFilePath)
{
    if (!File.Exists(sourceDataFilePath))
    {
        throw new ArgumentException("Data file does not exist: " + sourceDataFilePath);
    }

    using var stream = File.OpenRead(sourceDataFilePath);
    var numberOfImportedRows = 0;
    var numberOfAlreadyImportedRows = 0;
    await foreach (var post in StackOverflowDataDumpReader.Read(stream))
    {
        // In case we started populating the database earlier and have then come back and want to pick back up, skip any posts
        // that have already been imported so that we don't waste time vectorising content that we don't need (this isn't the
        // most efficient way to import large amounts of data, but it's fine for what we want to do here)
        if (await dataStore.DoesPostExist(post.Id))
        {
            numberOfAlreadyImportedRows++;
            continue;
        }

        var contentForEmedding = (post.Title is null ? "" : $"{post.Title}\n\n") + HtmlHelpers.ToPlainText(post.BodyHtml);

        var embedding = await vectoriser.CalculateForDocument(contentForEmedding);

        await dataStore.Insert(post, embedding);
        numberOfImportedRows++;

        Console.WriteLine("Imported count: " + numberOfImportedRows);
    }
}