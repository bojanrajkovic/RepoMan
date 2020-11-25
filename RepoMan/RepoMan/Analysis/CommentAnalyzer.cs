using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using RepoMan.Analysis.ApprovalAnalyzers;
using RepoMan.Repository.Models;

namespace RepoMan.Analysis
{
    class CommentAnalyzer :
        ICommentAnalyzer
    {
        private static readonly StringComparison _comparison = StringComparison.OrdinalIgnoreCase;
        private readonly IApprovalAnalyzer _approvalAnalyzer;
        private readonly IWordCounter _wordCounter;

        public CommentAnalyzer(IApprovalAnalyzer approvalAnalyzer, IWordCounter wordCounter)
        {
            _approvalAnalyzer = approvalAnalyzer ?? throw new ArgumentNullException(nameof(approvalAnalyzer));
            _wordCounter = wordCounter ?? throw new ArgumentNullException(nameof(wordCounter));
        }

        public PullRequestCommentSnapshot CalculateCommentStatistics(PullRequest pr)
        {
            var approvals = GetApprovals(pr);
            var nonEmptyComments = GetNonEmptyComments(pr);

            var wordCounts = nonEmptyComments
                .Select(c => _wordCounter.CountWords(c.Text))
                .ToList();

            // Comments with no words seem to make the stats less useful. They should probably be excluded from the comment statistics calculations
            var snapshot = new PullRequestCommentSnapshot
            {
                Timestamp = DateTimeOffset.UtcNow,
                Number = pr.Number,
                OpenedAt = pr.OpenedAt,
                ClosedAt = pr.ClosedAt,
                ApprovalCount = approvals.Count,
                CommentCount = nonEmptyComments.Count,
                CommentWordCount = wordCounts.Sum(),
                MedianWordsPerComment = wordCounts.CalculateMedian(),
            };
            return snapshot;
        }

        private List<Comment> GetNonEmptyComments(PullRequest pr)
        {
            return pr.AllComments
                .Where(c => !string.IsNullOrWhiteSpace(c.Text))
                .ToList();
        }

        /// <summary>
        /// Returns the list 
        /// </summary>
        /// <param name="pr"></param>
        /// <returns></returns>
        public IDictionary<string, List<Comment>> GetApprovals(PullRequest pr)
        {
            return pr.ReviewComments
                .Where(rc => _approvalAnalyzer.IsApproved(rc))
                .GroupBy(rc => rc.User.Login)
                .ToDictionary(
                    reviewer => reviewer.Key,
                    reviewer => reviewer.ToList(),
                    StringComparer.FromComparison(_comparison));
        }
    }

    public class PullRequestCommentSnapshot
    {
        public DateTimeOffset Timestamp { get; set; }
        public DateTimeOffset OpenedAt { get; set; }
        public DateTimeOffset ClosedAt { get; set; }
        
        [JsonIgnore]
        public TimeSpan OpenFor => ClosedAt - OpenedAt;
        
        public int Number { get; set; }
        public int CommentCount { get; set; }
        public int CommentWordCount { get; set; }
        public int ApprovalCount { get; set; }
        
        /// <summary>
        /// Median word count for comments on this pull request
        /// </summary>
        public int MedianWordsPerComment { get; set; }
    }
}
