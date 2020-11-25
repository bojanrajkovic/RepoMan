using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RepoMan.IO;
using RepoMan.Repository.Clients;
using RepoMan.Repository.Models;
using Serilog;

namespace RepoMan.Repository
{
    /// <summary>
    /// Handles the cache management (both reading and writing) associated with historical repositories, since querying the GitHub API is slow, and rate-limited.
    /// Under the hood, many methods utilize SemaphoreSlim for read/write thread-safety, which is why many methods are async Tasks instead of values.
    /// </summary>
    class RepositoryManager :
        IRepoManager
    {
        public WatchedRepository Repository { get; }

        private readonly IRepositoryClient _prReader;
        private readonly ICacheManager _cacheManager;
        private readonly TimeSpan _dosBuffer;
        private readonly ILogger _logger;
        
        private readonly SemaphoreSlim _byNumberLock = new(1, 1);
        private readonly Dictionary<int, PullRequest> _byNumber;

        private RepositoryManager(
            WatchedRepository repository,
            IRepositoryClient prReader,
            ICacheManager cacheManager,
            TimeSpan prApiDosBuffer,
            Dictionary<int, PullRequest> byNumber,
            ILogger logger)
        {
            Repository = repository;
            _prReader = prReader;
            _cacheManager = cacheManager;
            _dosBuffer = prApiDosBuffer;
            _logger = logger;
            _byNumber = byNumber;
        }

        public static async Task<IRepoManager> InitializeAsync(
            WatchedRepository repository,
            IRepositoryClient prReader,
            ICacheManager cacheManager,
            TimeSpan prApiDosBuffer,
            bool refreshFromUpstream,
            ILogger logger)
        {
            if (repository is null)
            {
                throw new ArgumentNullException(nameof(repository));
            }
            
            if (prReader is null)
            {
                throw new ArgumentNullException(nameof(prReader));
            }

            if (cacheManager is null)
            {
                throw new ArgumentNullException(nameof(cacheManager));
            }
            
            if (prApiDosBuffer < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(prApiDosBuffer));
            }
            
            if (logger is null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
            
            logger.Information("Initializing repository history manager cache");
            var timer = Stopwatch.StartNew();

            IList<PullRequest> prs = new List<PullRequest>();
            try
            {
                logger.Information("Reading the cache");
                prs = await cacheManager.LoadAsync(repository.Owner, repository.RepositoryName);
            }
            catch (Exception)
            {
                logger.Information("Cache file does not exist, therefore one will be created.");
            }
            var byNumber = prs.ToDictionary(pr => pr.Number);
            timer.Stop();
            logger.Information($"Initialized the cache with {byNumber.Count:N0} pull requests in {timer.ElapsedMilliseconds:N0}ms");
            
            var repoHistoryMgr = new RepositoryManager(repository, prReader, cacheManager, prApiDosBuffer, byNumber, logger);

            if (!refreshFromUpstream)
            {
                return repoHistoryMgr;
            }
            
            // If there's nothing in the cache, go look for stuff.
            await repoHistoryMgr.RefreshFromUpstreamAsync(PullRequestState.Closed);
            return repoHistoryMgr;
        }

        /// <summary>
        /// </summary>
        /// <param name="stateFilter"></param>
        /// <returns></returns>
        public async Task RefreshFromUpstreamAsync(PullRequestState stateFilter)
        {
            var prs = await _prReader.GetAllPullRequestsAsync(Repository, stateFilter);
            var unknownPrs = new List<PullRequest>(prs.Count());
            
            try
            {
                await _byNumberLock.WaitAsync();
                var unknownPrsQuery = prs.Where(pr => !_byNumber.ContainsKey(pr.Number) || _byNumber[pr.Number].IsFullyInterrogated == false);
                unknownPrs.AddRange(unknownPrsQuery);
            }
            finally
            {
                _byNumberLock.Release();
            }

            var completedPrs = await PopulateCommentsAndApprovalsAsync(unknownPrs);

            if (completedPrs.Any())
            {
                await UpdateMemoryCacheAsync(completedPrs);
                await PersistCacheAsync();
            }
        }

        /// <summary>
        /// Async-await and thread-safe update for recently-imported pull requests, and persists the updated collection to its home. The import is naive: if you
        /// supply a bad collection to be imported, your cache will be fried with bad data. If you supply incomplete pull requests, they will overwrite completed
        /// pull requests with no warning.
        /// </summary>
        /// <param name="prRoots"></param>
        /// <returns></returns>
        private async ValueTask UpdateMemoryCacheAsync(List<PullRequest> prRoots)
        {
            if (!prRoots.Any())
            {
                return;
            }
            
            try
            {
                await _byNumberLock.WaitAsync();
                foreach (var updatedPr in prRoots)
                {
                    _byNumber[updatedPr.Number] = updatedPr;
                }
            }
            finally
            {
                _byNumberLock.Release();
            }
        }

        private async ValueTask PersistCacheAsync()
        {
            var updates = new List<PullRequest>(_byNumber.Count);    // Dirty read is OK for minor GC optimization
            try
            {
                await _byNumberLock.WaitAsync();
                updates.AddRange(_byNumber.Values);
            }
            finally
            {
                _byNumberLock.Release();
            }

            await _cacheManager.SaveAsync(updates, Repository.Owner, Repository.RepositoryName);
        }

        /// <summary>
        /// Deep queries the git API to retrieve all comments, approvals, etc. for each of the specified pull requests to its data store. If the git API starts
        /// rejecting queries (due to rate limits or whatever), the pull requests that have had their comments approval and comment data fully populated are
        /// returned.
        /// </summary>
        /// <returns>The collection of fully-updated pull requests</returns>
        private async ValueTask<List<PullRequest>> PopulateCommentsAndApprovalsAsync(List<PullRequest> unfinishedPrs)
        {
            _logger.Information($"{unfinishedPrs.Count:N0} pull requests with incomplete data to be populated");
            var successfullyUpdatePrs = new List<PullRequest>(unfinishedPrs.Count);
            
            foreach (var pr in unfinishedPrs)
            {
                _logger.Information($"Updating {pr.Number}");
                var loopTimer = Stopwatch.StartNew();
                var succeeded = await _prReader.TryFillCommentGraphAsync(pr);
                loopTimer.Stop();
                
                if (!succeeded)
                {
                    _logger.Information($"Pull request {pr.Number} update failed in {loopTimer.ElapsedMilliseconds:N0}");
                    break;
                }
                _logger.Information($"Pull request {pr.Number} update succeeded in {loopTimer.ElapsedMilliseconds:N0}");
                
                successfullyUpdatePrs.Add(pr);

                await Task.Delay(_dosBuffer);
            }

            return successfullyUpdatePrs;
        }

        public async ValueTask<int> GetPullRequestCount()
        {
            try
            {
                await _byNumberLock.WaitAsync();
                return _byNumber.Count;
            }
            finally
            {
                _byNumberLock.Release();
            }
        }

        public async ValueTask<IList<Comment>> GetAllCommentsForRepo()
        {
            try
            {
                await _byNumberLock.WaitAsync();
                var commitComments = _byNumber.SelectMany(pr => pr.Value.CommitComments);
                var diffComments = _byNumber.SelectMany(pr => pr.Value.DiffComments);
                var reviewComments = _byNumber.SelectMany(pr => pr.Value.ReviewComments);
                return commitComments.Concat(diffComments).Concat(reviewComments).ToList();
            }
            finally
            {
                _byNumberLock.Release();
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="prNumber"></param>
        /// <returns>Null if the pull request number is not found</returns>
        public async ValueTask<PullRequest?> GetPullRequestByNumber(int prNumber)
        {
            try
            {
                await _byNumberLock.WaitAsync();
                return _byNumber.ContainsKey(prNumber)
                    ? _byNumber[prNumber]
                    : null;
            }
            finally
            {
                _byNumberLock.Release();
            }
        }

        /// <summary>
        /// Thread-safe, lazy, non-copy read from the in-memory cache
        /// </summary>
        /// <returns></returns>
        public async ValueTask<IList<PullRequest>> GetPullRequestsAsync()
        {
            try
            {
                await _byNumberLock.WaitAsync();
                var prs = new List<PullRequest>(_byNumber.Count);
                prs.AddRange(_byNumber.Values);
                return prs;
            }
            finally
            {
                _byNumberLock.Release();
            }
        }
    }
}
