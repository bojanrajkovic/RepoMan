namespace RepoMan.Repository.Clients
{
    class BitBucketRepositoryConfiguration
        : IRepositoryConfiguration
    {
        public string ApiToken { get; set; }
        public string Host { get; set; }
    }
}
