using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepoInspector.IO;
using RepoInspector.Records;

namespace RepoInspector.Repository
{
    public class RepoManagerFactory :
        IRepoManagerFactory
    {
        private readonly IPullRequestReaderFactory _prReaderFactory;
        private readonly IPullRequestCacheManager _cacheManager;
        private readonly TimeSpan _prApiDosBuffer;
        private readonly ILogger _logger;

        public RepoManagerFactory(IPullRequestReaderFactory prReaderFactory, IPullRequestCacheManager cacheManager, IOptionsSnapshot<RepoInspectorOptions> optionsSnapshot, ILogger<RepoManagerFactory> logger)
        {
            _prReaderFactory = prReaderFactory ?? throw new ArgumentNullException(nameof(prReaderFactory));
            _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
            _prApiDosBuffer = optionsSnapshot.Value.DenialOfServiceBuffer;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IRepoManager> GetManagerAsync(WatchedRepository repo, bool refreshFromUpstream)
        {
            var prReader = _prReaderFactory.GetReader(repo);
            var manager = await RepositoryManager.InitializeAsync(
                repo.Owner,
                repo.Name,
                repo.Url,
                prReader,
                _cacheManager,
                _prApiDosBuffer,
                refreshFromUpstream,
                _logger);
            return manager;
        }
    }
}
