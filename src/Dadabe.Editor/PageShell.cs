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
            <script src="/app.js"></script>
            <link rel="stylesheet" href="https://ka-f.webawesome.com/webawesome@3.7.0/styles/webawesome.css" />
            <link rel="stylesheet" href="https://ka-f.webawesome.com/webawesome@3.8.0/styles/themes/awesome.css">
            <link rel="stylesheet" href="https://ka-f.webawesome.com/webawesome@3.8.0/styles/color/palettes/bright.css">
            <script type="module" src="https://ka-f.webawesome.com/webawesome@3.7.0/webawesome.loader.js"></script>
            <script src="https://unpkg.com/htmx.org@2/dist/htmx.min.js"></script>
            <link rel="stylesheet" href="/app.css" />
        </head>
        <body>
          <wa-page>
            <header slot="header">
              <strong>Dadabe</strong>
              <wa-button-group label="Navigation">
                <wa-button appearance="filled" hx-get="/dashboard"    hx-target="#main-content" hx-select="#page-body" hx-swap="innerHTML" hx-push-url="true">Dashboard</wa-button>
                <wa-button appearance="filled" hx-get="/progressions" hx-target="#main-content" hx-select="#page-body" hx-swap="innerHTML" hx-push-url="true">Progressions</wa-button>
                <wa-button appearance="filled" hx-get="/predictions"  hx-target="#main-content" hx-select="#page-body" hx-swap="innerHTML" hx-push-url="true">Predictions</wa-button>
                <wa-button appearance="filled" hx-get="/tunings"      hx-target="#main-content" hx-select="#page-body" hx-swap="innerHTML" hx-push-url="true">Tunings</wa-button>
                <wa-button appearance="filled" hx-get="/reference"    hx-target="#main-content" hx-select="#page-body" hx-swap="innerHTML" hx-push-url="true">Reference</wa-button>
                <wa-button appearance="filled" hx-get="/voicings"     hx-target="#main-content" hx-select="#page-body" hx-swap="innerHTML" hx-push-url="true">Voicings</wa-button>
                <wa-button appearance="filled" hx-get="/voice-lead"   hx-target="#main-content" hx-select="#page-body" hx-swap="innerHTML" hx-push-url="true">Voice Lead</wa-button>
                <wa-button appearance="filled" hx-get="/songs"        hx-target="#main-content" hx-select="#page-body" hx-swap="innerHTML" hx-push-url="true">Songs</wa-button>
              </wa-button-group>
              <wa-select id="global-song-select" style="min-width: 140px;"
                      hx-get="/api/songs/options"
                      hx-trigger="load"
                      hx-swap="innerHTML">
              </wa-select>
              <wa-select id="global-section-select" style="min-width: 140px; display:none;">
              </wa-select>
              <wa-select id="global-tuning-select" disabled style="min-width: 140px;"
                      hx-get="/api/tunings/options"
                      hx-trigger="load"
                      hx-swap="innerHTML">
              </wa-select>
              <wa-button id="color-scheme-button" appearance="plain" aria-label="Toggle light/dark mode">🌓 Theme</wa-button>
            </header>
            <wa-callout id="song-tuning-warning" variant="warning" style="display:none; margin: 0 1.5rem;">
              <wa-icon slot="icon" name="triangle-exclamation"></wa-icon>
              The active tuning no longer matches <strong id="song-tuning-warning-name"></strong>'s tuning —
              every pinned shape in this song was fingered for a different tuning.
              <wa-button size="small" id="song-tuning-relock" appearance="outlined">Restore song tuning</wa-button>
            </wa-callout>
            <main id="main-content" style="padding: 1.5rem; overflow-y: auto;">
              <div id="page-body">
        """;

    public const string Footer = """
              </div>
            </main>
          </wa-page>
        </body>
        </html>
        """;
}
