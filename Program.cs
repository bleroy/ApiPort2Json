using LibGit2Sharp;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
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

            //var commentExpression = new Regex(@"<!--.*?-->", RegexOptions.Singleline);

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
            - [ ] Optional
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
            var parseExpression = new Regex(@"^[\r\n\s]*"
                + @"## (?<id>\d+): (?<title>[^\r\n]+)[\r\n\s]+"
                + @"### Scope[\r\n\s]+(?<scope>[^\r\n]+)[\r\n\s]+"
                + @"### Version Introduced[\r\n\s]+(?<versionIntroduced>[^\r\n]+)[\r\n\s]+"
                + @"(### Version Reverted[\r\n\s]+(?<versionReverted>[^\r\n]+)[\r\n\s]+)?"
                + @"### Source Analyzer Status[\r\n\s]+(?<sourceAnalyzerStatus>[^\r\n]+)[\r\n\s]+"
                + @"### Change Description[\r\n\s]+(?<description>.+)[\r\n\s]*"
                + @"- \[(?<quirked>.)\] Quirked[\r\n\s]+"
                + @"(- \[(?<optional>.)\] Optional[\r\n\s]+)?"
                + @"- \[(?<buildTimeBreak>.)\] Build-time break[\r\n\s]+"
                + @"### Recommended Action[\r\n\s]+(?<recommendedAction>.+)[\r\n\s]*"
                + @"### Affected APIs[\r\n\s]+(?<affectedApis>.+)[\r\n\s]*"
                + @"### (Category|Categories)[\r\n\s]+(?<category>.+?)[\r\n\s]*"
                + @"([\r\n\s]+\[More information\]\((?<moreInformation>.+)\)[\r\n\s]*)?"
                + @"([\r\n\s]+<!--"
                + @"[\r\n\s]+\s\s\s\s### Notes[\r\n\s]+(?<notes>.+)[\r\n\s]*"
                + @"[\r\n\s]+-->)?[\r\n\s]*$",
                RegexOptions.ExplicitCapture | RegexOptions.Singleline);

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
                        w.Formatting = Formatting.Indented;
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
                            // Remove comments
                            // markdown = commentExpression.Replace(markdown, "");

                            var match = parseExpression.Match(markdown);

                            Action<string> add = name => {
                                var value = match.Groups[name].Value.Trim();
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    w.WritePropertyName(name);
                                    w.WriteValue(value);
                                }
                            };

                            Action<string> addFlag = name => {
                                w.WritePropertyName(name);
                                w.WriteValue(string.IsNullOrWhiteSpace(match.Groups[name].Value) ? false : true);
                            };

                            w.WritePropertyName("id");
                            w.WriteValue(int.Parse(match.Groups["id"].Value));
                            add("title");
                            add("scope");
                            add("versionIntroduced");
                            add("versionReverted");
                            add("sourceAnalyzerStatus");
                            add("description");
                            addFlag("quirked");
                            addFlag("optional");
                            addFlag("buildTimeBreak");
                            add("recommendedAction");
                            add("affectedApis"); // Make that better-structured
                            w.WritePropertyName("categories");
                            w.WriteStartArray();
                            var category = match.Groups["category"].Value;
                            if (!string.IsNullOrWhiteSpace(category))
                            {
                                var categories = category
                                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(s => s.Trim());
                                foreach(var categoryName in categories)
                                {
                                    w.WriteValue(categoryName);
                                }
                            }
                            w.WriteEndArray();
                            add("moreInformation");
                            add("originalBug");
                            add("notes");

                            w.WriteEndObject();
                        }
                        w.WriteEndArray();
                    }
                }
            }
            finally
            {
                try {
                    Directory.Delete(repoFolder, true);
                }
                catch(UnauthorizedAccessException) { }
            }
        }
    }
}
