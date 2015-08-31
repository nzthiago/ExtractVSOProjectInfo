using ExtractVSOProjectInfo;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace VSOCommon
{
    //TODO: Create classes that represent the JSON responses and use JsonConvert.Deserialize<TResult> to deserialize into objects of the classes instead of doing the conversion to lists/dictionaries
    public class VSOQuery : IDisposable
    {
        private string _instance;
        private string _userName;
        private string _password;
        private AuthenticationHeaderValue _authHeader;
        private HttpClient _client;
        
        public VSOQuery(string instance, string userName, string password)
        {
            _instance = instance;
            _userName = userName;
            _password = password;
            _authHeader = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(
                        Encoding.ASCII.GetBytes(
                            string.Format("{0}:{1}", userName, password))));
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

            _client.DefaultRequestHeaders.Authorization = _authHeader;
        }

        public async Task<dynamic> GrabJSONDataWithPath(string path)
        {
            string url = "https://" + _instance + "/DefaultCollection/" + path;
            return await GrabJSONData(url);
        }

        public async Task<dynamic> GrabJSONData(string url)
        {
            using (HttpResponseMessage response = await _client.GetAsync(url))
            {
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                dynamic obj = JToken.Parse(responseBody);
                return obj;
            }
        }

        public async Task<dynamic> GetJSONTeamProjects()
        {
            //Get all the projects
            var projects = await GrabJSONDataWithPath("_apis/projects?api-version=2.0");
            return projects;
        }

        public async Task<List<string>> GetTeamProjects()
        {
            List<string> projects = new List<string>();
            //Get all the projects
            var projectsJSON = await GetJSONTeamProjects();
            foreach (var project in projectsJSON.value)
            {
                projects.Add(project["name"].ToString());
            }
            return projects;
        }

        public async Task<dynamic> GetJSONTeamsInProjects(string projectName)
        {
            //Get all the teams in each project
            var teams = await GrabJSONDataWithPath(string.Format("_apis/projects/{0}/teams?api-version=2.0", projectName));
            return teams;
        }

        public async Task<List<string>> GetTeamsInProjects(string projectName)
        {
            List<string> teams = new List<string>();
            var teamsJSON = await GetJSONTeamsInProjects(projectName);
            foreach (var team in teamsJSON.value)
            {
                teams.Add(team["name"].ToString());
            }
            return teams;
        }

        public async Task<dynamic> GetJSONMembersInTeam(string projectName, string teamName)
        {
            //Get all the members in each team
            var members = await GrabJSONDataWithPath(string.Format("_apis/projects/{0}/teams/{1}/members?api-version=2.0", projectName, teamName));
            return members;
        }

        public async Task<List<ProjectMember>> GetMembersInTeam(string projectName, string teamName)
        {
            List<ProjectMember> members = new List<ProjectMember>();
            var membersJSON = await GetJSONMembersInTeam(projectName, teamName);
            foreach (var member in membersJSON.value)
            {
                members.Add( new ProjectMember 
                                { Project = projectName,
                                  Id = member["id"].ToString(),
                                  Name = member["displayName"].ToString(),
                                  Email = member["uniqueName"].ToString(),
                                  Commits = 0 });
            }
            return members;
        }

        public async Task<dynamic> GetJSONRepositories()
        {
            //Get all the repositories in this VSO account
            var repositories = await GrabJSONDataWithPath("_apis/git/repositories?api-version=2.0");
            return repositories;
        }

        public async Task<List<Tuple<string,string>>> GetRepositories()
        {
            List<Tuple<string, string>> repositories = new List<Tuple<string, string>>();
            var repositoriesJSON = await GetJSONRepositories();
            foreach (var repository in repositoriesJSON.value)
            {
                repositories.Add(Tuple.Create(repository["id"].ToString(), repository.project.name.Value));
            }
            return repositories;
        }

        public async Task<dynamic> GetJSONCommits(string repositoryId)
        {
            //Get all the commits in a repo
            var commits = await GrabJSONDataWithPath(string.Format("_apis/git/repositories/{0}/commits?api-version=2.0", repositoryId));
            return commits;
        }

        public async Task<List<Commit>> GetCommits(string repositoryId)
        {
            var commitsJSON = await GetJSONCommits(repositoryId);
            var commits = await Task.WhenAll(
                ((JArray)commitsJSON.value).Select(
                    async commitJSON =>
                    {
                        var commitid = commitJSON["commitId"].ToString();
                        Commit commit = new Commit();
                        commit.id = commitid;
                        commit.author = await GetCommitAuthor(repositoryId, commitid);
                        return commit;
                    }));
            return commits.ToList();

        }

        public async Task<dynamic> GetJSONCommit(string repositoryId, string commitId)
        {
            //Get more details about the commit so we can get the pusher's id to match with team member id later
            var commit = await GrabJSONDataWithPath(string.Format("_apis/git/repositories/{0}/commits/{1}?api-version=2.0", repositoryId, commitId));
            return commit;
        }

        public async Task<ProjectMember> GetCommitAuthor(string repositoryId, string commitId)
        {
            var commitJSON = await GetJSONCommit(repositoryId, commitId);

            ProjectMember commitMember = new ProjectMember()
            {
                Id = commitJSON.push.pushedBy.id,
                Name = commitJSON.push.pushedBy.displayName,
                Email = commitJSON.push.pushedBy.uniqueName,
                Commits = 1
            };
            return commitMember;
        }

        public async Task<dynamic> GetJSONBuilds(string projectName)
        {
            //Get all the builds in the project
            var builds = await GrabJSONDataWithPath(string.Format("{0}/_apis/build/builds?api-version=2.0", projectName));
            return builds;
        }

        public async Task<int> GetBuildCount(string projectName)
        {
            var buildsJSON = await GetJSONBuilds(projectName);
            return Convert.ToInt32(buildsJSON.count);
        }

        public void Dispose()
        {
            ((IDisposable)_client).Dispose();
        }
    }
}
