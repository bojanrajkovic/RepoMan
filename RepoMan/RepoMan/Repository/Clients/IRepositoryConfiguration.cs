namespace RepoMan.Repository.Clients
{
    interface IRepositoryConfiguration
    {
        string ApiToken { get; set; }
        string Host { get; set; }
    }
}
