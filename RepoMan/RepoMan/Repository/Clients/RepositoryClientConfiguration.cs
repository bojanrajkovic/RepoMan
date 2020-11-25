using System.Collections.Generic;

namespace RepoMan.Repository.Clients
{
    class RepositoryClientConfiguration
    {
        public IReadOnlyList<GitHubRepositoryConfiguration> GitHub { get; set; }
        public IReadOnlyList<BitBucketRepositoryConfiguration> BitBucket { get; set; }
    }
}
