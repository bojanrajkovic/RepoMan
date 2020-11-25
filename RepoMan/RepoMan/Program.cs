using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Octokit;
using RepoMan.Analysis;
using RepoMan.Analysis.ApprovalAnalyzers;
using RepoMan.IO;
using RepoMan.Repository;
using RepoMan.Repository.Clients;
using Serilog;

namespace RepoMan
{
    class Program
    {
        private static readonly string TokenPath = Path.Combine(GetScratchDirectory(), "repoman-pan.secret");
        private static readonly string ConfigPath = Path.Combine(GetScratchDirectory(), "repoman-config.json");
        private static readonly string ScratchDir = GetScratchDirectory();
        private static readonly string Url = "https://github.com";
        private static readonly string Token = File.ReadAllText(TokenPath).Trim();
        private static readonly JsonSerializerSettings JsonSerializerSettings = GetDebugJsonSerializerSettings();
        private static readonly ILogger Logger = GetLogger();

        static async Task Main(string[] args)
        {
            Console.WriteLine(Environment.CurrentDirectory);

            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddJsonFile(ConfigPath, optional: false, reloadOnChange: true)
                .Build();

            var serviceCollection = new ServiceCollection()
                .Configure<PullRequestConstants>(RepositoryKind.GitHub.ToString(),
                    configuration.GetSection("PRConstants:GitHub"))
                .Configure<PullRequestConstants>(RepositoryKind.BitBucket.ToString(),
                    configuration.GetSection("PRConstants:BitBucket"))
                .Configure<RepositoryClientConfiguration>(configuration.GetSection("ClientConfiguration"))
                .AddTransient(sp =>
                {
                    var prConstantsAccessor = sp.GetRequiredService<IOptionsSnapshot<PullRequestConstants>>();
                    var prConstants = prConstantsAccessor.Get(RepositoryKind.GitHub.ToString());
                    return new GitHubApprovalAnalyzer(
                        prConstants.ExplicitApprovals,
                        prConstants.ExplicitNonApprovals,
                        prConstants.ImplicitApprovals);
                }).AddTransient(sp =>
                {
                    var prConstantsAccessor = sp.GetRequiredService<IOptionsSnapshot<PullRequestConstants>>();
                    var prConstants = prConstantsAccessor.Get(RepositoryKind.BitBucket.ToString());
                    return new BitBucketApprovalAnalyzer(
                        prConstants.ExplicitApprovals,
                        prConstants.ExplicitNonApprovals,
                        prConstants.ImplicitApprovals);
                }).AddTransient<RepositoryClientFactory>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var wordCounter = new WordCounter();
            var approvalAnalyzer = serviceProvider.GetRequiredService<GitHubApprovalAnalyzer>();
            var commentAnalyzer = new CommentAnalyzer(approvalAnalyzer, wordCounter);
            var repoHealthAnalyzer = new RepoHealthAnalyzer();
            var fs = new Filesystem();
            var cacheManager = new FilesystemCacheManager(fs, ScratchDir, JsonSerializerSettings);
            var dosBuffer = TimeSpan.FromSeconds(0.1);
            
            var watchedRepos = GetWatchedRepositories()
                .GroupBy(r => r.ApiToken);

            var repoMgrInitializationQuery =
                from kvp in watchedRepos
                from repo in kvp
                let prReader = serviceProvider.GetRequiredService<RepositoryClientFactory>().GetRepositoryClient(repo)
                select RepositoryManager.InitializeAsync(
                    repo,
                    prReader,
                    cacheManager,
                    dosBuffer,
                    refreshFromUpstream: true,
                    Logger);
            var watcherInitializationTasks = repoMgrInitializationQuery.ToList();
            await Task.WhenAll(watcherInitializationTasks);

            var repoWorkers = watcherInitializationTasks
                .Select(t => t.Result)
                .Select(rm => new RepoWorker(rm, approvalAnalyzer, commentAnalyzer, wordCounter, repoHealthAnalyzer, Logger))
                .ToList();
            
            // Create a BackgroundService with the collection of workers, and update the stats every 4 hours or so
        }

        private static string GetScratchDirectory()
        {
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            const int netCoreNestingLevel = 5;

            for (var i = 0; i < netCoreNestingLevel; i++)
            {
                path = Directory.GetParent(path).FullName;
            }

            return Path.Combine(path, "scratch");
        }
        
        private static ILogger GetLogger() =>
            new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();
        
        private static JsonSerializerSettings GetDebugJsonSerializerSettings()
        {
            return new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                //For demo purposes:
                DefaultValueHandling = DefaultValueHandling.Include,
                Formatting = Formatting.Indented,
                //Otherwise:
                // DefaultValueHandling = DefaultValueHandling.Ignore,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                Converters = new List<JsonConverter> { new StringEnumConverter(), },
            };
        }

        private static List<WatchedRepository> GetWatchedRepositories()
        {
            return new List<WatchedRepository>
            {
                new WatchedRepository
                {
                    Owner = "alex",
                    RepositoryName = "nyt-2020-election-scraper",
                    Description = "NYT election data scraper and renderer",
                    ApiToken = Token,
                    BaseUrl = "https://github.com",
                    RepositoryKind = RepositoryKind.GitHub,
                },
                new WatchedRepository
                {
                    Owner = "rianjs",
                    RepositoryName = "ical.net",
                    Description = "RFC-5545 ical data library",
                    ApiToken = Token,
                    BaseUrl = "https://github.com",
                    RepositoryKind = RepositoryKind.GitHub,
                },
            };
        }
    }
}
