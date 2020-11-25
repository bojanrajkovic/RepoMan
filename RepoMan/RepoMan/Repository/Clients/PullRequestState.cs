namespace RepoMan.Repository.Clients
{
    enum PullRequestState
    {
        // DO NOT CHANGE THE ORDER OF THESE. IT MUST MATCH ItemStateFilter FROM OCTOKIT. - BR, 11/24/2020
        Open,
        Closed,
        All
    }
}