﻿using Octokit;
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

        async Task doChecks(GitHubClient client, CheckSuiteEventPayload info, CheckRun buildRun, CheckRun testRun, ZipArchive archive)
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
                tests.Conclusion = CheckConclusion.Cancelled;
            } else if(exitCode == 69)
            { // build failed
                build.Output = new NewCheckRunOutput("Build Failed", "The program could not be built.");
                build.Conclusion = CheckConclusion.Failure;
                tests.Output = new NewCheckRunOutput("Skipped", "As the program could not be built, tests will not be ran.");
                tests.Conclusion = CheckConclusion.Cancelled;

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
                            var expPath = exp.filename.Replace(leadingPath, "");

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
                    Status = CheckStatus.InProgress
                };

                var buildRun = await client.Check.Run.Create(info.Repository.Id, build);
                var testRun = await client.Check.Run.Create(info.Repository.Id, tests);

                var content = await client.Repository.Content.GetArchive(info.Repository.Id,  ArchiveFormat.Zipball, info.CheckSuite.HeadSha);
                Info($"Got content: {content.Length} bytes");


                try
                {

                    using (var stream = new MemoryStream(content))
                    {
                        var zip = new ZipArchive(stream);
                        await doChecks(client, info, buildRun, testRun, zip);
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
