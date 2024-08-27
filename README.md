# A C# Semantic Indexing and Search example

This indexes data from [workplace.stackexchange.com](https://workplace.stackexchange.com/) using the [e5-small-v2](https://huggingface.co/intfloat/e5-small-v2) model from Hugging Face, hosted in a container published at [github.com/struct-chat/embedding](https://github.com/struct-chat/embedding), stored in Postgres using the [PgVector](https://github.com/pgvector/pgvector) extension.

When it runs, it will construct the index and then perform the searches that are defined in code in Program.cs - to change the searches performed, alter the Queries array:

```
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
```

Before running, you will have to have a local Postgres database available on port 5432, with "postgres" as the database name, user name, and password (and it will require that the PgVector extension be enabled).

The easiest way to do so is with that is, imo, to use the "ankane/pgvector" docker container makes this simple:

```
docker run --name postgres-with-pgvector -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d ankane/pgvector
```

The embedding model also needs to be running before this project is started - it requires a vectoriser endpoint at http://127.0.0.1:8080/embed that will take prefix and text values and return response with an embedding property that is an array of values.

Cloning the repository from [github.com/struct-chat/embedding](https://github.com/struct-chat/embedding) and then building and running it will provide this:

```
docker build -t struct-chat.
docker run -p 8080:8080 struct-chat
```

Finally, you will need a file "workplace stackexchange Posts.xml" to be in the solution root - you can download this by creating an account at [workplace.stackexchange.com](https://workplace.stackexchange.com) and then going to your profile page, settings, data dump access and downloading a compressed file and extracting "workplace stackexchange Posts.xml" from it.

One day I might streamline this setup, rather than requiring you to set up the Docker containers individually, but I haven't made time to do so yet!
