using System;

namespace RepoMan.Repository.Models
{
    public class TargetRepository
    {
        public long Id { get; set; }
        public string HtmlUrl { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset PushedAt { get; set; }
        public long Size { get; set; }
        public bool IsArchived { get; set; }
    }
}