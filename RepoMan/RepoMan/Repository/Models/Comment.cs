using System;

namespace RepoMan.Repository.Models
{
    public class Comment
    {
        public long Id { get; set; }
        public User User { get; set; }
        public string HtmlUrl { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>
        /// Null, unless the comment is associated with a review comment where someone has approved it, or requested changes or whatever
        /// </summary>
        public string ReviewState { get; set; }
        public string Text { get; set; }
    }
}
