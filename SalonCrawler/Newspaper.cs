using System;
using System.Text;
using System.Collections.Generic;


namespace SalonCrawler {
    
    public partial class Newspaper {
        public Newspaper() { }
        public virtual int Id { get; set; }
        public virtual User User { get; set; }
        public virtual string Name { get; set; }
        public virtual string Description { get; set; }
        public virtual string ExtendedDescription { get; set; }
        public virtual IList<Repost> Reposts { get; set; }
    }
}
