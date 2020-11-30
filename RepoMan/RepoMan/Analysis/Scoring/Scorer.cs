using System;
using System.Collections.Generic;
using RepoMan.Repository;

namespace RepoMan.Analysis.Scoring
{
    abstract class Scorer
    {
        public abstract string Attribute { get; }
        public abstract double ScoreMultiplier { get; }
        public abstract Score GetScore(PullRequestDetails prDetails);
    }
    
    abstract class PullRequestScorer : Scorer
    {
        public abstract int Count(PullRequestDetails prDetails);

        public override Score GetScore(PullRequestDetails prDetails)
        {
            var count = Count(prDetails);
            var rawPoints = count * ScoreMultiplier;
            var points = Math.Round(rawPoints, 2, MidpointRounding.AwayFromZero);
            return new Score
            {
                Attribute = Attribute,
                Count = count,
                Points = points,
            };
        }
    }
    
    abstract class CommentExtractorScorer : PullRequestScorer
    {
        /// <summary>
        /// Returns the matching elements from the specified string
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public abstract IEnumerable<string> Extract(string s);
    }
}