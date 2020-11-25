using System;
using System.Linq;
using Humanizer;
using Microsoft.Extensions.Options;
using Octokit;

namespace RepoMan.Repository.Clients
{
    // TODO: Figure out how to implement something like HttpClientFactory's short-lived cache of GitHub clients correctly.
    // TODO: Probably need to use object pooling with automatic recycling? - BR, 11/24/2020
    class RepositoryClientFactory : IRepositoryClientFactory
    {
        private readonly IOptionsSnapshot<RepositoryClientConfiguration> _clientConfigurationSnapshot;

        public RepositoryClientFactory(IOptionsSnapshot<RepositoryClientConfiguration> clientConfigurationSnapshot)
        {
            _clientConfigurationSnapshot = clientConfigurationSnapshot;
        }

        public IRepositoryClient GetRepositoryClient(WatchedRepository repository)
        {
            switch (repository.RepositoryKind)
            {
                case RepositoryKind.BitBucket:
                    return null;
                case RepositoryKind.GitHub:
                    return new GitHubRepositoryClient(CreateGitHubClient(repository));
                default:
                    throw new ArgumentException(
                        $"Unknown watched repository kind {repository.RepositoryKind.Humanize()}");
            }
        }

        GitHubClient CreateGitHubClient(WatchedRepository repository)
        {
            var ghConfiguration = _clientConfigurationSnapshot.Value.GitHub;
            var ghRepoConfig = ghConfiguration?.SingleOrDefault(r => r.Host == repository.BaseUrl);

            if (ghConfiguration == null || ghRepoConfig == null)
            {
                throw new ArgumentException($"No configuration for GitHub instance at {repository.BaseUrl}");
            }

            var github = new Uri(repository.BaseUrl);
            var client = new GitHubClient(new ProductHeaderValue("repoman-health-metrics"), github);
            var auth = new Credentials(ghRepoConfig.ApiToken);
            client.Credentials = auth;
            return client;
        }
    }
}
