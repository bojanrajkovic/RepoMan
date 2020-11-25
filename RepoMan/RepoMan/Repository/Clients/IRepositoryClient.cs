using System.Collections.Generic;
using System.Threading.Tasks;
using RepoMan.Repository.Models;

namespace RepoMan.Repository.Clients
{
    interface IRepositoryClient
    {
        ValueTask<IEnumerable<PullRequest>> GetAllPullRequestsAsync(WatchedRepository repository, PullRequestState state);
        ValueTask<bool> TryFillCommentGraphAsync(PullRequest pullRequest);
    }
}
