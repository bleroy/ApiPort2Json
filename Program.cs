using LibGit2Sharp;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ApiPort2Json
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--help")
            {
                Console.WriteLine(@"ApiPort2Json - Convert ApiPort data to a Json document

Usage: apiport2json url output
  url: the URL of the ApiPort repository. Default is ""https://github.com/Microsoft/dotnet-apiport/"".
  output: the path of the JSON output file. Default is ""apiport.json"".");
            }
            /* Template:

            ## ID: Breaking Change Title

            ### Scope
            Major

            ### Version Introduced
            4.5

            ### Version Reverted
            4.6

            ### Source Analyzer Status
            Available

            ### Change Description
            Description goes here.

            - [ ] Quirked
            - [ ] Build-time break

            ### Recommended Action
            Suggested steps if user is affected (such as work arounds or code fixes) go here.

            ### Affected APIs
            * Not detectable via API analysis

            ### Category
            Pick-a-Category-From-BreakingChangesCategories.json

            [More information](LinkForMoreInformation)

            <!--
                ### Original Bug
                Bug link goes here
                ### Notes
                Source analyzer status: Not usefully detectable with an analyzer
            -->
            */
            var parseExpression = new Regex(@"## (?<id>\d+): (?<title>[^\r\n]+)[\r\n\s]+### Scope[\r\n\s]+(?<scope>[^\r\n]+)", RegexOptions.ExplicitCapture | RegexOptions.Multiline);

            var apiPortRepoUrl = args.Length > 0 ? args[0] : "https://github.com/Microsoft/dotnet-apiport/";
            var outputPath = args.Length > 1 ? args[1] : "apiport.json";

            var tempPath = Path.GetTempPath();
            var tempFolder = Guid.NewGuid().ToString("n");
            var repoFolder = Path.Combine(tempPath, tempFolder);

            try
            {
                Console.WriteLine($"Cloning {apiPortRepoUrl} in {outputPath}...");
                Repository.Clone(apiPortRepoUrl, repoFolder);

                using (var output = File.CreateText(outputPath)) {
                    using (var w = new JsonTextWriter(output))
                    {
                        w.WriteStartArray();
                        // scan /docs/BreakingChanges/*.md
                        var docFolder = Path.Combine(repoFolder, "docs", "BreakingChanges");
                        foreach (var markdownFilePath in Directory.EnumerateFiles(docFolder, "*.md", SearchOption.TopDirectoryOnly))
                        {
                            var markdownFileName = Path.GetFileNameWithoutExtension(markdownFilePath);
                            // Skip template file
                            if (markdownFileName == "! Template") continue;

                            Console.WriteLine($"Parsing {markdownFileName}...");

                            w.WriteStartObject();

                            var markdown = File.ReadAllText(markdownFilePath);
                            var match = parseExpression.Match(markdown);


                            var id = int.Parse(match.Groups["id"].Value);
                            w.WritePropertyName("id");
                            w.WriteValue(id);

                            var title = match.Groups["title"].Value;
                            w.WritePropertyName("title");
                            w.WriteValue(title);

                            var scope = match.Groups["scope"].Value;
                            w.WritePropertyName("scope");
                            w.WriteValue(scope);

                            w.WriteEndObject();
                        }
                        w.WriteEndArray();
                    }
                }
            }
            finally
            {
                Directory.Delete(repoFolder, true);
            }
        }
    }
}
