namespace RepoMan.Repository.Clients
{
    interface IRepositoryClientFactory
    {
        IRepositoryClient GetRepositoryClient(WatchedRepository repository);
    }
}
