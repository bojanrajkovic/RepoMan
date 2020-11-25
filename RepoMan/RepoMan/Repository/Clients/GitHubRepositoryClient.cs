using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octokit;

using PullRequest = RepoMan.Repository.Models.PullRequest;

namespace RepoMan.Repository.Clients
{
    class GitHubRepositoryClient
        : IRepositoryClient
    {
        private readonly GitHubClient _client;

        public GitHubRepositoryClient(GitHubClient client)
        {
            _client = client;
        }

        public async ValueTask<IEnumerable<PullRequest>> GetAllPullRequestsAsync(WatchedRepository repository, PullRequestState state)
        {
            _ = repository ?? throw new ArgumentNullException(nameof(repository));
            var prOpts = new PullRequestRequest {
                // See comment in PullRequestState enum.
                State = (ItemStateFilter)(int)state,
                SortProperty = PullRequestSort.Created,
                SortDirection = SortDirection.Ascending,
            };
            var pullRequests = await _client.PullRequest.GetAllForRepository(
                repository.Owner,
                repository.RepositoryName,
                prOpts);

            return pullRequests
                .AsParallel()
                .Select(pr => new PullRequest(repository, pr))
                .ToList();
        }

        public async ValueTask<bool> TryFillCommentGraphAsync(PullRequest pullRequest)
        {
            string repoOwner = pullRequest.Repository.Owner, repoName = pullRequest.Repository.RepositoryName;

            // Comments on specific lines and ranges of lines in the changed code
            var diffReviewCommentsTask = _client.PullRequest.ReviewComment.GetAll(repoOwner, repoName, pullRequest.Number);

            // State transitions (APPROVED), and comments associated with them
            var approvalSummariesTask = _client.PullRequest.Review.GetAll(repoOwner, repoName, pullRequest.Number);

            // These are the comments on the PR in general, not associated with an approval, or with a commit, or with something in the diff
            var generalPrCommentsTask = _client.Issue.Comment.GetAllForIssue(repoOwner, repoName, pullRequest.Number);

            await Task.WhenAll(diffReviewCommentsTask, approvalSummariesTask, generalPrCommentsTask);

            if (diffReviewCommentsTask.IsFaulted || generalPrCommentsTask.IsFaulted || approvalSummariesTask.IsFaulted)
            {
                return false;
            }

            // TODO: Figure out how to make this not side-effecty. Probably just use C# 9 records and the
            // .With construct they enable.
            pullRequest.UpdateDiffComments(diffReviewCommentsTask.Result);
            pullRequest.UpdateDiscussionComments(generalPrCommentsTask.Result);
            pullRequest.UpdateStateTransitionComments(approvalSummariesTask.Result);
            pullRequest.IsFullyInterrogated = true;

            return true;
        }
    }
}
