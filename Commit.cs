using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractVSOProjectInfo
{
    public class Commit
    {
        public string id { get; set; }
        public ProjectMember author { get; set; }
    }
}
