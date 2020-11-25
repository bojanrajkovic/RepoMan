using System.Collections.Generic;
using System.Threading.Tasks;
using RepoMan.Repository.Models;

namespace RepoMan.IO
{
        interface ICacheManager
    {
        ValueTask SaveAsync(IList<PullRequest> prDetails, string repoOwner, string repoName);
        ValueTask<IList<PullRequest>> LoadAsync(string repoOwner, string repoName);
    }
}
