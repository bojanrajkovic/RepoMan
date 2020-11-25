using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RepoMan.Repository.Models;

namespace RepoMan.IO
{
    class FilesystemCacheManager :
        ICacheManager
    {
        private readonly IFilesystem _fs;
        private readonly string _cacheParentDirectory;
        private readonly JsonSerializerSettings _jsonSerializerSettings;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fs"></param>
        /// <param name="pathToCache">The directory that contains the cache files</param>
        /// <param name="jsonSerializerSettings"></param>
        public FilesystemCacheManager(IFilesystem fs, string pathToCache, JsonSerializerSettings jsonSerializerSettings)
        {
            _fs = fs ?? throw new ArgumentNullException(nameof(fs));
            _cacheParentDirectory = string.IsNullOrWhiteSpace(pathToCache)
                ? throw new ArgumentNullException(nameof(pathToCache))
                : pathToCache;
            _jsonSerializerSettings = jsonSerializerSettings ?? throw new ArgumentNullException(nameof(jsonSerializerSettings));
        }

        public async ValueTask SaveAsync(IList<PullRequest> prDetails, string repoOwner, string repoName)
        {
            var path = GetPullRequestDetailsPath(repoOwner, repoName);
            var json = JsonConvert.SerializeObject(prDetails, _jsonSerializerSettings);
            await _fs.FileWriteAllTextAsync(path, json);
        }

        public async ValueTask<IList<PullRequest>> LoadAsync(string repoOwner, string repoName)
        {
            var path = GetPullRequestDetailsPath(repoOwner, repoName);
            var json = await _fs.FileReadAllTextAsync(path);
            return JsonConvert.DeserializeObject<List<PullRequest>>(json, _jsonSerializerSettings);
        }

        private string GetPullRequestDetailsPath(string repoOwner, string repoName)
            => Path.Combine(_cacheParentDirectory, $"{repoOwner}-{repoName}-prs.json");
    }
}
