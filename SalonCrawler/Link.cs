using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SalonCrawler
{
    public class Link
    {
        public virtual int Id { get; set; }
        public virtual string Domain { get; set; }
        public virtual string URL { get; set; }
        public virtual IList<Post> Posts { get; set; }
        public virtual IList<Post> Comments { get; set; }
    }
}
