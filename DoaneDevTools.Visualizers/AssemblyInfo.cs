using System.Diagnostics;
using DoaneDevTools.Visualizers;
using Microsoft.VisualStudio.DebuggerVisualizers;

// ── SqlConnection visualizer ───────────────────────────────────────────────
[assembly: DebuggerVisualizer(
    typeof(SqlConnectionVisualizer),
    typeof(SqlConnectionVisualizerObjectSource),
    Target = typeof(System.Data.SqlClient.SqlConnection),
    Description = "Doane SQL Connection Inspector")]

// ── HttpClient visualizer ─────────────────────────────────────────────────
[assembly: DebuggerVisualizer(
    typeof(HttpClientVisualizer),
    typeof(HttpClientVisualizerObjectSource),
    Target = typeof(System.Net.Http.HttpClient),
    Description = "Doane HTTP Client Inspector")]

// ── HttpResponseMessage visualizer ───────────────────────────────────────
[assembly: DebuggerVisualizer(
    typeof(HttpResponseVisualizer),
    typeof(HttpResponseVisualizerObjectSource),
    Target = typeof(System.Net.Http.HttpResponseMessage),
    Description = "Doane HTTP Response Inspector")]

// ── JObject (Newtonsoft.Json) visualizer ─────────────────────────────────
[assembly: DebuggerVisualizer(
    typeof(JsonVisualizer),
    typeof(JsonVisualizerObjectSource),
    TargetTypeName = "Newtonsoft.Json.Linq.JObject, Newtonsoft.Json",
    Description = "Doane JSON Object Inspector")]

// ── JArray (Newtonsoft.Json) visualizer ──────────────────────────────────
[assembly: DebuggerVisualizer(
    typeof(JsonVisualizer),
    typeof(JsonVisualizerObjectSource),
    TargetTypeName = "Newtonsoft.Json.Linq.JArray, Newtonsoft.Json",
    Description = "Doane JSON Array Inspector")]

// ── Dictionary visualizer ─────────────────────────────────────────────────
[assembly: DebuggerVisualizer(
    typeof(DictionaryVisualizer),
    typeof(DictionaryVisualizerObjectSource),
    TargetTypeName = "System.Collections.Generic.Dictionary`2, mscorlib",
    Description = "Doane Dictionary Inspector")]

// ── List/IEnumerable visualizer ───────────────────────────────────────────
[assembly: DebuggerVisualizer(
    typeof(ListVisualizer),
    typeof(ListVisualizerObjectSource),
    TargetTypeName = "System.Collections.Generic.List`1, mscorlib",
    Description = "Doane List Inspector")]

// ── Exception visualizer ──────────────────────────────────────────────────
[assembly: DebuggerVisualizer(
    typeof(ExceptionVisualizer),
    typeof(ExceptionVisualizerObjectSource),
    Target = typeof(System.Exception),
    Description = "Doane Exception Inspector")]
