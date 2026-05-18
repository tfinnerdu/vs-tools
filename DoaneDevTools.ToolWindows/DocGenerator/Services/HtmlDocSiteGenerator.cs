using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace DoaneDevTools.ToolWindows.DocGenerator.Services
{
    // -----------------------------------------------------------------------
    // Models
    // -----------------------------------------------------------------------

    /// <summary>Represents a documented type extracted from an XML doc file.</summary>
    internal class DocType
    {
        public string FullName    { get; set; } = string.Empty;
        public string Namespace   { get; set; } = string.Empty;
        public string ShortName   { get; set; } = string.Empty;
        public string Summary     { get; set; } = string.Empty;
        public List<DocMember> Members { get; set; } = new List<DocMember>();
    }

    /// <summary>Represents a documented member (method, property, field, event).</summary>
    internal class DocMember
    {
        public string Name        { get; set; } = string.Empty;
        public string Signature   { get; set; } = string.Empty;
        public string Summary     { get; set; } = string.Empty;
        public string Returns     { get; set; } = string.Empty;
        public List<(string Name, string Description)> Params { get; set; } = new();
        public char   Kind        { get; set; } = 'M'; // T, M, P, F, E
    }

    // -----------------------------------------------------------------------
    // Generator
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads the .xml documentation file produced by the C# compiler
    /// (<c>&lt;GenerateDocumentationFile&gt;true&lt;/GenerateDocumentationFile&gt;</c>)
    /// and generates a static HTML documentation site.
    ///
    /// Output structure:
    ///   {outputDir}/index.html              — landing page with namespace index
    ///   {outputDir}/types/{FullName}.html   — per-type page
    ///   {outputDir}/assets/styles.css       — Doane-branded stylesheet
    ///   {outputDir}/assets/search.js        — client-side search
    /// </summary>
    public class HtmlDocSiteGenerator
    {
        // Doane brand colours (from handoff doc Section 6).
        private const string BrandOrange = "#FF7900";
        private const string BrandBlue   = "#1F3864";

        // -----------------------------------------------------------------------
        // Public entry point
        // -----------------------------------------------------------------------

        /// <summary>
        /// Generates the HTML documentation site.
        /// </summary>
        /// <param name="xmlDocFile">
        /// Path to the compiler-generated XML documentation file, or
        /// <see langword="null"/> if no XML file is available (the site will still
        /// be generated with a notice).
        /// </param>
        /// <param name="outputDir">Root directory for generated output.</param>
        /// <param name="assemblyName">Display name used in the page titles.</param>
        public void Generate(string? xmlDocFile, string outputDir, string assemblyName)
        {
            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(Path.Combine(outputDir, "types"));
            Directory.CreateDirectory(Path.Combine(outputDir, "assets"));

            // Write static assets first (stylesheet + search script).
            WriteStylesheet(Path.Combine(outputDir, "assets", "styles.css"));
            WriteSearchScript(Path.Combine(outputDir, "assets", "search.js"));

            // Parse the XML doc file (if present).
            List<DocType> types;
            if (!string.IsNullOrEmpty(xmlDocFile) && File.Exists(xmlDocFile))
            {
                types = ParseXmlDocFile(xmlDocFile);
            }
            else
            {
                types = new List<DocType>();
            }

            // Generate per-type pages.
            foreach (var type in types)
                WriteTypePage(type, outputDir, assemblyName);

            // Generate the landing page.
            WriteIndexPage(types, outputDir, assemblyName);
        }

        // -----------------------------------------------------------------------
        // XML parsing
        // -----------------------------------------------------------------------

        private static List<DocType> ParseXmlDocFile(string xmlDocFile)
        {
            var types = new Dictionary<string, DocType>(StringComparer.Ordinal);

            try
            {
                var doc     = XDocument.Load(xmlDocFile);
                var members = doc.Descendants("member");

                foreach (var member in members)
                {
                    var name = member.Attribute("name")?.Value ?? string.Empty;
                    if (string.IsNullOrEmpty(name)) continue;

                    char kind    = name.Length > 0 ? name[0] : 'M';
                    var  dotName = name.Length > 2 ? name[2..] : name; // strip "T:", "M:", etc.

                    var summary  = member.Element("summary")?.Value.Trim() ?? string.Empty;
                    var returns  = member.Element("returns")?.Value.Trim() ?? string.Empty;
                    var paramEls = member.Elements("param")
                        .Select(p => (p.Attribute("name")?.Value ?? "", p.Value.Trim()))
                        .ToList();

                    if (kind == 'T')
                    {
                        // Type entry.
                        var typeName      = dotName;
                        var lastDot       = typeName.LastIndexOf('.');
                        var shortName     = lastDot >= 0 ? typeName[(lastDot + 1)..] : typeName;
                        var namespaceName = lastDot >= 0 ? typeName[..lastDot] : string.Empty;

                        if (!types.TryGetValue(typeName, out var typeEntry))
                        {
                            typeEntry = new DocType
                            {
                                FullName  = typeName,
                                ShortName = shortName,
                                Namespace = namespaceName
                            };
                            types[typeName] = typeEntry;
                        }

                        typeEntry.Summary = summary;
                    }
                    else
                    {
                        // Member entry: strip parameter list to get the parent type name.
                        var parenIdx  = dotName.IndexOf('(');
                        var fullSig   = parenIdx >= 0 ? dotName[..parenIdx] : dotName;
                        var lastDot   = fullSig.LastIndexOf('.');
                        var ownerType = lastDot >= 0 ? fullSig[..lastDot] : string.Empty;
                        var memberName = lastDot >= 0 ? fullSig[(lastDot + 1)..] : fullSig;

                        if (string.IsNullOrEmpty(ownerType)) continue;

                        if (!types.TryGetValue(ownerType, out var typeEntry))
                        {
                            var lastOwnerDot = ownerType.LastIndexOf('.');
                            typeEntry = new DocType
                            {
                                FullName  = ownerType,
                                ShortName = lastOwnerDot >= 0 ? ownerType[(lastOwnerDot + 1)..] : ownerType,
                                Namespace = lastOwnerDot >= 0 ? ownerType[..lastOwnerDot] : string.Empty
                            };
                            types[ownerType] = typeEntry;
                        }

                        typeEntry.Members.Add(new DocMember
                        {
                            Kind      = kind,
                            Name      = memberName,
                            Signature = dotName,
                            Summary   = summary,
                            Returns   = returns,
                            Params    = paramEls
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // Return empty list if the XML is malformed.
                Console.Error.WriteLine($"[HtmlDocSiteGenerator] XML parse error: {ex.Message}");
            }

            return types.Values.OrderBy(t => t.FullName).ToList();
        }

        // -----------------------------------------------------------------------
        // Page generators
        // -----------------------------------------------------------------------

        private void WriteIndexPage(List<DocType> types, string outputDir, string assemblyName)
        {
            var namespaces = types
                .GroupBy(t => string.IsNullOrEmpty(t.Namespace) ? "(global)" : t.Namespace)
                .OrderBy(g => g.Key);

            var sb = new StringBuilder();
            sb.AppendLine(HtmlHeader(assemblyName, "../assets/styles.css", "../assets/search.js"));
            sb.AppendLine($"<h1 class=\"assembly-title\">{HtmlEncode(assemblyName)}</h1>");
            sb.AppendLine("<p class=\"subtitle\">API Reference Documentation</p>");

            // Search box
            sb.AppendLine("<div class=\"search-container\">");
            sb.AppendLine("  <input id=\"searchInput\" type=\"text\" placeholder=\"Search types and members…\" oninput=\"doSearch(this.value)\"/>");
            sb.AppendLine("  <div id=\"searchResults\" class=\"search-results\"></div>");
            sb.AppendLine("</div>");

            // Namespace index
            foreach (var nsGroup in namespaces)
            {
                sb.AppendLine($"<section class=\"namespace-section\">");
                sb.AppendLine($"  <h2 class=\"namespace-heading\">{HtmlEncode(nsGroup.Key)}</h2>");
                sb.AppendLine("  <ul class=\"type-list\">");

                foreach (var type in nsGroup.OrderBy(t => t.ShortName))
                {
                    var href   = $"types/{type.FullName}.html";
                    var badge  = BadgeHtml(type);
                    sb.AppendLine($"    <li>{badge}<a href=\"{href}\">{HtmlEncode(type.ShortName)}</a>");
                    if (!string.IsNullOrEmpty(type.Summary))
                        sb.AppendLine($"      <span class=\"summary\">{HtmlEncode(type.Summary)}</span>");
                    sb.AppendLine("    </li>");
                }

                sb.AppendLine("  </ul>");
                sb.AppendLine("</section>");
            }

            sb.AppendLine(HtmlFooter());
            File.WriteAllText(Path.Combine(outputDir, "index.html"), sb.ToString(), Encoding.UTF8);
        }

        private void WriteTypePage(DocType type, string outputDir, string assemblyName)
        {
            var sb = new StringBuilder();
            var rootHref = "../../assets/";
            sb.AppendLine(HtmlHeader($"{type.ShortName} — {assemblyName}",
                rootHref + "styles.css", rootHref + "search.js"));

            // Breadcrumb
            sb.AppendLine("<nav class=\"breadcrumb\">");
            sb.AppendLine($"  <a href=\"../../index.html\">{HtmlEncode(assemblyName)}</a> › ");
            sb.AppendLine($"  <span>{HtmlEncode(type.Namespace)}</span> › ");
            sb.AppendLine($"  <strong>{HtmlEncode(type.ShortName)}</strong>");
            sb.AppendLine("</nav>");

            // Type heading + summary
            sb.AppendLine($"<h1 class=\"type-title\">{HtmlEncode(type.ShortName)}</h1>");
            sb.AppendLine($"<p class=\"type-namespace\">Namespace: <code>{HtmlEncode(type.Namespace)}</code></p>");
            if (!string.IsNullOrEmpty(type.Summary))
                sb.AppendLine($"<p class=\"type-summary\">{HtmlEncode(type.Summary)}</p>");

            // Members grouped by kind
            var groups = type.Members
                .GroupBy(m => m.Kind)
                .OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                var groupTitle = group.Key switch
                {
                    'M' => "Methods",
                    'P' => "Properties",
                    'F' => "Fields",
                    'E' => "Events",
                    _   => "Members"
                };

                sb.AppendLine($"<section class=\"member-section\">");
                sb.AppendLine($"  <h2 class=\"section-heading\">{groupTitle}</h2>");

                foreach (var member in group.OrderBy(m => m.Name))
                {
                    sb.AppendLine("  <div class=\"member-entry\">");
                    sb.AppendLine($"    <h3 class=\"member-name\"><code>{HtmlEncode(member.Signature)}</code></h3>");

                    if (!string.IsNullOrEmpty(member.Summary))
                        sb.AppendLine($"    <p class=\"member-summary\">{HtmlEncode(member.Summary)}</p>");

                    if (member.Params.Count > 0)
                    {
                        sb.AppendLine("    <table class=\"params-table\">");
                        sb.AppendLine("      <thead><tr><th>Parameter</th><th>Description</th></tr></thead>");
                        sb.AppendLine("      <tbody>");
                        foreach (var (pName, pDesc) in member.Params)
                            sb.AppendLine($"        <tr><td><code>{HtmlEncode(pName)}</code></td><td>{HtmlEncode(pDesc)}</td></tr>");
                        sb.AppendLine("      </tbody>");
                        sb.AppendLine("    </table>");
                    }

                    if (!string.IsNullOrEmpty(member.Returns))
                        sb.AppendLine($"    <p class=\"member-returns\"><strong>Returns:</strong> {HtmlEncode(member.Returns)}</p>");

                    sb.AppendLine("  </div>");
                }

                sb.AppendLine("</section>");
            }

            sb.AppendLine(HtmlFooter());

            var outPath = Path.Combine(outputDir, "types", $"{type.FullName}.html");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        }

        // -----------------------------------------------------------------------
        // Static assets
        // -----------------------------------------------------------------------

        private static void WriteStylesheet(string path)
        {
            const string css = @"
/* ── Doane DevTools Documentation Site Styles ────────────────────────────── */
:root {
    --brand-orange: #FF7900;
    --brand-blue:   #1F3864;
    --bg:           #ffffff;
    --bg-secondary: #f7f8fa;
    --text:         #1a1a2e;
    --text-muted:   #6b7280;
    --border:       #e5e7eb;
    --code-bg:      #f3f4f6;
    --link:         #1F3864;
    --link-hover:   #FF7900;
    --font-sans:    'Segoe UI', system-ui, sans-serif;
    --font-mono:    'Cascadia Code', 'Consolas', monospace;
    --radius:       6px;
    --shadow:       0 1px 4px rgba(0,0,0,0.08);
}

*, *::before, *::after { box-sizing: border-box; }

body {
    margin: 0;
    font-family: var(--font-sans);
    font-size: 15px;
    color: var(--text);
    background: var(--bg);
    line-height: 1.6;
}

a { color: var(--link); text-decoration: none; }
a:hover { color: var(--link-hover); text-decoration: underline; }

/* ── Top nav bar ─────────────────────────────────────────────────────────── */
.top-bar {
    background: var(--brand-blue);
    color: #fff;
    padding: 12px 32px;
    display: flex;
    align-items: center;
    gap: 16px;
    box-shadow: var(--shadow);
}
.top-bar .logo { font-size: 18px; font-weight: 700; color: #fff; letter-spacing: .5px; }
.top-bar .logo span { color: var(--brand-orange); }

/* ── Layout ──────────────────────────────────────────────────────────────── */
main {
    max-width: 1100px;
    margin: 0 auto;
    padding: 40px 32px;
}

/* ── Headings ────────────────────────────────────────────────────────────── */
h1.assembly-title { font-size: 2rem; color: var(--brand-blue); margin-bottom: 4px; }
p.subtitle        { color: var(--text-muted); margin-top: 0; }
h1.type-title     { font-size: 1.75rem; color: var(--brand-blue); margin-bottom: 4px; }
h2.namespace-heading,
h2.section-heading {
    font-size: 1.1rem;
    color: var(--brand-blue);
    border-bottom: 2px solid var(--brand-orange);
    padding-bottom: 4px;
    margin-top: 36px;
}

/* ── Namespace index ─────────────────────────────────────────────────────── */
section.namespace-section { margin-bottom: 32px; }
ul.type-list { list-style: none; padding: 0; margin: 0; }
ul.type-list li {
    padding: 6px 0;
    border-bottom: 1px solid var(--border);
    display: flex;
    align-items: baseline;
    gap: 8px;
}
ul.type-list li:last-child { border-bottom: none; }
.summary { color: var(--text-muted); font-size: 0.875rem; margin-left: 8px; }

/* ── Badges ──────────────────────────────────────────────────────────────── */
.badge {
    display: inline-block;
    font-size: 0.7rem;
    font-weight: 700;
    padding: 1px 6px;
    border-radius: 3px;
    color: #fff;
    letter-spacing: .5px;
    flex-shrink: 0;
}
.badge-class     { background: var(--brand-blue); }
.badge-interface { background: var(--brand-orange); }
.badge-struct    { background: #4a9c6f; }
.badge-enum      { background: #7c4dff; }

/* ── Breadcrumb ──────────────────────────────────────────────────────────── */
nav.breadcrumb { font-size: 0.875rem; color: var(--text-muted); margin-bottom: 24px; }
nav.breadcrumb a { color: var(--text-muted); }
nav.breadcrumb a:hover { color: var(--link-hover); }

/* ── Type page ───────────────────────────────────────────────────────────── */
p.type-namespace { font-size: 0.875rem; color: var(--text-muted); margin-top: 0; }
p.type-summary   { max-width: 800px; }

/* ── Member entries ──────────────────────────────────────────────────────── */
.member-entry {
    background: var(--bg-secondary);
    border: 1px solid var(--border);
    border-radius: var(--radius);
    padding: 16px 20px;
    margin-bottom: 12px;
    box-shadow: var(--shadow);
}
h3.member-name { margin: 0 0 8px; font-size: 1rem; font-weight: 600; }
h3.member-name code {
    background: var(--code-bg);
    border-radius: 4px;
    padding: 2px 6px;
    font-size: 0.9rem;
    word-break: break-all;
}
p.member-summary { margin: 0 0 12px; }
p.member-returns { margin: 8px 0 0; font-size: 0.9rem; }

/* ── Params table ────────────────────────────────────────────────────────── */
table.params-table {
    width: 100%;
    border-collapse: collapse;
    font-size: 0.875rem;
    margin: 8px 0;
}
table.params-table th {
    text-align: left;
    background: var(--brand-blue);
    color: #fff;
    padding: 6px 10px;
    font-weight: 600;
}
table.params-table td {
    padding: 6px 10px;
    border-bottom: 1px solid var(--border);
    vertical-align: top;
}
table.params-table tr:last-child td { border-bottom: none; }
table.params-table td:first-child { font-family: var(--font-mono); font-size: 0.85rem; }
table.params-table tr:nth-child(even) td { background: var(--bg); }

/* ── Search ──────────────────────────────────────────────────────────────── */
.search-container { position: relative; max-width: 500px; margin-bottom: 32px; }
#searchInput {
    width: 100%;
    padding: 10px 16px;
    border: 1px solid var(--border);
    border-radius: var(--radius);
    font-size: 15px;
    font-family: var(--font-sans);
    outline: none;
    box-shadow: var(--shadow);
}
#searchInput:focus { border-color: var(--brand-orange); }
.search-results {
    position: absolute;
    top: calc(100% + 4px);
    left: 0; right: 0;
    background: #fff;
    border: 1px solid var(--border);
    border-radius: var(--radius);
    box-shadow: 0 4px 12px rgba(0,0,0,0.12);
    z-index: 10;
    max-height: 320px;
    overflow-y: auto;
    display: none;
}
.search-results.visible { display: block; }
.search-result-item {
    padding: 10px 16px;
    border-bottom: 1px solid var(--border);
    cursor: pointer;
}
.search-result-item:hover { background: var(--bg-secondary); }
.search-result-item:last-child { border-bottom: none; }
.search-result-title { font-weight: 600; color: var(--brand-blue); }
.search-result-ns    { font-size: 0.8rem; color: var(--text-muted); }

/* ── Footer ──────────────────────────────────────────────────────────────── */
footer {
    margin-top: 64px;
    padding: 24px 32px;
    border-top: 1px solid var(--border);
    text-align: center;
    font-size: 0.8rem;
    color: var(--text-muted);
}
footer span { color: var(--brand-orange); font-weight: 600; }

code { font-family: var(--font-mono); background: var(--code-bg); padding: 1px 4px; border-radius: 3px; }
";
            File.WriteAllText(path, css, Encoding.UTF8);
        }

        private static void WriteSearchScript(string path)
        {
            const string js = @"
/* ── DoaneDevTools Documentation Site — Client-side Search ───────────────── */

// The search index is built from the page's type list entries.
// Each entry: { title, namespace, href }
var searchIndex = [];

(function buildIndex() {
    document.querySelectorAll('ul.type-list li').forEach(function(li) {
        var anchor = li.querySelector('a');
        var badge  = li.querySelector('.badge');
        if (!anchor) return;
        searchIndex.push({
            title:     anchor.textContent.trim(),
            namespace: li.closest('section')?.querySelector('.namespace-heading')?.textContent?.trim() ?? '',
            kind:      badge ? badge.textContent.trim() : '',
            href:      anchor.getAttribute('href')
        });
    });
})();

function doSearch(query) {
    var resultsEl = document.getElementById('searchResults');
    if (!resultsEl) return;

    query = query.trim().toLowerCase();
    if (!query) {
        resultsEl.innerHTML = '';
        resultsEl.classList.remove('visible');
        return;
    }

    var hits = searchIndex.filter(function(item) {
        return item.title.toLowerCase().includes(query) ||
               item.namespace.toLowerCase().includes(query);
    }).slice(0, 12);

    if (hits.length === 0) {
        resultsEl.innerHTML = '<div class=""search-result-item""><span class=""search-result-ns"">No results found.</span></div>';
        resultsEl.classList.add('visible');
        return;
    }

    resultsEl.innerHTML = hits.map(function(item) {
        return '<div class=""search-result-item"" onclick=""location.href=\'' + item.href + '\'"" >' +
               '  <div class=""search-result-title"">' + item.title + '</div>' +
               '  <div class=""search-result-ns"">' + item.namespace + '</div>' +
               '</div>';
    }).join('');
    resultsEl.classList.add('visible');
}

// Close dropdown when clicking outside.
document.addEventListener('click', function(e) {
    if (!e.target.closest('.search-container')) {
        var r = document.getElementById('searchResults');
        if (r) { r.innerHTML = ''; r.classList.remove('visible'); }
    }
});
";
            File.WriteAllText(path, js, Encoding.UTF8);
        }

        // -----------------------------------------------------------------------
        // Private HTML helpers
        // -----------------------------------------------------------------------

        private static string HtmlHeader(string title, string cssHref, string jsHref) =>
            $@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8""/>
  <meta name=""viewport"" content=""width=device-width,initial-scale=1""/>
  <title>{HtmlEncode(title)}</title>
  <link rel=""stylesheet"" href=""{cssHref}""/>
</head>
<body>
  <header class=""top-bar"">
    <span class=""logo"">Doane<span>DevTools</span></span>
    <span style=""color:rgba(255,255,255,0.6);font-size:0.9rem"">{HtmlEncode(title)}</span>
  </header>
  <main>
    <script src=""{jsHref}""></script>
";

        private static string HtmlFooter() =>
            $@"  </main>
  <footer>
    Generated by <span>DoaneDevTools</span> &mdash; {DateTime.UtcNow:yyyy-MM-dd}
  </footer>
</body>
</html>";

        private static string BadgeHtml(DocType type)
        {
            // Heuristic: interfaces start with "I" and have a second upper-case letter.
            var badgeClass = "badge-class";
            if (type.ShortName.StartsWith("I") && type.ShortName.Length > 1 &&
                char.IsUpper(type.ShortName[1]))
                badgeClass = "badge-interface";
            else if (type.ShortName.EndsWith("Enum") || type.FullName.Contains(".Enums."))
                badgeClass = "badge-enum";

            var label = badgeClass switch
            {
                "badge-interface" => "I",
                "badge-enum"      => "E",
                _                 => "C"
            };

            return $"<span class=\"badge {badgeClass}\">{label}</span>";
        }

        private static string HtmlEncode(string s) =>
            System.Net.WebUtility.HtmlEncode(s);
    }
}
