namespace RepoMan.Repository.Clients
{
    class GitHubRepositoryConfiguration
        : IRepositoryConfiguration
    {
        public string ApiToken { get; set; }
        public string Host { get; set; }
    }
}
