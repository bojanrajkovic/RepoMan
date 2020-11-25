using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;
using RepoMan.Repository.Clients;
using RepoMan.Repository.Models;
using PullRequest = RepoMan.Repository.Models.PullRequest;

namespace RepoMan.Repository
{
    interface IRepoManager
    {
        WatchedRepository Repository { get; }
        
        /// <summary>
        /// Check the upstream git repo API for any pull requests that the cache manager doesn't know about.
        /// </summary>
        /// <param name="stateFilter"></param>
        /// <returns></returns>
        Task RefreshFromUpstreamAsync(PullRequestState stateFilter);
        
        /// <summary>
        /// Returns the number of pull requests in the cache that have been fully populated
        /// </summary>
        /// <returns></returns>
        ValueTask<int> GetPullRequestCount();

        /// <summary>
        ///
        /// </summary>
        /// <param name="prNumber"></param>
        /// <returns>null if the pull request number is not present</returns>
        ValueTask<PullRequest?> GetPullRequestByNumber(int prNumber);

        ValueTask<IList<PullRequest>> GetPullRequestsAsync();

        /// <summary>
        /// Returns the comments on each PR
        /// </summary>
        /// <returns></returns>
        ValueTask<IList<Comment>> GetAllCommentsForRepo();
    }
}
