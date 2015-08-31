using System.Collections.Generic;
using System.Threading.Tasks;
using VSOCommon;

namespace ExtractVSOProjectInfo
{
    public class Project
    {
        public string Name { get; set; }
        public int BuildCount { get; set; }
        public List<ProjectMember> Members { get; set; } 
    }
}
