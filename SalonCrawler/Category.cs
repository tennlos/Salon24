using System;
using System.Text;
using System.Collections.Generic;


namespace SalonCrawler {
    
    public partial class Category {
        public Category() { }
        public virtual int Id { get; set; }
        public virtual string Code { get; set; }
        public virtual string Name { get; set; }
        public virtual IList<Post> Posts { get; set; }
    }
}
