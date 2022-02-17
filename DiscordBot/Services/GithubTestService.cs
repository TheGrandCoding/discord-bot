using Octokit;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace DiscordBot.Services
{
    public class GithubTestService : Service
    {
        private GitHubJwt.GitHubJwtFactory jwtFactory;

        private SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        const string appIdConfig = "tokens:gh-catch:app_id";
        const string clientIdConfig = "tokens:gh-catch:client_id";
        const string clientSecretConfig = "tokens:gh-catch:client_secret";
        public override void OnReady()
        {
            EnsureConfiguration(appIdConfig);
            EnsureConfiguration(clientIdConfig);
            EnsureConfiguration(clientSecretConfig);

            jwtFactory = new GitHubJwt.GitHubJwtFactory(
                new GitHubJwt.FilePrivateKeySource(Path.Combine(Program.BASE_PATH, "data", "keys", "catchbot.pem")),
                new GitHubJwt.GitHubJwtFactoryOptions
                {
                    AppIntegrationId = int.Parse(Program.Configuration[appIdConfig]), // The GitHub App Id
                    ExpirationSeconds = 600 // 10 minutes is the maximum time allowed
                }
            );
        }

        private GitHubClient getClient(string installationToken = null)
        {
            bool e = string.IsNullOrWhiteSpace(installationToken);
            return new GitHubClient(new ProductHeaderValue("Catch2Bot" + (e ? "" : $"-Installation")), new Uri("https://github.coventry.ac.uk"))
            {
                Credentials = new Credentials(e ? jwtFactory.CreateEncodedJwtToken() : installationToken, AuthenticationType.Bearer)
            };
        }

        public void InboundWebhook(JsonWebhook jsonData)
        {
            var t = new Thread(thread);
            t.Name = "GH-" + jsonData.HookId;
            t.Start(jsonData);
        }

        void thread(object o)
        {
            if (!(o is JsonWebhook s))
                return;
            var cancelToken = Program.GetToken();
            try
            {
                Info($"Getting semaphore..", Thread.CurrentThread.Name);
                _lock.Wait(cancelToken);
                Info("Achieved semaphore..", Thread.CurrentThread.Name);
                handleWebhook(s).Wait(cancelToken);
            } catch(OperationCanceledException)
            {

            } catch(Exception e)
            {
                Error(e, "HandleWebhook");
            } finally
            {
                Info("Releasing semaphore..", Thread.CurrentThread.Name);
                _lock.Release();
                Info("Exiting HandleWebhook");
            }
        }

        const string extractFolder = "temp_data";

#if WINDOWS
        const string shellCmd = "cmd.exe";
#else
        const string shellCmd = "/bin/bash";
#endif

        string runProcess(string command, bool readStErrInstead, out int exitCode)
        {
#if DEBUG
            exitCode = 70;
            return File.ReadAllText(Path.Join(Program.BASE_PATH, "build.txt"));
#endif


            using Process proc = new Process();
            proc.StartInfo = new ProcessStartInfo(shellCmd)
            {
                RedirectStandardOutput = readStErrInstead == false,
                RedirectStandardError = readStErrInstead,
                UseShellExecute = false,
                CreateNoWindow = false,
#if WINDOWS
                Arguments = "/C " + command
#else
                Arguments = command
#endif
            };
            proc.Start();
            while(!proc.HasExited)
            {
                Info($"Waiting for process to exit..");
                proc.WaitForExit(5000);
            }
            exitCode = proc.ExitCode;
            if (readStErrInstead)
                return proc.StandardError.ReadToEnd();
            return proc.StandardOutput.ReadToEnd();
        }

        string _currentPath = null;
        string githubPath(string path)
        {
            return path.Replace(_currentPath, "");
        }

        async Task<List<NewCheckRunAnnotation>> innerConfigCheck(DirectoryInfo folder)
        {
            var annotations = new List<NewCheckRunAnnotation>();
            var cppFiles = folder.GetFiles("*.cpp")
                .Select(x => x.Name)
                .Where(x => x != "Game.cpp")
                .ToList();
            var cmakeFile = new FileInfo(Path.Combine(folder.FullName, "CMakeLists.txt"));


            int lastLineOfLibraries = 1;
            var libraries = new List<string>();

            int lineOfLink = 1;
            var missingLibraries = new List<string>();

            int lineNo = -1;
            foreach(var _line in await File.ReadAllLinesAsync(cmakeFile.FullName))
            {
                var line = _line.Replace("(", " ").Replace(")", " ");
                lineNo++;
                if(line.StartsWith("add_library"))
                {
                    var split = line.Split(' ').ToList();
                    int cppIndex = split.FindIndex(x => x.EndsWith(".cpp"));
                    if(cppIndex >= 0)
                    {
                        lastLineOfLibraries = lineNo;
                        var fileName = split[cppIndex];
                        if (fileName.Contains("Game.cpp"))
                            continue;
                        var libName = fileName.Replace(".cpp", "");
                        if(!split.Contains(libName))
                        {
                            annotations.Add(new NewCheckRunAnnotation(githubPath(cmakeFile.FullName),
                                lineNo, lineNo,
                                CheckAnnotationLevel.Warning,
                                $"Library name should be name of file without extension:\nOriginal:\n\n" +
                                "    " + line + 
                                "\n\nPossible fix:\n\n" +
                                $"    add_library( {libName} {libName}.cpp )"));
                        }
                        libraries.Add(libName);
                        if(!cppFiles.Remove(fileName))
                        {
                            annotations.Add(new NewCheckRunAnnotation(githubPath(cmakeFile.FullName), 
                                lineNo, lineNo, 
                                CheckAnnotationLevel.Warning, 
                                $"add_library references file which does not exist - is there a typo, or is this intentional?"));
                        }
                    }
                } else if(line.StartsWith("target_link_libraries"))
                {
                    lineOfLink = lineNo;
                    var split = line.Split(' ');
                    foreach(var iter in split)
                    {
                        var libLinked = iter.Trim();
                        if (libLinked.Contains("target_link_libraries") || libLinked.StartsWith("sfml"))
                            continue;

                    }
                    foreach(var lib in libraries)
                    {
                        if(!split.Contains(lib))
                        {
                            missingLibraries.Add(lib);
                        }
                    }
                }
            }

            if(missingLibraries.Count > 0)
            {
                annotations.Add(new NewCheckRunAnnotation(githubPath(cmakeFile.FullName),
                    lineOfLink, lineOfLink,
                    CheckAnnotationLevel.Failure,
                    $"The following libraries mentioned above have not been linked into the executable:\n\n    " +
                    string.Join("\n    ", missingLibraries) + 
                    "\n\nA potential fix could be to use the following line:\n\n" +
                    $"target_link_libraries( Game sfml-window sfml-system " + string.Join(" ", libraries) + " )"));
            }
        
            if(cppFiles.Count > 0)
            {
                annotations.Add(new NewCheckRunAnnotation(githubPath(cmakeFile.FullName),
                    lastLineOfLibraries, lastLineOfLibraries,
                    CheckAnnotationLevel.Failure,
                    $"The following .cpp files are not linked here, meaning they will not be built:\n\n    " +
                    string.Join("\n    ", cppFiles) + "\n\n" +
                    "These can be linked using the following syntax:\r\n" +
                    $"    add_library( [NAME] [NAME].cpp )\n\n" +
                    $"Hence, a potential fix could be to place the following lines into the file:\n\n" +
                    $"    " + string.Join("\n    ", cppFiles.Select(x => $"add_library( {Path.GetFileNameWithoutExtension(x)} {x} )"))));
            }
            return annotations;
        } 

        async Task doCheckConfig(GitHubClient client, CheckSuiteEventPayload info, CheckRun configRun, string folderPath, string leadingPath)
        {
            var cmakeLink = $"[CMakeLists.txt file]({info.Repository.HtmlUrl}/blob/{info.CheckSuite.HeadBranch}/CMakeTests.List)";
            var run = new CheckRunUpdate();
            DirectoryInfo x = new DirectoryInfo(folderPath);
            var annotations = await innerConfigCheck(x);
            int failures = annotations.Count(ann => ann.AnnotationLevel == CheckAnnotationLevel.Failure);
            int warnings = annotations.Count(ann => ann.AnnotationLevel == CheckAnnotationLevel.Warning);
            if(failures > 0)
            {
                run.Output = new NewCheckRunOutput("Config misconfigured", $"The {cmakeLink} appears to have {failures} invalid or omitted configurations, with {warnings} potential warnings.");
                run.Conclusion = CheckConclusion.Failure;
            }
            else
            {
                run.Output = new NewCheckRunOutput("No link failures", $"The {cmakeLink} does not appear to have any critical linking issues; there are {warnings} possible warnings");
                run.Conclusion = CheckConclusion.Success;
            }
            run.CompletedAt = DateTimeOffset.Now;
            run.Output.Annotations = annotations.ToImmutableArray();

            await client.Check.Run.Update(info.Repository.Id, configRun.Id, run);
        }

        async Task doChecks(GitHubClient client, CheckSuiteEventPayload info, CheckRun buildRun, CheckRun testRun, CheckRun configRun, ZipArchive archive)
        {
            var build = new CheckRunUpdate();
            var tests = new CheckRunUpdate();
            var path = Path.Combine(Program.BASE_PATH, extractFolder);
            Info($"Extracting to path: {path}");
            archive.ExtractToDirectory(path);
            Info($"Extracted.");
            var names = Directory.GetDirectories(path);
            Info($"Folders: " + String.Join(", ", names));

            var folderPath = names[0];

            var leadingPath = folderPath + Path.DirectorySeparatorChar;
            _currentPath = leadingPath;

            await doCheckConfig(client, info, configRun, folderPath, leadingPath);

            var x = runProcess($"{Path.Join(Program.BASE_PATH, "doGit.sh")} \"{folderPath}\"", true, out int exitCode);
            Info($"{exitCode} -- {x}");


            foreach (var cr in new[] { build, tests })
            {
                cr.Status = CheckStatus.Completed;
                cr.CompletedAt = DateTime.Now;
            }

            if (exitCode == 0)
            {
                foreach(var cr in new[] { build, tests})
                    cr.Conclusion = CheckConclusion.Success;
            } else if(exitCode == 68)
            { // CMake init failed
                build.Output = new NewCheckRunOutput("CMake Failed", "Failed to initialize CMake on the build machine.");
                build.Conclusion = CheckConclusion.Failure;
                tests.Output = new NewCheckRunOutput("Skipped", "As the program could not be built, tests will not be ran.");
                tests.Conclusion = CheckConclusion.Skipped;
            } else if(exitCode == 69)
            { // build failed
                build.Output = new NewCheckRunOutput("Build Failed", "The program could not be built.");
                build.Conclusion = CheckConclusion.Failure;
                tests.Output = new NewCheckRunOutput("Skipped", "As the program could not be built, tests will not be ran.");
                tests.Conclusion = CheckConclusion.Skipped;

                var buildAnnotations = new List<NewCheckRunAnnotation>();
                foreach(var line in x.Split('\n'))
                {
                    if(line.StartsWith(leadingPath) && line.Contains(": error: "))
                    { // format:
                        // /path/to/file.h:LINE:COLUMN: error: MESSAGE
                        var restLine = line.Replace(leadingPath, "");
                        var firstColon = restLine.IndexOf(':');

                        var errPath = restLine.Substring(0, firstColon);
                        var secondColon = restLine.IndexOf(':', firstColon + 1);
                        var lineNum = int.Parse(restLine.Substring(firstColon + 1, secondColon - firstColon - 1));

                        var thirdColon = restLine.IndexOf(':', secondColon + 1);
                        var colNum = int.Parse(restLine.Substring(secondColon + 1, thirdColon - secondColon - 1));

                        var fourthColon = restLine.IndexOf(':', thirdColon + 1);
                        var errMessage = restLine.Substring(fourthColon + 1);

                        var ann = new NewCheckRunAnnotation(errPath, lineNum, lineNum, CheckAnnotationLevel.Failure, errMessage)
                        {
                            StartColumn = colNum,
                            EndColumn = colNum,
                        };
                        buildAnnotations.Add(ann);
                    }
                }
                build.Output.Annotations = buildAnnotations.ToImmutableArray();
            } else if(exitCode == 70)
            { // tests failed
                build.Conclusion = CheckConclusion.Success;

                var testsFiles = Directory.EnumerateFiles(Path.Combine(folderPath, "tests/bin"), "*.xml");
                XmlSerializer serializer = new XmlSerializer(typeof(Catch));
                Dictionary<string, Catch> results = new Dictionary<string, Catch>();
                var annotations = new List<NewCheckRunAnnotation>();
                string summary = "";
                foreach (var testFile in testsFiles)
                {
                    using var fs = new FileStream(testFile, System.IO.FileMode.Open, FileAccess.Read);
                    var catchObject = serializer.Deserialize(fs) as Catch;

                    var testName = catchObject.name;
                    results[testName] = catchObject;
                    int total = catchObject.OverallResults.successes + catchObject.OverallResults.failures + catchObject.OverallResults.expectedFailures;
                    int successes = catchObject.OverallResults.successes + catchObject.OverallResults.expectedFailures;
                    summary += $"{testName}: {successes}/{total}, failed: {catchObject.OverallResults.failures}\n";

                    foreach (var section in catchObject.Group.TestCase.Section)
                    {
                        if(section.Expression != null && section.Expression.success == false)
                        { // test failed
                            var exp = section.Expression;
                            var expPath = githubPath(exp.filename);

                            var ann = new NewCheckRunAnnotation(expPath, exp.line, exp.line, CheckAnnotationLevel.Failure,
                                $"{exp.type} test was not met; original:\n    {exp.Original.Trim()}\n\nExpanded:\n    {exp.Expanded.Trim()}")
                            {
                                Title = section.name,
                            };
                            annotations.Add(ann);
                        }
                    }
                }
                int sumFailures = results.Values.Sum(x => x.OverallResults.failures);
                tests.Output = new NewCheckRunOutput($"{sumFailures} Tests Failed", summary)
                {
                    Annotations = annotations.ToImmutableArray()
                };
                tests.Conclusion = CheckConclusion.Failure;
            }

            await client.Check.Run.Update(info.Repository.Id, buildRun.Id, build);
            await client.Check.Run.Update(info.Repository.Id, testRun.Id, tests);
        }

        async Task handleWebhook(JsonWebhook webhook)
        {
            var serialiser = new Octokit.Internal.SimpleJsonSerializer();

            if(webhook.EventName == "ping")
            {
                Info("Ping webhook: " + webhook.JsonBody);
            } else if(webhook.EventName == "check_suite")
            {
                Info("Received check_suite");
                var info = serialiser.Deserialize<CheckSuiteEventPayload>(webhook.JsonBody);
                Info($"Action: {info.Action}");
                if (!info.Action.Contains("requested")) // also includes 'rerequested'
                    return;

                var token = await getClient().GitHubApps.CreateInstallationToken(info.Installation.Id);
                Info("Got installation token");
                var client = getClient(token.Token);
                Info("Got client");

                var build = new NewCheckRun("cmake-build", info.CheckSuite.HeadSha)
                {
                    Status = CheckStatus.InProgress
                };
                var tests = new NewCheckRun("catch2-tests", info.CheckSuite.HeadSha)
                {
                    Status = CheckStatus.Queued
                };
                var config = new NewCheckRun("cmake-config", info.CheckSuite.HeadSha)
                {
                    Status = CheckStatus.InProgress
                };

                var buildRun = await client.Check.Run.Create(info.Repository.Id, build);
                var testRun = await client.Check.Run.Create(info.Repository.Id, tests);
                var configRun = await client.Check.Run.Create(info.Repository.Id, config);

                var content = await client.Repository.Content.GetArchive(info.Repository.Id,  ArchiveFormat.Zipball, info.CheckSuite.HeadSha);
                Info($"Got content: {content.Length} bytes");


                try
                {

                    using (var stream = new MemoryStream(content))
                    {
                        var zip = new ZipArchive(stream);
                        await doChecks(client, info, buildRun, testRun, configRun, zip);
                    }
                } catch(Exception e)
                {
                    Error(e, "CheckRun");
                }
                finally
                {
                    try
                    {
                        var path = Path.Combine(Program.BASE_PATH, extractFolder);
                        Directory.Delete(path, true);
                    } catch { }
                }
            } else
            {
                Info($"Received {webhook.EventName}");
            }
        }
    }

    public struct JsonWebhook
    {
        public string JsonBody { get; set; }
        public string EventName { get; set; }

        public string HookId { get; set; }
    }


    // NOTE: Generated code may require at least .NET Framework 4.5 or .NET Core/Standard 2.0.
    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "", IsNullable = false)]
    public partial class Catch
    {

        private CatchGroup groupField;

        private CatchOverallResults overallResultsField;

        private string nameField;

        /// <remarks/>
        public CatchGroup Group
        {
            get
            {
                return this.groupField;
            }
            set
            {
                this.groupField = value;
            }
        }

        /// <remarks/>
        public CatchOverallResults OverallResults
        {
            get
            {
                return this.overallResultsField;
            }
            set
            {
                this.overallResultsField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string name
        {
            get
            {
                return this.nameField;
            }
            set
            {
                this.nameField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class CatchGroup
    {

        private CatchGroupTestCase testCaseField;

        private CatchGroupOverallResults overallResultsField;

        private string nameField;

        /// <remarks/>
        public CatchGroupTestCase TestCase
        {
            get
            {
                return this.testCaseField;
            }
            set
            {
                this.testCaseField = value;
            }
        }

        /// <remarks/>
        public CatchGroupOverallResults OverallResults
        {
            get
            {
                return this.overallResultsField;
            }
            set
            {
                this.overallResultsField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string name
        {
            get
            {
                return this.nameField;
            }
            set
            {
                this.nameField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class CatchGroupTestCase
    {

        private CatchGroupTestCaseSection[] sectionField;

        private CatchGroupTestCaseOverallResult overallResultField;

        private string nameField;

        private string filenameField;

        private int lineField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("Section")]
        public CatchGroupTestCaseSection[] Section
        {
            get
            {
                return this.sectionField;
            }
            set
            {
                this.sectionField = value;
            }
        }

        /// <remarks/>
        public CatchGroupTestCaseOverallResult OverallResult
        {
            get
            {
                return this.overallResultField;
            }
            set
            {
                this.overallResultField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string name
        {
            get
            {
                return this.nameField;
            }
            set
            {
                this.nameField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string filename
        {
            get
            {
                return this.filenameField;
            }
            set
            {
                this.filenameField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public int line
        {
            get
            {
                return this.lineField;
            }
            set
            {
                this.lineField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class CatchGroupTestCaseSection
    {

        private CatchGroupTestCaseSectionExpression expressionField;

        private CatchGroupTestCaseSectionOverallResults overallResultsField;

        private string nameField;

        private string filenameField;

        private int lineField;

        /// <remarks/>
        public CatchGroupTestCaseSectionExpression Expression
        {
            get
            {
                return this.expressionField;
            }
            set
            {
                this.expressionField = value;
            }
        }

        /// <remarks/>
        public CatchGroupTestCaseSectionOverallResults OverallResults
        {
            get
            {
                return this.overallResultsField;
            }
            set
            {
                this.overallResultsField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string name
        {
            get
            {
                return this.nameField;
            }
            set
            {
                this.nameField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string filename
        {
            get
            {
                return this.filenameField;
            }
            set
            {
                this.filenameField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public int line
        {
            get
            {
                return this.lineField;
            }
            set
            {
                this.lineField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class CatchGroupTestCaseSectionExpression
    {

        private string originalField;

        private string expandedField;

        private bool successField;

        private string typeField;

        private string filenameField;

        private int lineField;

        /// <remarks/>
        public string Original
        {
            get
            {
                return this.originalField;
            }
            set
            {
                this.originalField = value;
            }
        }

        /// <remarks/>
        public string Expanded
        {
            get
            {
                return this.expandedField;
            }
            set
            {
                this.expandedField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public bool success
        {
            get
            {
                return this.successField;
            }
            set
            {
                this.successField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string filename
        {
            get
            {
                return this.filenameField;
            }
            set
            {
                this.filenameField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public int line
        {
            get
            {
                return this.lineField;
            }
            set
            {
                this.lineField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class CatchGroupTestCaseSectionOverallResults
    {

        private int successesField;

        private int failuresField;

        private int expectedFailuresField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public int successes
        {
            get
            {
                return this.successesField;
            }
            set
            {
                this.successesField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public int failures
        {
            get
            {
                return this.failuresField;
            }
            set
            {
                this.failuresField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public int expectedFailures
        {
            get
            {
                return this.expectedFailuresField;
            }
            set
            {
                this.expectedFailuresField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class CatchGroupTestCaseOverallResult
    {

        private bool successField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public bool success
        {
            get
            {
                return this.successField;
            }
            set
            {
                this.successField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class CatchGroupOverallResults
    {

        private int successesField;

        private int failuresField;

        private int expectedFailuresField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public int successes
        {
            get
            {
                return this.successesField;
            }
            set
            {
                this.successesField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public int failures
        {
            get
            {
                return this.failuresField;
            }
            set
            {
                this.failuresField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public int expectedFailures
        {
            get
            {
                return this.expectedFailuresField;
            }
            set
            {
                this.expectedFailuresField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class CatchOverallResults
    {

        private int successesField;

        private int failuresField;

        private int expectedFailuresField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public int successes
        {
            get
            {
                return this.successesField;
            }
            set
            {
                this.successesField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public int failures
        {
            get
            {
                return this.failuresField;
            }
            set
            {
                this.failuresField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public int expectedFailures
        {
            get
            {
                return this.expectedFailuresField;
            }
            set
            {
                this.expectedFailuresField = value;
            }
        }
    }


}
