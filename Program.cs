using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VSOCommon;
using System.Configuration;

namespace ExtractVSOProjectInfo
{
    public class Program
    {
        //TODO Add unit test project
        private static VSOQuery vso;

        public static void Main(string[] args)
        {
            //VSO Alternate Authentication Credential https://www.visualstudio.com/integrate/get-started/auth/overview
            string _userName = ConfigurationManager.AppSettings["user"];
            string _password = ConfigurationManager.AppSettings["password"];
            string _url = ConfigurationManager.AppSettings["url"];
            vso = new VSOQuery(_url, _userName, _password);

            Task.Run(() => MainAsync()).Wait();
        }

        private static async Task MainAsync()
        {
            LogMsg("Connecting to VSO", ConsoleColor.Cyan);

            try
            {
                LogMsg("Getting members", ConsoleColor.Cyan);
                var projects = await GetProjects();

                LogMsg("Getting repositories", ConsoleColor.Cyan);
                var commits = await GetCommitStats();

                WriteFiles(projects, commits);

                LogMsg("Press any key to close", ConsoleColor.Cyan);
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                LogMsg(ex.Message, ConsoleColor.Red);
                Console.ReadKey();
            }
        }

        //Gets all the projects, build counts and team members
        private static async Task<IList<Project>> GetProjects()
        {
            var projectIds = await vso.GetTeamProjects();

            return await Task.WhenAll(
                projectIds.Select(
                    async projectId => await GetProject(projectId)));
        }

        private static async Task<Project> GetProject(string projectId)
        {
            LogMsg(string.Format("Going through project {0}", projectId), ConsoleColor.White);
            Project newProject = new Project() { Name = projectId };

            //note there's 10 day default retention setting for build history in VSO
            newProject.BuildCount = await vso.GetBuildCount(projectId);
            newProject.Members = await GetProjectMembers(projectId);

            return newProject;
        }

        private static async Task<List<ProjectMember>> GetProjectMembers(string projectId)
        {
            //Loop through teams in case there's more than one team in a project
            var teams = await vso.GetTeamsInProjects(projectId);
            var teamMembers = await Task.WhenAll(
                teams.Select(async team => await GetTeamMembers(team, projectId))
                );
            return teamMembers
                    .SelectMany(x => x.ToList()) //flatten
                    .GroupBy(x => x.Id) //group same member in different teams 
                    .Select(x => x.First()).ToList(); //distinct   
        }

        private static async Task<List<ProjectMember>> GetTeamMembers(string team, string projectId)
        {
            LogMsg(string.Format("Going through team {0}", team), ConsoleColor.Yellow);
            //Loop through team members
            var r = await vso.GetMembersInTeam(projectId, team);
            return r;
        }

        //Gets number of commits per team member from the VSO account
        private static async Task<IList<ProjectMember>> GetCommitStats()
        {
            var repositories = await vso.GetRepositories();

            var projectCommits = await Task.WhenAll(
                repositories.Select(
                    async repository => await GetCommitAuthorCounts(repository)));

            return projectCommits
                .SelectMany(x => x.ToList()) //flatten
                .GroupBy(x => x.Id) //group commit authors 
                .Select(x =>
                    new ProjectMember
                    {
                        Project = x.First().Project,
                        Id = x.First().Id,
                        Name = x.First().Name,
                        Email = x.First().Email,
                        Commits = x.Sum(y => y.Commits)
                    }).ToList(); //distinct with sum
        }

        private static async Task<List<ProjectMember>> GetCommitAuthorCounts(Tuple<string, string> repository)
        {
            var repositoryId = repository.Item1;
            LogMsg(string.Format("Going through repository {0}", repositoryId), ConsoleColor.White);

            // Note: this only returns the top 1000 commits in a repo, use API pagination if you repo has more
            var commits = await vso.GetCommits(repositoryId);

            var commitsByAuthor = commits
                .GroupBy(x => x.author.Id)
                .Select(
                    x => new ProjectMember
                    {
                        Project = repository.Item2,
                        Id = x.First().author.Id,
                        Name = x.First().author.Name,
                        Email = x.First().author.Email,
                        Commits = x.Count()
                    }).ToList();

            return commitsByAuthor;
        }

        private static void WriteFiles(IList<Project> projects, IList<ProjectMember> commits)
        {
            LINQtoCSV.CsvContext csv = new LINQtoCSV.CsvContext();

            //File 1: projects and their build counts
            csv.Write(
                (from p in projects select new { Project = p.Name, p.BuildCount }).OrderBy(x => x.Project),
                "ProjectBuilds.csv");

            //File 2: project members and their count of commits
            csv.Write(
                //returns commits even if member has left the team since committing code
                (
                    from commit in commits 
                    group commit by new { commit.Project, commit.Name } into gc
                    select new
                    {
                        Project = gc.First().Project,
                        Member = gc.First().Name,
                        gc.First().Email,
                        Commits = gc.Sum(c => c.Commits)
                    }
                //gets the members that did not commit any code
                ).Union( 
                    from projectmembers in projects.SelectMany(x => x.Members)
                    where !(from commit in commits select commit.Id).Contains(projectmembers.Id)
                    select new
                        {
                            Project = projectmembers.Project,
                            Member = projectmembers.Name,
                            projectmembers.Email,
                            Commits = 0
                        }
                ).OrderBy(x => x.Project).ThenByDescending(x => x.Commits),
                "MemberCommits.csv");
        }

        private static void LogMsg(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
        }
    }
}