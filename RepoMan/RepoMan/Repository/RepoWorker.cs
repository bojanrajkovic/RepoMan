using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Octokit;
using RepoMan.Analysis;
using RepoMan.Analysis.ApprovalAnalyzers;
using RepoMan.Repository.Clients;
using Serilog;

namespace RepoMan.Repository
{
    class RepoWorker :
        IWorker
    {
        private readonly IRepoManager _repo;
        private readonly string _fullName;
        private readonly IApprovalAnalyzer _approvalAnalyzer;
        private readonly ICommentAnalyzer _commentAnalyzer;
        private readonly IWordCounter _wordCounter;
        private readonly IRepositoryHealthAnalyzer _repoHealthAnalyzer;
        private readonly ILogger _logger;

        public RepoWorker(
            IRepoManager repo,
            IApprovalAnalyzer approvalAnalyzer,
            ICommentAnalyzer commentAnalyzer,
            IWordCounter wordCounter,
            IRepositoryHealthAnalyzer repoHealthAnalyzer,
            ILogger logger)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _fullName = $"{repo.Repository.Owner}:{repo.Repository.RepositoryName}";
            _approvalAnalyzer = approvalAnalyzer ?? throw new ArgumentNullException(nameof(approvalAnalyzer));
            _commentAnalyzer = commentAnalyzer ?? throw new ArgumentNullException(nameof(commentAnalyzer));
            _wordCounter = wordCounter ?? throw new ArgumentNullException(nameof(wordCounter));
            _repoHealthAnalyzer = repoHealthAnalyzer ?? throw new ArgumentNullException(nameof(repoHealthAnalyzer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ExecuteAsync()
        {
            _logger.Information($"{_fullName} work loop starting");
            var timer = Stopwatch.StartNew();
            
            // TODO: Refresh from upstream first
            await _repo.RefreshFromUpstreamAsync(PullRequestState.Closed);
            var pullRequestSnapshots = await _repo.GetPullRequestsAsync();

            _logger.Information($"{_fullName} comment analysis starting for {pullRequestSnapshots.Count:N0} pull requests");
            var analysisTimer = Stopwatch.StartNew();
            var singularPrSnapshots = pullRequestSnapshots
                .Select(pr => _commentAnalyzer.CalculateCommentStatistics(pr))
                .ToList();
            analysisTimer.Stop();
            _logger.Information($"{_fullName} comment analysis completed for {pullRequestSnapshots.Count:N0} pull requests in {analysisTimer.Elapsed.ToMicroseconds():N0} microseconds");
            
            // TODO: Do something with the comment statistics
            
            _logger.Information($"{_fullName} repository analysis starting for {pullRequestSnapshots.Count:N0} pull requests");
            analysisTimer = Stopwatch.StartNew();
            var repoHealth = _repoHealthAnalyzer.CalculateRepositoryHealthStatistics(singularPrSnapshots);
            analysisTimer.Stop();
            _logger.Information($"{_fullName} repository analysis completed for {pullRequestSnapshots.Count:N0} pull requests in {analysisTimer.Elapsed.ToMicroseconds():N0} microseconds");

            // TODO: Do something with the repo health snapshot
            
            
            timer.Stop();
            _logger.Information($"{_fullName} work loop completed in {timer.ElapsedMilliseconds:N0}ms");
        }
    }
}
