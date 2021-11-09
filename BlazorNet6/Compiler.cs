/*
    Credits for serving as a working template:
    https://github.com/Suchiman/Runny/blob/master/Runny/Compiler.cs
*/
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Net.Http.Json;
using System.Reflection;

namespace BlazorNet6
{
    public static class Compiler
    {
        private class BlazorBoot
        {
            public bool cacheBootResources { get; set; }
            public object[] config { get; set; }
            public bool debugBuild { get; set; }
            public string entryAssembly { get; set; }
            public bool linkerEnabled { get; set; }
            public Resources resources { get; set; }
        }

        private class Resources
        {
            public Dictionary<string, string> assembly { get; set; }
            public Dictionary<string, string> pdb { get; set; }
            public Dictionary<string, string> runtime { get; set; }
        }

        private static List<MetadataReference> References;

        public static async Task InitializeMetadataReferencesAsync(HttpClient client)
        {
            var response = await client.GetFromJsonAsync<BlazorBoot>("_framework/blazor.boot.json");
            var assemblies = await Task.WhenAll(response.resources.assembly.Keys.Select(x => client.GetAsync("_framework/_bin/" + x)));

            var references = new List<MetadataReference>(assemblies.Length);
            foreach (var asm in assemblies)
            {
                using var task = await asm.Content.ReadAsStreamAsync();
                references.Add(MetadataReference.CreateFromStream(task));
            }

            References = references;
        }

        public static bool TryLoadSource(string source, out Assembly assembly)
        {
            assembly = null;

            var compilation = CSharpCompilation.Create("DynamicCode")
                .WithOptions(new CSharpCompilationOptions(OutputKind.ConsoleApplication))
                .AddReferences(References)
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)));

            ImmutableArray<Diagnostic> diagnostics = compilation.GetDiagnostics();

            if (diagnostics.Any(_ => _.Severity == DiagnosticSeverity.Error))
                return false;

            var outputAssembly = new MemoryStream();
            compilation.Emit(outputAssembly);

            assembly = Assembly.Load(outputAssembly.ToArray());
            return true;
        }
    }
}
