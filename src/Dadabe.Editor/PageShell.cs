namespace Dadabe.Editor;

public static class PageShell
{
    public const string Header = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>Dadabe Editor</title>
            <link href="https://cdn.jsdelivr.net/npm/beercss@4.0.21/dist/cdn/beer.min.css" rel="stylesheet">
            <script type="module" src="https://cdn.jsdelivr.net/npm/beercss@4.0.21/dist/cdn/beer.min.js"></script>
            <script type="module" src="https://cdn.jsdelivr.net/npm/material-dynamic-colors@1.1.4/dist/cdn/material-dynamic-colors.min.js"></script>
            <script src="https://unpkg.com/htmx.org@2/dist/htmx.min.js"></script>
            <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined" />
            <link rel="stylesheet" href="/app.css" />
        </head>
        <body>
          <nav class="left drawer l">
            <header>
              <nav>
                <i>music_note</i>
                <h6 class="small">Dadabe Editor</h6>
              </nav>
            </header>
            <a href="/dashboard"><i>dashboard</i><div>Dashboard</div></a>
            <a href="/progressions"><i>queue_music</i><div>Progressions</div></a>
            <a href="/predictions"><i>auto_awesome</i><div>Predictions</div></a>
            <a href="/tunings"><i>tune</i><div>Tunings</div></a>
            <a href="/reference"><i>library_books</i><div>Reference</div></a>
            <a href="/voicings"><i>voice_chat</i><div>Voicings</div></a>
          </nav>
          <main class="responsive">
        """;

    public const string Footer = """
              </main>
            </body>
            </html>
            """;
}
