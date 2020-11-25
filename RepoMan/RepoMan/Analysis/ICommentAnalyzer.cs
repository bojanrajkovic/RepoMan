using System.Collections.Generic;
using RepoMan.Repository.Models;

namespace RepoMan.Analysis
{
    interface ICommentAnalyzer
    {
        PullRequestCommentSnapshot CalculateCommentStatistics(PullRequest pr);

        /// <summary>
        /// Returns the list 
        /// </summary>
        /// <param name="pr"></param>
        /// <returns></returns>
        IDictionary<string, List<Comment>> GetApprovals(PullRequest pr);
    }
}
